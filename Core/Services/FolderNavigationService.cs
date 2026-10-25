namespace Launcher.Core.Services;

/// <summary>
/// Punto unico para pedir "abre esta carpeta en el panel lateral del modulo Carpetas".
/// Lo usan el buscador, el asistente o cualquier otro modulo:
///     FolderNavigationService.Request(@"C:\Users\yo\Documents");
/// MainWindow escucha NavigateRequested para cambiar de modulo; FoldersView
/// consume la ruta pendiente (tanto si ya existia como si se crea ahora).
/// </summary>
public static class FolderNavigationService
{
    /// <summary>Ruta pendiente de abrir. La consume FoldersView con TakePending().</summary>
    private static string? _pending;

    /// <summary>Lo escucha MainWindow para mostrar el modulo "folders".</summary>
    public static event Action? NavigateRequested;

    /// <summary>Lo escucha FoldersView (si ya esta creada) para abrir la ruta.</summary>
    public static event Action? PathRequested;

    public static void Request(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        // Acepta carpeta o archivo: con un archivo, el panel abre su carpeta y lo selecciona.
        if (!System.IO.Directory.Exists(path) && !System.IO.File.Exists(path)) return;

        _pending = path;
        NavigateRequested?.Invoke();
        PathRequested?.Invoke();
    }

    public static string? TakePending()
    {
        var path = _pending;
        _pending = null;
        return path;
    }
}
