using System.Collections.ObjectModel;
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
/// Muestrea los procesos del sistema y mantiene una ObservableCollection viva:
/// actualiza en sitio las entradas existentes (para no perder iconos cacheados)
/// y anade/quita segun procesos que aparecen o terminan.
///
/// CPU: delta de TotalProcessorTime normalizado sobre el numero de nucleos,
/// igual que el Administrador de tareas (100% = toda la maquina).
/// GPU/VRAM: contadores de rendimiento "GPU Engine" y "GPU Process Memory",
/// agregados por PID a partir del nombre de instancia (formato "pid_1234_...").
/// PID del padre: leido via Toolhelp32Snapshot (no expuesto por System.Diagnostics.Process),
/// usado solo para agrupar procesos del mismo tipo (ej. varios Discord.exe).
/// </summary>
public class ProcessMonitorService : IDisposable
{
    public ObservableCollection<ProcessSample> Processes { get; } = new();

    private readonly Dictionary<int, ProcessSample> _samplesByPid = new();
    private readonly Dictionary<int, (TimeSpan CpuTime, DateTime Timestamp)> _lastCpu = new();
    private readonly Dictionary<string, ImageSource?> _iconCache = new();

    private readonly Dictionary<string, PerformanceCounter> _gpuUtilCounters = new();
    private readonly Dictionary<string, PerformanceCounter> _gpuMemCounters = new();

    private static readonly Regex PidRegex = new(@"pid_(\d+)_", RegexOptions.Compiled);

    private readonly double _totalRamBytes;
    private readonly double _totalVramBytes;

    public ProcessMonitorService()
    {
        _totalRamBytes = GetTotalPhysicalMemoryBytes();
        _totalVramBytes = GetTotalVramBytes();
    }

    /// <summary>Toma una muestra nueva de todos los procesos y actualiza la coleccion.
    /// Pensado para llamarse desde un DispatcherTimer cada 1-2 segundos.</summary>
    public void Refresh()
    {
        var now = DateTime.UtcNow;
        var gpuUtilByPid = SampleGpuUtilizationByPid();
        var gpuMemByPid = SampleGpuMemoryByPid();
        var parentMap = GetParentPidMap();

        Process[] processes;
        try { processes = Process.GetProcesses(); }
        catch { return; }

        var seenPids = new HashSet<int>();

        foreach (var proc in processes)
        {
            int pid;
            string name;
            try
            {
                pid = proc.Id;
                name = proc.ProcessName;
            }
            catch { proc.Dispose(); continue; }

            seenPids.Add(pid);

            double cpuPercent = 0;
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
                // proceso protegido (sin acceso a su tiempo de CPU): lo dejamos a 0 en vez de reventar el refresco.
            }

            double ramBytes = 0;
            try { ramBytes = proc.WorkingSet64; } catch { }
            var ramMb = ramBytes / 1024.0 / 1024.0;
            var ramPercent = _totalRamBytes > 0 ? ramBytes / _totalRamBytes * 100.0 : 0;

            var gpuPercent = gpuUtilByPid.TryGetValue(pid, out var gu) ? Math.Clamp(gu, 0, 100) : 0;
            var vramBytes = gpuMemByPid.TryGetValue(pid, out var vb) ? vb : 0;
            var vramMb = vramBytes / 1024.0 / 1024.0;
            var vramPercent = _totalVramBytes > 0 ? vramBytes / _totalVramBytes * 100.0 : 0;

            bool responding = true;
            try { responding = proc.Responding; } catch { }

            string? path = null;
            try { path = proc.MainModule?.FileName; } catch { /* procesos de sistema: sin acceso al modulo principal */ }

            if (!_samplesByPid.TryGetValue(pid, out var sample))
            {
                sample = new ProcessSample(pid, name) { ExecutablePath = path };
                sample.Icon = GetIcon(path, name);
                _samplesByPid[pid] = sample;
                Processes.Add(sample);
            }

            sample.Name = name;
            sample.ParentPid = parentMap.TryGetValue(pid, out var ppid) ? ppid : 0;
            sample.CpuPercent = cpuPercent;
            sample.RamMB = ramMb;
            sample.RamPercent = ramPercent;
            sample.GpuPercent = gpuPercent;
            sample.VramMB = vramMb;
            sample.VramPercent = vramPercent;
            sample.IsResponding = responding;

            proc.Dispose();
        }

        foreach (var pid in _samplesByPid.Keys.Where(p => !seenPids.Contains(p)).ToList())
        {
            Processes.Remove(_samplesByPid[pid]);
            _samplesByPid.Remove(pid);
            _lastCpu.Remove(pid);
        }
    }

    /// <summary>Agrupa la ultima muestra por familia de procesos: un proceso es "raiz"
    /// de un grupo si su padre no esta vivo o tiene un nombre distinto; todo hijo
    /// (directo o indirecto) que conserve el mismo nombre a lo largo de la cadena
    /// de padres se cuelga de esa raiz. Asi los varios Discord.exe quedan agrupados
    /// bajo la instancia principal.</summary>
    public List<ProcessGroup> BuildGroups()
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
                    bitmap.Freeze();
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

    // ===================== GPU (Performance Counters) =====================

    private Dictionary<int, double> SampleGpuUtilizationByPid() =>
        SampleGpuCategory("GPU Engine", "Utilization Percentage", _gpuUtilCounters);

    private Dictionary<int, double> SampleGpuMemoryByPid() =>
        SampleGpuCategory("GPU Process Memory", "Dedicated Usage", _gpuMemCounters);

    private Dictionary<int, double> SampleGpuCategory(
        string categoryName, string counterName, Dictionary<string, PerformanceCounter> counterCache)
    {
        var result = new Dictionary<int, double>();

        string[] instanceNames;
        try
        {
            if (!PerformanceCounterCategory.Exists(categoryName)) return result;
            instanceNames = new PerformanceCounterCategory(categoryName).GetInstanceNames();
        }
        catch
        {
            return result; // sin soporte de contadores de GPU en este equipo/driver
        }

        var current = new HashSet<string>(instanceNames);

        foreach (var stale in counterCache.Keys.Where(k => !current.Contains(k)).ToList())
        {
            counterCache[stale].Dispose();
            counterCache.Remove(stale);
        }

        foreach (var instanceName in instanceNames)
        {
            var match = PidRegex.Match(instanceName);
            if (!match.Success) continue;

            var pid = int.Parse(match.Groups[1].Value);
            if (pid == 0) continue;

            if (!counterCache.TryGetValue(instanceName, out var counter))
            {
                try
                {
                    counter = new PerformanceCounter(categoryName, counterName, instanceName, readOnly: true);
                    counterCache[instanceName] = counter;
                }
                catch
                {
                    continue; // la instancia desaparecio justo entre el listado y la creacion del contador
                }
            }

            double value;
            try { value = counter.NextValue(); }
            catch { continue; }

            result[pid] = result.TryGetValue(pid, out var existing) ? existing + value : value;
        }

        return result;
    }

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

    public void Dispose()
    {
        foreach (var counter in _gpuUtilCounters.Values) counter.Dispose();
        foreach (var counter in _gpuMemCounters.Values) counter.Dispose();
        _gpuUtilCounters.Clear();
        _gpuMemCounters.Clear();
    }
}