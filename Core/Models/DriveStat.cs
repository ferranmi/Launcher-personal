namespace Launcher.Core.Models;

/// <summary>Instantanea de uso y actividad de una unidad de disco fija en un momento dado.</summary>
public class DriveStat
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public double UsedGB { get; set; }
    public double TotalGB { get; set; }
    public double UsedPercent { get; set; }

    public double ReadBytesPerSec { get; set; }
    public double WriteBytesPerSec { get; set; }
}