using System.IO;
using System.Text.Json;
using Launcher.Core.Interfaces;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.Games.Providers;

/// <summary>
/// Detecta juegos de Epic Games leyendo los manifiestos JSON que el launcher de
/// Epic deja en disco (uno por juego instalado, extension .item aunque el
/// contenido es JSON normal). No hace falta login.
///
/// El lanzamiento usa el protocolo com.epicgames.launcher://apps/{AppName}?action=launch,
/// igual que hacen otros launchers de terceros (evita tener que lidiar con DRM/online
/// fix de Epic al abrir el .exe directamente).
///
/// NOTA: igual que con Steam, esta logica sigue el formato documentado de los
/// manifiestos de Epic pero no se ha podido verificar contra una instalacion real.
/// Si algun campo no aparece como se espera, pega el contenido de un .item y lo
/// ajustamos.
/// </summary>
public class EpicGameProvider : IGameProvider
{
    public GameSource Source => GameSource.Epic;

    public async Task<List<Game>> DetectGamesAsync()
    {
        var results = new List<Game>();

        var manifestsPath = LibraryPathsService.Instance.EpicManifestsPath;
        if (string.IsNullOrWhiteSpace(manifestsPath) || !Directory.Exists(manifestsPath))
        {
            return results; // sin carpeta configurada: GamesView pedira una antes de llamar aqui
        }

        var coverService = new CoverArtService();

        foreach (var itemFile in Directory.GetFiles(manifestsPath, "*.item"))
        {
            Game? game;
            try
            {
                game = await ParseManifestAsync(itemFile, coverService);
            }
            catch
            {
                continue; // un manifiesto corrupto no debe tumbar el resto de la deteccion
            }

            if (game != null) results.Add(game);
        }

        return results;
    }

    private static async Task<Game?> ParseManifestAsync(string itemFile, CoverArtService coverService)
    {
        var json = await File.ReadAllTextAsync(itemFile);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var displayName = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : null;
        var appName = root.TryGetProperty("AppName", out var an) ? an.GetString() : null;

        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(appName))
        {
            return null;
        }

        var coverPath = await coverService.SearchAndDownloadAsync(displayName, $"epic_{appName}");

        return new Game
        {
            Name = displayName,
            ExecutablePath = $"com.epicgames.launcher://apps/{appName}?action=launch&silent=true",
            Arguments = "",
            Source = GameSource.Epic,
            ExternalId = appName,
            CoverImagePath = coverPath
        };
    }
}
