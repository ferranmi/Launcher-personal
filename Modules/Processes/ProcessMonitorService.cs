using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Launcher.Modules.Processes;

/// <summary>
/// Muestrea los procesos del sistema y mantiene una instancia de ProcessSample por proceso:
/// actualiza en sitio las existentes (para no perder iconos cacheados) y anade/quita segun
/// procesos que aparecen o terminan.
///
/// Pensado para ejecutarse en un hilo de fondo (Task.Run): Refresh() y BuildGroups() comparten
/// un cerrojo, asi que Dispose() espera a que termine un refresco en curso. Las muestras no
/// deben leerse desde la UI mientras haya un Refresh() ejecutandose (ProcessesView no solapa
/// refrescos).
///
/// CPU: delta de TotalProcessorTime normalizado sobre el numero de nucleos, igual que el
/// Administrador de tareas (100% = toda la maquina). Los procesos protegidos (sin acceso a su
/// tiempo de CPU) se recuerdan para no repetir la excepcion en cada refresco.
/// GPU/VRAM: se lee la categoria entera de contadores ("GPU Engine" y "GPU Process Memory") de
/// una sola vez y se agrega por PID a partir del nombre de instancia (formato "pid_1234_...").
/// En vez de un PerformanceCounter por instancia, se comparan dos lecturas consecutivas.
/// Ruta del ejecutable: solo se lee la primera vez que se ve un proceso.
/// Congelado: IsHungAppWindow sobre las ventanas visibles (no bloquea, a diferencia de
/// Process.Responding, que puede esperar varios segundos por cada aplicacion colgada).
/// PID del padre: Toolhelp32Snapshot, usado solo para agrupar procesos del mismo tipo.
/// </summary>
public class ProcessMonitorService : IDisposable
{
    private readonly object _gate = new();

    private readonly Dictionary<int, ProcessSample> _samplesByPid = new();
    private readonly Dictionary<int, (TimeSpan CpuTime, DateTime Timestamp)> _lastCpu = new();
    private readonly HashSet<int> _cpuDenied = new();
    private readonly Dictionary<string, ImageSource?> _iconCache = new();

    // null = aun no comprobado; false = no disponible en este equipo/driver (no se vuelve a intentar).
    private bool? _gpuEngineAvailable;
    private bool? _gpuMemoryAvailable;
    private readonly Dictionary<string, CounterSample> _prevGpuEngine = new();

    private static readonly Regex PidRegex = new(@"pid_(\d+)_", RegexOptions.Compiled);

    private readonly double _totalRamBytes;
    private readonly double _totalVramBytes;

    /// <summary>Procesos conocidos tras el ultimo refresco.</summary>
    public IReadOnlyCollection<ProcessSample> Processes => _samplesByPid.Values;

    public ProcessMonitorService()
    {
        _totalRamBytes = GetTotalPhysicalMemoryBytes();
        _totalVramBytes = GetTotalVramBytes();
    }

    /// <summary>Toma una muestra nueva de todos los procesos. Pensado para llamarse cada 1-2
    /// segundos desde un hilo de fondo.</summary>
    public void Refresh()
    {
        lock (_gate)
        {
            RefreshCore();
        }
    }

    private void RefreshCore()
    {
        var now = DateTime.UtcNow;
        var gpuUtilByPid = SampleGpuUtilizationByPid();
        var gpuMemByPid = SampleGpuMemoryByPid();
        var parentMap = GetParentPidMap();
        var hungPids = GetHungProcessIds();

        Process[] processes;
        try { processes = Process.GetProcesses(); }
        catch { return; }

        var seenPids = new HashSet<int>();

        foreach (var proc in processes)
        {
            using (proc)
            {
                int pid;
                string name;
                try
                {
                    pid = proc.Id;
                    name = proc.ProcessName;
                }
                catch { continue; }

                seenPids.Add(pid);

                // CPU: si un proceso nego el acceso una vez, se salta en los siguientes
                // refrescos (lanzar y capturar la excepcion cada segundo es lo mas caro).
                double cpuPercent = 0;
                if (!_cpuDenied.Contains(pid))
                {
                    try
                    {
                        var cpuTime = proc.TotalProcessorTime;
                        if (_lastCpu.TryGetValue(pid, out var prev))
                        {
                            var elapsedMs = (now - prev.Timestamp).TotalMilliseconds;
                            if (elapsedMs > 0)
                            {
                                var cpuDeltaMs = (cpuTime - prev.CpuTime).TotalMilliseconds;
                                cpuPercent = Math.Clamp(cpuDeltaMs / elapsedMs / Environment.ProcessorCount * 100.0, 0, 100);
                            }
                        }
                        _lastCpu[pid] = (cpuTime, now);
                    }
                    catch
                    {
                        _cpuDenied.Add(pid);
                    }
                }

                double ramBytes = 0;
                try { ramBytes = proc.WorkingSet64; } catch { }
                var ramMb = ramBytes / 1024.0 / 1024.0;
                var ramPercent = _totalRamBytes > 0 ? ramBytes / _totalRamBytes * 100.0 : 0;

                var gpuPercent = gpuUtilByPid.TryGetValue(pid, out var gu) ? Math.Clamp(gu, 0, 100) : 0;
                var vramBytes = gpuMemByPid.TryGetValue(pid, out var vb) ? vb : 0;
                var vramMb = vramBytes / 1024.0 / 1024.0;
                var vramPercent = _totalVramBytes > 0 ? vramBytes / _totalVramBytes * 100.0 : 0;

                // Un PID puede reutilizarse para otro programa: si el nombre ya no coincide,
                // se trata como un proceso nuevo (icono y ruta nuevos).
                if (_samplesByPid.TryGetValue(pid, out var sample) &&
                    !string.Equals(sample.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    _samplesByPid.Remove(pid);
                    _lastCpu.Remove(pid);
                    _cpuDenied.Remove(pid);
                    sample = null;
                }

                if (sample == null)
                {
                    // La ruta (y con ella el icono) solo se lee la primera vez que se ve el proceso.
                    string? path = null;
                    try { path = proc.MainModule?.FileName; } catch { /* procesos de sistema: sin acceso al modulo principal */ }

                    sample = new ProcessSample(pid, name) { ExecutablePath = path };
                    sample.Icon = GetIcon(path, name);
                    _samplesByPid[pid] = sample;
                }

                sample.Name = name;
                sample.ParentPid = parentMap.TryGetValue(pid, out var ppid) ? ppid : 0;
                sample.CpuPercent = cpuPercent;
                sample.RamMB = ramMb;
                sample.RamPercent = ramPercent;
                sample.GpuPercent = gpuPercent;
                sample.VramMB = vramMb;
                sample.VramPercent = vramPercent;
                sample.IsResponding = !hungPids.Contains(pid);
            }
        }

        foreach (var pid in _samplesByPid.Keys.Where(p => !seenPids.Contains(p)).ToList())
        {
            _samplesByPid.Remove(pid);
            _lastCpu.Remove(pid);
            _cpuDenied.Remove(pid);
        }
    }

    /// <summary>Agrupa la ultima muestra por familia de procesos: un proceso es "raiz"
    /// de un grupo si su padre no esta vivo o tiene un nombre distinto; todo hijo
    /// (directo o indirecto) que conserve el mismo nombre a lo largo de la cadena
    /// de padres se cuelga de esa raiz. Asi los varios Discord.exe quedan agrupados
    /// bajo la instancia principal.</summary>
    public List<ProcessGroup> BuildGroups()
    {
        lock (_gate)
        {
            var byPid = _samplesByPid;
            var rootCache = new Dictionary<int, int>();

            int FindRoot(ProcessSample s)
            {
                if (rootCache.TryGetValue(s.Pid, out var cached)) return cached;

                var current = s;
                var visited = new HashSet<int> { current.Pid };

                while (byPid.TryGetValue(current.ParentPid, out var parent) &&
                       string.Equals(parent.Name, s.Name, StringComparison.OrdinalIgnoreCase) &&
                       visited.Add(parent.Pid))
                {
                    current = parent;
                }

                rootCache[s.Pid] = current.Pid;
                return current.Pid;
            }

            var groups = new Dictionary<int, ProcessGroup>();
            foreach (var sample in byPid.Values)
            {
                var rootPid = FindRoot(sample);
                if (!groups.TryGetValue(rootPid, out var group))
                {
                    var rootSample = byPid.TryGetValue(rootPid, out var rs) ? rs : sample;
                    group = new ProcessGroup(rootSample);
                    groups[rootPid] = group;
                }

                if (sample.Pid != rootPid) group.Children.Add(sample);
            }

            return groups.Values.ToList();
        }
    }

    // ===================== Finalizar tarea =====================

    public bool TryKill(ProcessSample sample, out string? error)
    {
        error = null;

        if (sample.Pid == Environment.ProcessId)
        {
            error = "No se puede finalizar el propio Launcher.";
            return false;
        }

        try
        {
            using var proc = Process.GetProcessById(sample.Pid);
            proc.Kill(entireProcessTree: true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // ===================== Iconos (con cache) =====================

    private ImageSource? GetIcon(string? path, string name)
    {
        var cacheKey = path ?? name;
        if (_iconCache.TryGetValue(cacheKey, out var cached)) return cached;

        ImageSource? image = null;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            System.Drawing.Icon? icon = null;
            try
            {
                icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                {
                    var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bitmap.Freeze(); // congelado: se puede usar desde el hilo de la UI
                    image = bitmap;
                }
            }
            catch
            {
                // sin permisos para leer el ejecutable, o icono no valido: sin icono.
            }
            finally
            {
                icon?.Dispose();
            }
        }

        _iconCache[cacheKey] = image;
        return image;
    }

    // ===================== GPU (lectura de categoria completa) =====================

    /// <summary>Utilizacion de GPU por PID: suma de todos los motores del proceso. "Utilization
    /// Percentage" es un contador de tasa, asi que se calcula entre la lectura anterior y esta.
    /// La primera lectura solo fija la referencia (0 para todos).</summary>
    private Dictionary<int, double> SampleGpuUtilizationByPid()
    {
        var result = new Dictionary<int, double>();
        if (_gpuEngineAvailable == false) return result;

        try
        {
            if (_gpuEngineAvailable == null)
            {
                _gpuEngineAvailable = PerformanceCounterCategory.Exists("GPU Engine");
                if (_gpuEngineAvailable == false) return result;
            }

            var data = new PerformanceCounterCategory("GPU Engine").ReadCategory();
            if (!data.Contains("Utilization Percentage")) return result;

            var seen = new HashSet<string>();

            foreach (InstanceData instance in data["Utilization Percentage"].Values)
            {
                var instanceName = instance.InstanceName;
                var match = PidRegex.Match(instanceName);
                if (!match.Success) continue;

                seen.Add(instanceName);
                var current = instance.Sample;

                if (_prevGpuEngine.TryGetValue(instanceName, out var previous))
                {
                    double value;
                    try { value = CounterSample.Calculate(previous, current); }
                    catch { value = 0; }

                    var pid = int.Parse(match.Groups[1].Value);
                    if (pid != 0)
                    {
                        result[pid] = result.TryGetValue(pid, out var existing) ? existing + value : value;
                    }
                }

                _prevGpuEngine[instanceName] = current;
            }

            foreach (var stale in _prevGpuEngine.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                _prevGpuEngine.Remove(stale);
            }
        }
        catch
        {
            // Sin soporte de contadores de GPU (o fallo del driver): no se reintenta en cada tick.
            _gpuEngineAvailable = false;
            _prevGpuEngine.Clear();
        }

        return result;
    }

    /// <summary>VRAM dedicada por PID. Es un valor instantaneo, no hace falta lectura previa.</summary>
    private Dictionary<int, double> SampleGpuMemoryByPid()
    {
        var result = new Dictionary<int, double>();
        if (_gpuMemoryAvailable == false) return result;

        try
        {
            if (_gpuMemoryAvailable == null)
            {
                _gpuMemoryAvailable = PerformanceCounterCategory.Exists("GPU Process Memory");
                if (_gpuMemoryAvailable == false) return result;
            }

            var data = new PerformanceCounterCategory("GPU Process Memory").ReadCategory();
            if (!data.Contains("Dedicated Usage")) return result;

            foreach (InstanceData instance in data["Dedicated Usage"].Values)
            {
                var match = PidRegex.Match(instance.InstanceName);
                if (!match.Success) continue;

                var pid = int.Parse(match.Groups[1].Value);
                if (pid == 0) continue;

                double value = instance.Sample.RawValue;
                result[pid] = result.TryGetValue(pid, out var existing) ? existing + value : value;
            }
        }
        catch
        {
            _gpuMemoryAvailable = false;
        }

        return result;
    }

    // ===================== Congelados (ventanas colgadas) =====================

    /// <summary>PIDs con alguna ventana visible que Windows considera colgada. Una sola
    /// enumeracion de ventanas por refresco, sin llamar a Process.Responding.</summary>
    private static HashSet<int> GetHungProcessIds()
    {
        var hung = new HashSet<int>();

        try
        {
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                GetWindowThreadProcessId(hWnd, out var pid);
                if (pid != 0 && !hung.Contains((int)pid) && IsHungAppWindow(hWnd))
                {
                    hung.Add((int)pid);
                }

                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // Sin acceso a la lista de ventanas: nada se marca como congelado.
        }

        return hung;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsHungAppWindow(IntPtr hWnd);

    // ===================== Totales del sistema =====================

    private static double GetTotalPhysicalMemoryBytes()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref status) ? status.ullTotalPhys : 0;
    }

    /// <summary>Igual que en Monitor: WMI AdapterRAM no es fiable para GPUs de mas de 4 GB,
    /// asi que se lee HardwareInformation.qwMemorySize del registro.</summary>
    private static double GetTotalVramBytes()
    {
        const string basePath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        double best = 0;

        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(basePath);
            if (classKey == null) return 0;

            foreach (var subKeyName in classKey.GetSubKeyNames())
            {
                if (!subKeyName.All(char.IsDigit)) continue;

                using var adapterKey = classKey.OpenSubKey(subKeyName);
                if (adapterKey?.GetValue("HardwareInformation.qwMemorySize") is long size && size > best)
                {
                    best = size;
                }
            }
        }
        catch
        {
            // sin acceso al registro: se queda a 0 y el % de VRAM simplemente no se calcula.
        }

        return best;
    }

    // ===================== PID del padre (Toolhelp32Snapshot) =====================

    private static Dictionary<int, int> GetParentPidMap()
    {
        var map = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE) return map;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (!Process32First(snapshot, ref entry)) return map;

            do
            {
                map[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
            } while (Process32Next(snapshot, ref entry));
        }
        catch
        {
            // snapshot corrupto a mitad de lectura: nos quedamos con lo que se haya podido leer.
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return map;
    }

    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);
    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll")]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    /// <summary>Descarta las referencias de CPU y GPU (la vista llama aqui al descargarse). El
    /// servicio sigue siendo reutilizable: el siguiente Refresh() vuelve a fijar la referencia,
    /// asi que el primer refresco tras volver a la vista muestra 0 y no una media de todo el
    /// tiempo que estuvo cerrada.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _lastCpu.Clear();
            _prevGpuEngine.Clear();
            _gpuEngineAvailable = null;
            _gpuMemoryAvailable = null;
        }
    }
}
