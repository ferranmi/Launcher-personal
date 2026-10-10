using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Launcher.Core.Models;

namespace Launcher.Core.Services;

/// <summary>
/// Fuente de verdad de los accesos directos a carpetas. Persiste en JSON en
/// %AppData%\Launcher\folders.json. La primera vez que se ejecuta (no existe el
/// archivo), siembra las carpetas de usuario habituales de Windows (Escritorio,
/// Documentos, Descargas, Imagenes, Musica, Videos) usando las rutas especiales
/// del propio sistema, para que el modulo no arranque vacio.
/// </summary>
public class FolderShortcutsService
{
    private static readonly string AppFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Launcher");

    private static readonly string LibraryPath = Path.Combine(AppFolder, "folders.json");

    public ObservableCollection<FolderShortcut> Folders { get; } = new();

    public FolderShortcutsService()
    {
        Directory.CreateDirectory(AppFolder);
        Load();
    }

    private void Load()
    {
        Folders.Clear();

        if (!File.Exists(LibraryPath))
        {
            SeedDefaults();
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(LibraryPath);
            var items = JsonSerializer.Deserialize<List<FolderShortcut>>(json) ?? new List<FolderShortcut>();
            foreach (var f in items) Folders.Add(f);
        }
        catch
        {
            // folders.json corrupto o ilegible: arrancamos vacios en vez de reventar la app.
        }
    }

    private void SeedDefaults()
    {
        AddDefault("Escritorio", Environment.SpecialFolder.DesktopDirectory, "\uE7F4");
        AddDefault("Documentos", Environment.SpecialFolder.MyDocuments, "\uE8A5");
        AddDefault("Descargas", GetDownloadsPath(), "\uE896");
        AddDefault("Imagenes", Environment.SpecialFolder.MyPictures, "\uEB9F");
        AddDefault("Musica", Environment.SpecialFolder.MyMusic, "\uEC4F");
        AddDefault("Videos", Environment.SpecialFolder.MyVideos, "\uE714");
    }

    private void AddDefault(string name, Environment.SpecialFolder special, string icon) =>
        AddDefault(name, Environment.GetFolderPath(special), icon);

    private void AddDefault(string name, string? path, string icon)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        Folders.Add(new FolderShortcut
        {
            Name = name,
            Path = path,
            Icon = icon,
            IsBuiltIn = true
        });
    }

    /// <summary>La carpeta de Descargas no tiene un Environment.SpecialFolder propio
    /// en .NET; se resuelve como subcarpeta del perfil de usuario (ruta estandar en
    /// Windows desde Vista).</summary>
    private static string? GetDownloadsPath()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile)) return null;

        var downloads = Path.Combine(profile, "Downloads");
        return Directory.Exists(downloads) ? downloads : null;
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(Folders.ToList(), options));
    }

    public void Add(FolderShortcut folder)
    {
        Folders.Add(folder);
        Save();
    }

    public void Update(FolderShortcut _) => Save();

    public void Remove(FolderShortcut folder)
    {
        // Las carpetas del sistema no se eliminan (solo se pueden ocultar desde la
        // UI); esto es una salvaguarda por si algo las manda aqui por error.
        if (folder.IsBuiltIn) return;

        Folders.Remove(folder);
        Save();
    }

    /// <summary>Abre la carpeta en el Explorador de Windows. No lanza excepcion si
    /// ya no existe (carpeta borrada/movida despues de anadirla).</summary>
    public void Open(FolderShortcut folder)
    {
        if (!folder.Exists)
        {
            System.Diagnostics.Debug.WriteLine($"[FolderShortcutsService] Carpeta no encontrada para '{folder.Name}': {folder.Path}");
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = folder.Path,
            UseShellExecute = true
        });
    }
}