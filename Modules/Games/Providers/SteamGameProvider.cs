using System.IO;
using System.Text.RegularExpressions;
using Launcher.Core.Interfaces;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.Games.Providers;

/// <summary>
/// Detecta juegos de Steam leyendo los archivos locales de la instalacion
/// (nunca hace falta login): steamapps/libraryfolders.vdf para encontrar todas
/// las bibliotecas del usuario (puede tener varios discos), y dentro de cada una
/// los appmanifest_&lt;appid&gt;.acf, que traen nombre y appid de cada juego instalado.
///
/// El lanzamiento usa el protocolo steam://rungameid/{appid} en vez de intentar
/// adivinar la ruta del .exe (Steam no la guarda en el .acf de forma fiable, y el
/// protocolo ademas deja que Steam gestione overlay/DRM/actualizaciones).
///
/// NOTA: esta logica sigue el formato conocido de esos archivos pero no se ha
/// podido probar contra una instalacion real de Steam. Si algo no detecta bien,
/// probablemente sea el formato de libraryfolders.vdf (Valve lo ha cambiado
/// alguna vez entre versiones de Steam) — dime el error o pega el contenido del
/// archivo y lo ajustamos.
/// </summary>
public class SteamGameProvider : IGameProvider
{
    private static readonly Regex AppIdRegex = new("\"appid\"\\s*\"(\\d+)\"", RegexOptions.IgnoreCase);
    private static readonly Regex NameRegex = new("\"name\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
    private static readonly Regex LibraryPathRegex = new("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);

    public GameSource Source => GameSource.Steam;

    public async Task<List<Game>> DetectGamesAsync()
    {
        var results = new List<Game>();

        var steamPath = LibraryPathsService.Instance.SteamPath;
        if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
        {
            return results; // sin carpeta configurada: GamesView pedira una antes de llamar aqui
        }

        var coverService = new CoverArtService();

        foreach (var steamAppsFolder in FindSteamAppsFolders(steamPath))
        {
            foreach (var acfFile in Directory.GetFiles(steamAppsFolder, "appmanifest_*.acf"))
            {
                Game? game;
                try
                {
                    game = await ParseManifestAsync(acfFile, coverService);
                }
                catch
                {
                    continue; // un manifiesto corrupto no debe tumbar el resto de la deteccion
                }

                if (game != null) results.Add(game);
            }
        }

        return results;
    }

    /// <summary>La carpeta steamapps principal, mas todas las bibliotecas adicionales
    /// listadas en libraryfolders.vdf (juegos instalados en otros discos).</summary>
    private static List<string> FindSteamAppsFolders(string steamPath)
    {
        var folders = new List<string> { Path.Combine(steamPath, "steamapps") };

        var libraryVdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryVdf)) return folders;

        var text = File.ReadAllText(libraryVdf);
        foreach (Match match in LibraryPathRegex.Matches(text))
        {
            var rawPath = match.Groups[1].Value.Replace(@"\\", @"\");
            var candidate = Path.Combine(rawPath, "steamapps");
            if (Directory.Exists(candidate) && !folders.Contains(candidate))
            {
                folders.Add(candidate);
            }
        }

        return folders;
    }

    private static async Task<Game?> ParseManifestAsync(string acfFile, CoverArtService coverService)
    {
        var text = File.ReadAllText(acfFile);

        var appId = AppIdRegex.Match(text).Groups[1].Value;
        var name = NameRegex.Match(text).Groups[1].Value;

        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var coverPath = await coverService.DownloadByAppIdAsync(int.Parse(appId), $"steam_{appId}");

        return new Game
        {
            Name = name,
            ExecutablePath = $"steam://rungameid/{appId}",
            Arguments = "",
            Source = GameSource.Steam,
            ExternalId = appId,
            CoverImagePath = coverPath
        };
    }
}
