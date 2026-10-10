namespace Launcher.Core.Models;

/// <summary>Foto completa del estado del sistema en un tick del monitor.</summary>
public class SystemSnapshot
{
    public double CpuPercent { get; set; }

    public double RamPercent { get; set; }
    public ulong RamUsedBytes { get; set; }
    public ulong RamTotalBytes { get; set; }

    public double GpuPercent { get; set; }
    public string GpuName { get; set; } = "GPU";

    public double VramPercent { get; set; }
    public ulong VramUsedBytes { get; set; }
    public ulong VramTotalBytes { get; set; }

    public double NetDownBytesPerSec { get; set; }
    public double NetUpBytesPerSec { get; set; }

    public List<DriveStat> Drives { get; set; } = new();

    public TimeSpan Uptime { get; set; }

    public List<ProcessLoadInfo> TopProcesses { get; set; } = new();
}