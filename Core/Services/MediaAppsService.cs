using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Launcher.Core.Models;

namespace Launcher.Core.Services;

/// <summary>
/// Fuente de verdad de los accesos a apps multimedia. Persiste en JSON en
/// %AppData%\Launcher\media_apps.json, igual patron que GameLibraryService y
/// FolderShortcutsService pero sin sincronizacion automatica (aqui todo es
/// manual: no hay un "Steam de apps multimedia" del que detectar instalaciones).
/// </summary>
public class MediaAppsService
{
    private static readonly string AppFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Launcher");

    private static readonly string LibraryPath = Path.Combine(AppFolder, "media_apps.json");

    public ObservableCollection<MediaAppShortcut> Apps { get; } = new();

    public MediaAppsService()
    {
        Directory.CreateDirectory(AppFolder);
        Load();
    }

    private void Load()
    {
        Apps.Clear();
        if (!File.Exists(LibraryPath)) return;

        try
        {
            var json = File.ReadAllText(LibraryPath);
            var items = JsonSerializer.Deserialize<List<MediaAppShortcut>>(json) ?? new List<MediaAppShortcut>();
            foreach (var a in items) Apps.Add(a);
        }
        catch
        {
            // media_apps.json corrupto o ilegible: arrancamos vacios en vez de reventar la app.
        }
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(Apps.ToList(), options));
    }

    public void Add(MediaAppShortcut app)
    {
        Apps.Add(app);
        Save();
    }

    public void Update(MediaAppShortcut _) => Save();

    public void Remove(MediaAppShortcut app)
    {
        Apps.Remove(app);
        Save();
    }

    /// <summary>Lanza la app. No lanza excepcion si la ruta ya no es valida.</summary>
    public void Launch(MediaAppShortcut app)
    {
        var target = app.ExecutablePath;
        var isProtocol = target.Contains("://", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(target) || (!isProtocol && !File.Exists(target)))
        {
            System.Diagnostics.Debug.WriteLine($"[MediaAppsService] Ruta no valida para '{app.Name}': {target}");
            return;
        }

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        };

        if (!isProtocol)
        {
            startInfo.Arguments = app.Arguments;
            startInfo.WorkingDirectory = Path.GetDirectoryName(target);
        }

        System.Diagnostics.Process.Start(startInfo);
    }
}
