using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Launcher.Core.Models;

namespace Launcher.Core.Services;

/// <summary>
/// Lee metricas del sistema (CPU, RAM, GPU/VRAM, red, disco por unidad, procesos)
/// para el modulo Monitor. Sample() se llama una vez por segundo desde un
/// DispatcherTimer en la vista; los contadores de rendimiento y el historial
/// por proceso se mantienen entre llamadas para poder calcular deltas.
/// </summary>
public class SystemMonitorService : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    private ulong _prevIdle, _prevKernel, _prevUser;
    private bool _hasPrevCpu;

    private long _prevBytesReceived, _prevBytesSent;
    private DateTime _prevNetSampleTime = DateTime.MinValue;

    private readonly Dictionary<string, PerformanceCounter> _gpuEngineCounters = new();
    private readonly Dictionary<string, PerformanceCounter> _gpuMemCounters = new();

    private readonly Dictionary<string, (PerformanceCounter Read, PerformanceCounter Write)> _diskIoCounters =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<int, (TimeSpan CpuTime, DateTime SampleTime)> _prevProcessTimes = new();

    private bool _gpuInfoLoaded;
    private string _gpuName = "GPU";
    private ulong _vramTotalBytes;

    public SystemSnapshot Sample()
    {
        var cpu = SampleCpu();
        SampleMemory(out var ramPercent, out var ramUsed, out var ramTotal);
        LoadGpuInfoOnce();
        var gpuPercent = SampleGpuUtilization();
        var vramUsed = SampleVramUsed();
        var vramPercent = _vramTotalBytes > 0
            ? Math.Clamp(vramUsed / (double)_vramTotalBytes * 100.0, 0, 100)
            : 0;
        var (netDown, netUp) = SampleNetwork();
        var diskIoByDrive = SampleDiskIOPerDrive();
        var drives = SampleDrives(diskIoByDrive);
        
        return new SystemSnapshot
        {
            CpuPercent = cpu,
            RamPercent = ramPercent,
            RamUsedBytes = ramUsed,
            RamTotalBytes = ramTotal,
            GpuPercent = gpuPercent,
            GpuName = _gpuName,
            VramPercent = vramPercent,
            VramUsedBytes = vramUsed,
            VramTotalBytes = _vramTotalBytes,
            NetDownBytesPerSec = netDown,
            NetUpBytesPerSec = netUp,
            Drives = drives,
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
        };
    }

    /// <summary>Si es false, Sample() no calcula el top de procesos (ahorra recorrer todos los procesos).</summary>
    public bool IncludeTopProcesses { get; set; } = true;

    // ===================== CPU =====================

    private double SampleCpu()
    {
        if (!GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
            return 0;

        var idle = ToULong(idleFt);
        var kernel = ToULong(kernelFt);
        var user = ToULong(userFt);

        if (!_hasPrevCpu)
        {
            _prevIdle = idle; _prevKernel = kernel; _prevUser = user;
            _hasPrevCpu = true;
            return 0;
        }

        var idleDelta = idle - _prevIdle;
        var kernelDelta = kernel - _prevKernel;
        var userDelta = user - _prevUser;

        _prevIdle = idle; _prevKernel = kernel; _prevUser = user;

        var total = kernelDelta + userDelta;
        if (total == 0) return 0;

        var busy = total - idleDelta;
        return Math.Clamp((busy / (double)total) * 100.0, 0, 100);
    }

    private static ulong ToULong(FILETIME ft) => ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;

    // ===================== RAM =====================

    private static void SampleMemory(out double percent, out ulong usedBytes, out ulong totalBytes)
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };

        if (GlobalMemoryStatusEx(ref mem))
        {
            totalBytes = mem.ullTotalPhys;
            usedBytes = mem.ullTotalPhys - mem.ullAvailPhys;
            percent = mem.dwMemoryLoad;
        }
        else
        {
            totalBytes = 0; usedBytes = 0; percent = 0;
        }
    }

    // ===================== GPU / VRAM =====================

    private void LoadGpuInfoOnce()
    {
        if (_gpuInfoLoaded) return;
        _gpuInfoLoaded = true;

        ulong wmiRam = 0;

        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT Name, AdapterRAM FROM Win32_VideoController");

            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"] as string;
                var ramValue = obj["AdapterRAM"];
                var ram = ramValue != null ? Convert.ToUInt64(ramValue) : 0;

                if (ram >= wmiRam)
                {
                    wmiRam = ram;
                    _gpuName = name ?? "GPU";
                }
            }
        }
        catch
        {
            // WMI no disponible: seguimos, el registro puede aun darnos la VRAM.
        }

        // El AdapterRAM de WMI se trunca o desborda en muchas GPUs con mas de 4GB
        // (limitacion historica de Windows con ese campo de 32 bits). El registro
        // del driver tiene el valor real de 64 bits que el fabricante escribe al
        // instalarlo, asi que lo preferimos cuando esta disponible.
        var registryRam = TryGetVramFromRegistry();
        _vramTotalBytes = registryRam is > 0 ? registryRam.Value : wmiRam;
    }

    /// <summary>Lee el tamano real de VRAM desde la clave de registro que el driver
    /// de la GPU escribe al instalarse (HardwareInformation.qwMemorySize). Si hay
    /// varias GPUs (p.ej. integrada + dedicada), se queda con el valor mas alto.</summary>
    private static ulong? TryGetVramFromRegistry()
    {
        const string basePath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(basePath);
            if (baseKey == null) return null;

            ulong best = 0;
            foreach (var subKeyName in baseKey.GetSubKeyNames())
            {
                if (!subKeyName.All(char.IsDigit)) continue;

                using var subKey = baseKey.OpenSubKey(subKeyName);
                var value = subKey?.GetValue("HardwareInformation.qwMemorySize");

                if (value is long qword && (ulong)qword > best)
                {
                    best = (ulong)qword;
                }
            }

            return best > 0 ? best : null;
        }
        catch
        {
            return null;
        }
    }

    private double SampleGpuUtilization()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine")) return 0;

            var category = new PerformanceCounterCategory("GPU Engine");
            var instanceNames = category.GetInstanceNames()
                .Where(n => n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .ToHashSet();

            foreach (var stale in _gpuEngineCounters.Keys.Except(instanceNames).ToList())
            {
                _gpuEngineCounters[stale].Dispose();
                _gpuEngineCounters.Remove(stale);
            }

            foreach (var name in instanceNames)
            {
                if (!_gpuEngineCounters.ContainsKey(name))
                {
                    _gpuEngineCounters[name] = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, true);
                }
            }

            double total = 0;
            foreach (var counter in _gpuEngineCounters.Values)
            {
                try { total += counter.NextValue(); }
                catch { /* la instancia desaparecio justo ahora (proceso cerrado) */ }
            }

            return Math.Clamp(total, 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    private ulong SampleVramUsed()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Adapter Memory")) return 0;

            var category = new PerformanceCounterCategory("GPU Adapter Memory");
            var instanceNames = category.GetInstanceNames().ToHashSet();

            foreach (var stale in _gpuMemCounters.Keys.Except(instanceNames).ToList())
            {
                _gpuMemCounters[stale].Dispose();
                _gpuMemCounters.Remove(stale);
            }

            foreach (var name in instanceNames)
            {
                if (!_gpuMemCounters.ContainsKey(name))
                {
                    _gpuMemCounters[name] = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", name, true);
                }
            }

            ulong total = 0;
            foreach (var counter in _gpuMemCounters.Values)
            {
                try { total += (ulong)counter.NextValue(); }
                catch { /* instancia desaparecida */ }
            }

            return total;
        }
        catch
        {
            return 0;
        }
    }

    // ===================== Disco (capacidad + I/O por unidad) =====================

    private Dictionary<string, (double Read, double Write)> SampleDiskIOPerDrive()
    {
        var result = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!PerformanceCounterCategory.Exists("LogicalDisk")) return result;

            var category = new PerformanceCounterCategory("LogicalDisk");
            var instanceNames = category.GetInstanceNames()
                .Where(n => n != "_Total")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var stale in _diskIoCounters.Keys.Except(instanceNames, StringComparer.OrdinalIgnoreCase).ToList())
            {
                _diskIoCounters[stale].Read.Dispose();
                _diskIoCounters[stale].Write.Dispose();
                _diskIoCounters.Remove(stale);
            }

            foreach (var name in instanceNames)
            {
                if (!_diskIoCounters.ContainsKey(name))
                {
                    _diskIoCounters[name] = (
                        new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", name, true),
                        new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", name, true));
                }
            }

            foreach (var (name, counters) in _diskIoCounters)
            {
                try
                {
                    result[name] = (counters.Read.NextValue(), counters.Write.NextValue());
                }
                catch
                {
                    // instancia desaparecida justo ahora (unidad expulsada, etc.)
                }
            }
        }
        catch
        {
            // categoria no disponible en este sistema
        }

        return result;
    }

    private static List<DriveStat> SampleDrives(Dictionary<string, (double Read, double Write)> ioByDrive)
    {
        var list = new List<DriveStat>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;

            try
            {
                var totalGb = drive.TotalSize / 1024d / 1024 / 1024;
                var freeGb = drive.TotalFreeSpace / 1024d / 1024 / 1024;
                var usedGb = totalGb - freeGb;
                var percent = totalGb > 0 ? (usedGb / totalGb) * 100.0 : 0;
                var name = drive.Name.TrimEnd('\\');

                var io = ioByDrive.TryGetValue(name, out var val) ? val : (Read: 0, Write: 0);

                list.Add(new DriveStat
                {
                    Name = name,
                    Label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Disco local" : drive.VolumeLabel,
                    UsedGB = usedGb,
                    TotalGB = totalGb,
                    UsedPercent = percent,
                    ReadBytesPerSec = io.Read,
                    WriteBytesPerSec = io.Write
                });
            }
            catch
            {
                // Unidad no lista o error de acceso puntual: se omite en este ciclo.
            }
        }

        return list.OrderBy(d => d.Name).ToList();
    }

    // ===================== Red =====================

    private (double down, double up) SampleNetwork()
    {
        long received = 0, sent = 0;

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var stats = nic.GetIPv4Statistics();
            received += stats.BytesReceived;
            sent += stats.BytesSent;
        }

        var now = DateTime.UtcNow;
        double downRate = 0, upRate = 0;

        if (_prevNetSampleTime != DateTime.MinValue)
        {
            var elapsed = (now - _prevNetSampleTime).TotalSeconds;
            if (elapsed > 0)
            {
                downRate = Math.Max(0, (received - _prevBytesReceived) / elapsed);
                upRate = Math.Max(0, (sent - _prevBytesSent) / elapsed);
            }
        }

        _prevBytesReceived = received;
        _prevBytesSent = sent;
        _prevNetSampleTime = now;

        return (downRate, upRate);
    }

    // ===================== Procesos =====================

    private List<ProcessLoadInfo> SampleTopProcesses()
    {
        var topProcesses = IncludeTopProcesses ? SampleTopProcesses() : new List<ProcessLoadInfo>();
        var now = DateTime.UtcNow;
        var results = new List<ProcessLoadInfo>();
        var seenPids = new HashSet<int>();

        foreach (var proc in Process.GetProcesses())
        {
            using (proc)
            {
                try
                {
                    if (proc.Id <= 4) continue;

                    seenPids.Add(proc.Id);
                    var cpuTime = proc.TotalProcessorTime;
                    var ramBytes = (ulong)proc.WorkingSet64;

                    double cpuPercent = 0;
                    if (_prevProcessTimes.TryGetValue(proc.Id, out var prev))
                    {
                        var elapsedMs = (now - prev.SampleTime).TotalMilliseconds;
                        if (elapsedMs > 0)
                        {
                            var cpuDeltaMs = (cpuTime - prev.CpuTime).TotalMilliseconds;
                            cpuPercent = Math.Clamp(
                                cpuDeltaMs / (elapsedMs * Environment.ProcessorCount) * 100.0, 0, 100);
                        }
                    }

                    _prevProcessTimes[proc.Id] = (cpuTime, now);

                    results.Add(new ProcessLoadInfo
                    {
                        Pid = proc.Id,
                        Name = proc.ProcessName,
                        CpuPercent = cpuPercent,
                        RamBytes = ramBytes
                    });
                }
                catch
                {
                    // proceso del sistema sin permisos de acceso, o que ha terminado justo ahora.
                }
            }
        }

        foreach (var stalePid in _prevProcessTimes.Keys.Except(seenPids).ToList())
        {
            _prevProcessTimes.Remove(stalePid);
        }

        return results
            .OrderByDescending(p => p.CpuPercent)
            .ThenByDescending(p => p.RamBytes)
            .Take(5)
            .ToList();
    }

    public void Dispose()
    {
        foreach (var c in _gpuEngineCounters.Values) c.Dispose();
        foreach (var c in _gpuMemCounters.Values) c.Dispose();
        foreach (var (read, write) in _diskIoCounters.Values) { read.Dispose(); write.Dispose(); }
    }

}