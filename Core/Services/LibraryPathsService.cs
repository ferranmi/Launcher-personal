using System.IO;
using System.Text.Json;

namespace Launcher.Core.Services;

/// <summary>
/// Guarda en disco las carpetas que el usuario ha indicado para Steam y Epic
/// (en vez de depender de login/OAuth con cada plataforma). Intenta auto-detectar
/// como primera sugerencia, pero el usuario siempre puede corregirlo a mano.
/// </summary>
public class LibraryPathsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Launcher", "settings.json");

    private static LibraryPathsService? _instance;
    public static LibraryPathsService Instance => _instance ??= Load();

    public string? SteamPath { get; set; }
    public string? EpicManifestsPath { get; set; }

    /// <summary>Clave de la Web API de Steam (necesaria para ver los logros) y SteamID64.</summary>
    public string? SteamApiKey { get; set; }
    public string? SteamId { get; set; }
    public bool HasSteamApiKey => !string.IsNullOrWhiteSpace(SteamApiKey);

    public bool HasSteamPath => !string.IsNullOrWhiteSpace(SteamPath) && Directory.Exists(SteamPath);
    public bool HasEpicPath => !string.IsNullOrWhiteSpace(EpicManifestsPath) && Directory.Exists(EpicManifestsPath);

    private static LibraryPathsService Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<LibraryPathsService>(json);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // settings.json corrupto: seguimos con valores vacios en vez de reventar.
        }

        return new LibraryPathsService();
    }

    public void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(folder);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // No es critico: si falla el guardado, en la siguiente sesion se
            // volvera a pedir la carpeta.
        }
    }

    /// <summary>Intenta localizar Steam leyendo el registro (instalacion estandar en Windows).</summary>
    public static string? AutoDetectSteamPath()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var path = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) return path;
        }
        catch
        {
            // sin acceso al registro o clave inexistente: seguimos con las rutas por defecto.
        }

        foreach (var candidate in new[]
                 {
                     @"C:\Program Files (x86)\Steam",
                     @"C:\Program Files\Steam"
                 })
        {
            if (Directory.Exists(candidate)) return candidate;
        }

        return null;
    }

    /// <summary>Intenta localizar la carpeta de manifiestos de Epic (ruta estandar de instalacion).</summary>
    public static string? AutoDetectEpicManifestsPath()
    {
        var candidate = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");

        return Directory.Exists(candidate) ? candidate : null;
    }
}
