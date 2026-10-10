using Launcher.Core.Models;

namespace Launcher.Core.Interfaces;

/// <summary>
/// Fuente externa de juegos (Steam, Epic...). Cada proveedor sabe detectar los
/// juegos instalados de su plataforma; GameLibraryService fusiona el resultado
/// con la biblioteca local sin pisar ediciones manuales del usuario.
/// </summary>
public interface IGameProvider
{
    GameSource Source { get; }

    /// <summary>Detecta los juegos instalados de esta plataforma. No debe lanzar
    /// si la plataforma no esta instalada: en ese caso, lista vacia.</summary>
    Task<List<Game>> DetectGamesAsync();
}
