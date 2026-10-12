using System.Windows.Media;
using Launcher.Core.Models;

namespace Launcher.Modules.Dashboard;

/// <summary>Envoltorio de solo presentacion para una tarjeta de juego: calcula la inicial que
/// se muestra cuando el juego no tiene portada, sin depender de que el modelo la ofrezca.</summary>
public class GameTile
{
    public required Game Game { get; init; }

    public string Name => Game.Name;
    public string? CoverImagePath => Game.CoverImagePath;
    public bool HasCover => Game.HasCover;

    public string Initial =>
        string.IsNullOrWhiteSpace(Game.Name) ? "?" : Game.Name.Trim()[..1].ToUpperInvariant();
}

/// <summary>Fila de la tabla "Procesos" del dashboard (un grupo de procesos del mismo
/// ejecutable). IsCpu/IsRam/IsGpu marcan la columna por la que esta ordenada la lista.</summary>
public class TopProcessRow
{
    public ImageSource? Icon { get; init; }
    public string Name { get; init; } = "";
    public string CountLabel { get; init; } = "";

    public string CpuText { get; init; } = "";
    public string RamText { get; init; } = "";
    public string GpuText { get; init; } = "";

    public bool IsCpu { get; init; }
    public bool IsRam { get; init; }
    public bool IsGpu { get; init; }
}
