namespace Launcher.Core.Models;

/// <summary>Que consola debe alojar el comando al ejecutarlo. None = lanzamiento
/// directo (apps con su propia ventana). Cmd y PowerShell abren una consola nueva
/// con el comando dentro, necesario para herramientas CLI/TUI (p.ej. Kunai va por
/// PowerShell, no por cmd).</summary>
public enum TerminalHost
{
    None,
    Cmd,
    PowerShell
}