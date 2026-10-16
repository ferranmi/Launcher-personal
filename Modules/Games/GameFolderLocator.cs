using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.Games;

/// <summary>
/// Encuentra donde esta instalado un juego para poder abrir su carpeta:
/// - Juego con ejecutable local (manual): el propio .exe, que se abre seleccionado en el Explorador.
/// - Steam: lee installdir del appmanifest_{appid}.acf, buscando en todas las bibliotecas.
/// - Epic: lee InstallLocation del manifiesto .item cuyo AppName coincide.
/// Steam y Epic se lanzan por protocolo (steam://...), que no dice donde esta el juego: por eso
/// hace falta mirar sus manifiestos.
///
/// NOTA: sigue el formato conocido de esos archivos pero no se ha podido probar contra una
/// instalacion real. Si no encuentra la carpeta de un juego, pega su .acf o .item y se ajusta.
/// </summary>
public static class GameFolderLocator
{
    public readonly record struct Location(string Path, bool IsFile);

    private static readonly Regex LibraryPathRegex = new("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
    private static readonly Regex InstallDirRegex = new("\"installdir\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);

    public static Location? Find(Game game)
    {
        try
        {
            var target = game.ExecutablePath;

            if (!string.IsNullOrWhiteSpace(target) && !target.Contains("://", StringComparison.Ordinal))
            {
                return File.Exists(target) ? new Location(target, true) : null;
            }

            return game.Source switch
            {
                GameSource.Steam => FindSteamFolder(game.ExternalId),
                GameSource.Epic => FindEpicFolder(game.ExternalId),
                _ => null
            };
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo localizar la carpeta de '{game.Name}'", ex);
            return null;
        }
    }

    /// <summary>Abre el Explorador: con el .exe seleccionado si es un archivo, o dentro de la
    /// carpeta si es un directorio.</summary>
    public static bool TryOpen(Location location, out string? error)
    {
        error = null;

        try
        {
            if (location.IsFile)
            {
                Process.Start("explorer.exe", $"/select,\"{location.Path}\"");
            }
            else
            {
                Process.Start(new ProcessStartInfo { FileName = location.Path, UseShellExecute = true });
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static Location? FindSteamFolder(string? appId)
    {
        var steamPath = LibraryPathsService.Instance.SteamPath;
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
        {
            return null;
        }

        var steamAppsFolders = new List<string> { Path.Combine(steamPath, "steamapps") };

        var libraryVdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryVdf))
        {
            foreach (Match match in LibraryPathRegex.Matches(File.ReadAllText(libraryVdf)))
            {
                var rawPath = match.Groups[1].Value.Replace(@"\\", @"\");
                var candidate = Path.Combine(rawPath, "steamapps");
                if (Directory.Exists(candidate) && !steamAppsFolders.Contains(candidate)) steamAppsFolders.Add(candidate);
            }
        }

        foreach (var steamApps in steamAppsFolders)
        {
            var manifest = Path.Combine(steamApps, $"appmanifest_{appId}.acf");
            if (!File.Exists(manifest)) continue;

            var installDir = InstallDirRegex.Match(File.ReadAllText(manifest));
            if (!installDir.Success) continue;

            var folder = Path.Combine(steamApps, "common", installDir.Groups[1].Value);
            if (Directory.Exists(folder)) return new Location(folder, false);
        }

        return null;
    }

    private static Location? FindEpicFolder(string? appName)
    {
        var manifestsPath = LibraryPathsService.Instance.EpicManifestsPath;
        if (string.IsNullOrWhiteSpace(appName) || string.IsNullOrWhiteSpace(manifestsPath) || !Directory.Exists(manifestsPath))
        {
            return null;
        }

        foreach (var itemFile in Directory.GetFiles(manifestsPath, "*.item"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(itemFile));
                var root = doc.RootElement;

                var name = root.TryGetProperty("AppName", out var an) ? an.GetString() : null;
                if (!string.Equals(name, appName, StringComparison.OrdinalIgnoreCase)) continue;

                var location = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null;
                if (!string.IsNullOrWhiteSpace(location) && Directory.Exists(location)) return new Location(location, false);
            }
            catch
            {
                // un manifiesto corrupto no debe impedir mirar los demas.
            }
        }

        return null;
    }
}
