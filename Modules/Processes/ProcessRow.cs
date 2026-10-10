using System.Windows.Media;

namespace Launcher.Modules.Processes;

/// <summary>
/// Fila mostrada en el DataGrid: puede ser un grupo (proceso padre, con valores
/// agregados de todos sus miembros) o una fila hija individual dentro de un grupo
/// expandido. Se reconstruye entera en cada refresco/orden/busqueda, asi que no
/// necesita INotifyPropertyChanged.
/// </summary>
public class ProcessRow
{
    public required string Name { get; init; }
    public ImageSource? Icon { get; init; }
    public int Pid { get; init; }

    public double CpuPercent { get; init; }
    public double RamMB { get; init; }
    public double RamPercent { get; init; }
    public double GpuPercent { get; init; }
    public double VramMB { get; init; }
    public double VramPercent { get; init; }
    public bool IsFrozen { get; init; }

    public int Depth { get; init; }
    public bool IsGroup { get; init; }
    public bool HasChildren { get; init; }
    public bool IsExpanded { get; init; }
    public int MemberCount { get; init; }
    public string CountLabel => MemberCount > 1 ? $"\u00d7{MemberCount}" : "";

    /// <summary>ProcessGroup si es fila de grupo, o ProcessSample si es fila individual/hija.
    /// Usado por los manejadores de expandir/finalizar para saber sobre que actuar.</summary>
    public required object Tag { get; init; }

    public bool IsCpuHigh => CpuPercent >= 80.0;
    public bool IsRamHigh => RamPercent >= 80.0;
    public bool IsGpuHigh => GpuPercent >= 80.0;
    public bool IsVramHigh => VramPercent >= 80.0;
}