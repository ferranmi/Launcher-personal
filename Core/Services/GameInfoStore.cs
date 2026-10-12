using System.IO;
using System.Text.Json;
using Launcher.Core.Models;

namespace Launcher.Core.Services;

/// <summary>
/// Lee y guarda la ficha de cada juego en %AppData%\Launcher\game-info\{Game.Id}.json.
/// Nunca lanza: si el archivo esta corrupto o no se puede escribir, lo deja en el registro
/// de errores y sigue (la ficha simplemente empieza vacia).
/// </summary>
public static class GameInfoStore
{
    public static readonly string Folder = Path.Combine(AppSettingsService.AppFolder, "game-info");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static GameInfo Load(string gameId)
    {
        var path = PathFor(gameId);

        try
        {
            if (File.Exists(path))
            {
                var info = JsonSerializer.Deserialize<GameInfo>(File.ReadAllText(path), Options);
                if (info != null)
                {
                    info.GameId = gameId;
                    info.Fetched ??= new FetchedGameInfo();
                    info.Overrides ??= new GameInfoOverrides();
                    info.Achievements ??= new List<AchievementInfo>();
                    info.Links ??= new List<GameLink>();
                    info.Documents ??= new List<GameDocument>();
                    info.Notes ??= "";
                    return info;
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo leer la ficha del juego {gameId}", ex);
        }

        return new GameInfo { GameId = gameId };
    }

    public static void Save(GameInfo info)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(PathFor(info.GameId), JsonSerializer.Serialize(info, Options));
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo guardar la ficha del juego {info.GameId}", ex);
        }
    }

    private static string PathFor(string gameId) => Path.Combine(Folder, $"{gameId}.json");
}
