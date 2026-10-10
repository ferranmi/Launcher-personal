using System.Linq;

namespace Launcher.Modules.Processes;

/// <summary>
/// Agrupa un proceso "raiz" con sus procesos hijos del mismo ejecutable (p.ej. el
/// proceso principal de Discord y sus procesos auxiliares de renderizado/GPU).
/// Si no tiene hijos, se comporta como una fila normal sin flecha de expandir.
/// </summary>
public class ProcessGroup
{
    public ProcessSample Root { get; }
    public List<ProcessSample> Children { get; } = new();

    public ProcessGroup(ProcessSample root) => Root = root;

    public bool HasChildren => Children.Count > 0;
    public int MemberCount => Children.Count + 1;

    /// <summary>Todos los miembros del grupo (raiz + hijos), usado al finalizar la tarea completa.</summary>
    public IEnumerable<ProcessSample> AllMembers => new[] { Root }.Concat(Children);

    public double TotalCpu => Root.CpuPercent + Children.Sum(c => c.CpuPercent);
    public double TotalRamMB => Root.RamMB + Children.Sum(c => c.RamMB);
    public double TotalRamPercent => Root.RamPercent + Children.Sum(c => c.RamPercent);
    public double TotalGpu => Root.GpuPercent + Children.Sum(c => c.GpuPercent);
    public double TotalVramMB => Root.VramMB + Children.Sum(c => c.VramMB);
    public double TotalVramPercent => Root.VramPercent + Children.Sum(c => c.VramPercent);
    public bool AnyFrozen => Root.IsFrozen || Children.Any(c => c.IsFrozen);
}