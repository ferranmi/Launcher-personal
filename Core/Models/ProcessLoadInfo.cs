namespace Launcher.Core.Models;

/// <summary>Instantanea de carga de un proceso individual en un tick del monitor.</summary>
public class ProcessLoadInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public double CpuPercent { get; set; }
    public ulong RamBytes { get; set; }
}