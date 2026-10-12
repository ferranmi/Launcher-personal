using System.IO;
using System.Text.Json.Serialization;

namespace Launcher.Core.Models;

/// <summary>
/// Ficha de un juego: lo descargado de internet (Fetched), lo que el usuario ha editado
/// encima (Overrides), los logros en cache, y sus enlaces, documentos y notas. Se guarda en
/// %AppData%\Launcher\game-info\{Game.Id}.json, un archivo por juego.
///
/// Lo descargado y lo editado se guardan por separado: al actualizar la informacion se
/// reemplaza Fetched, pero Overrides (lo que escribio el usuario) se conserva.
/// </summary>
public class GameInfo
{
    public string GameId { get; set; } = "";

    /// <summary>Id del juego en Steam: el del propio juego si es de Steam, el que se encontro
    /// por nombre, o el que el usuario haya escrito a mano.</summary>
    public int? SteamAppId { get; set; }

    public FetchedGameInfo Fetched { get; set; } = new();
    public GameInfoOverrides Overrides { get; set; } = new();

    public List<AchievementInfo> Achievements { get; set; } = new();
    public DateTime? AchievementsFetchedAt { get; set; }

    /// <summary>False si se obtuvo la lista de logros pero no el progreso del jugador
    /// (perfil privado, juego que no es de la cuenta...).</summary>
    public bool AchievementsHavePlayerData { get; set; }

    public List<GameLink> Links { get; set; } = new();
    public List<GameDocument> Documents { get; set; } = new();
    public string Notes { get; set; } = "";
}

/// <summary>Datos descargados (Steam Store). Texto ya listo para mostrar.</summary>
public class FetchedGameInfo
{
    public DateTime? FetchedAt { get; set; }
    public string? Description { get; set; }
    public string? Genres { get; set; }
    public string? Developers { get; set; }
    public string? Publishers { get; set; }
    public string? ReleaseDate { get; set; }
    public int? MetacriticScore { get; set; }
    public string? HeaderImageUrl { get; set; }
    public string? StoreUrl { get; set; }
    public List<ScreenshotInfo> Screenshots { get; set; } = new();
}

/// <summary>Valores escritos por el usuario. null = sin cambios (se muestra lo descargado).</summary>
public class GameInfoOverrides
{
    public string? Description { get; set; }
    public string? Genres { get; set; }
    public string? Developers { get; set; }
    public string? Publishers { get; set; }
    public string? ReleaseDate { get; set; }
}

public class ScreenshotInfo
{
    public string ThumbUrl { get; set; } = "";
    public string FullUrl { get; set; } = "";
}

public class AchievementInfo
{
    public string ApiName { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool Hidden { get; set; }
    public string? IconUrl { get; set; }
    public string? IconGrayUrl { get; set; }
    public bool Achieved { get; set; }
    public DateTime? UnlockTime { get; set; }

    /// <summary>Porcentaje de jugadores que lo tienen desbloqueado (global).</summary>
    public double? GlobalPercent { get; set; }

    /// <summary>Solo para la vista: true cuando no se pudo leer el progreso del jugador, para
    /// no mostrar todos los logros como bloqueados.</summary>
    [JsonIgnore]
    public bool ProgressUnknown { get; set; }

    [JsonIgnore]
    public string? ShownIcon => Achieved ? IconUrl : (IconGrayUrl ?? IconUrl);

    [JsonIgnore]
    public string StatusText
    {
        get
        {
            if (ProgressUnknown) return "";
            if (!Achieved) return "Bloqueado";
            return UnlockTime is { } time ? $"Desbloqueado el {time:dd/MM/yyyy}" : "Desbloqueado";
        }
    }

    [JsonIgnore]
    public string PercentText => GlobalPercent is { } percent ? $"{percent:0.#} % de los jugadores" : "";

    [JsonIgnore]
    public string DescriptionText =>
        string.IsNullOrWhiteSpace(Description) ? (Hidden ? "Logro oculto" : "") : Description;

    [JsonIgnore]
    public double RowOpacity => Achieved || ProgressUnknown ? 1.0 : 0.6;
}

/// <summary>Enlace web de la ficha. Se abre con el navegador elegido, y en ventana privada
/// si asi se pidio (mismos argumentos que usa Comandos).</summary>
public class GameLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";

    /// <summary>"" = navegador predeterminado; si no, clave del navegador (chrome, msedge, firefox...).</summary>
    public string Browser { get; set; } = "";

    public bool Incognito { get; set; }

    [JsonIgnore]
    public string BrowserLabel
    {
        get
        {
            var name = (Browser ?? "") switch
            {
                "" => "Predeterminado",
                "chrome" => "Chrome",
                "msedge" => "Edge",
                "firefox" => "Firefox",
                "brave" => "Brave",
                "opera" => "Opera",
                "vivaldi" => "Vivaldi",
                var other => other
            };

            return Incognito ? $"{name} \u00b7 incognito" : name;
        }
    }
}

/// <summary>Archivo o carpeta local asociado al juego (manual, guias, partidas guardadas, mods...).</summary>
public class GameDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";

    [JsonIgnore]
    public bool Exists => File.Exists(Path) || Directory.Exists(Path);

    [JsonIgnore]
    public string Icon => Directory.Exists(Path) ? "\uE8B7" : "\uE8A5";
}
