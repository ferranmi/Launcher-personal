using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Launcher.Core.Models;
using Microsoft.Win32;

namespace Launcher.Core.Services;

/// <summary>Resultado de pedir los logros de un juego. Items trae lo que se haya podido
/// obtener; Error significa que no hay nada que mostrar, y PlayerWarning que se obtuvo la
/// lista de logros pero no el progreso del jugador.</summary>
public sealed class AchievementsResult
{
    public List<AchievementInfo> Items { get; } = new();
    public string? Error { get; set; }
    public string? PlayerWarning { get; set; }
    public bool HasPlayerData { get; set; }
}

/// <summary>
/// Informacion de juegos desde Steam.
///
/// - Ficha (descripcion, generos, desarrollador, fecha, capturas...): API publica de la Steam
///   Store (storesearch + appdetails). No necesita clave. Funciona para cualquier juego que
///   exista en Steam, aunque no lo hayas comprado alli: tambien se usa por nombre para juegos
///   de Epic o anadidos a mano.
/// - Logros: ISteamUserStats de la Web API. El esquema (nombres, iconos) y el progreso del
///   jugador piden una clave de la Web API (gratuita) y que el perfil tenga los detalles de
///   juego en publico; los porcentajes globales no piden clave.
///
/// NOTA: esta logica sigue el formato documentado de esas APIs pero no se ha podido probar
/// contra Steam desde aqui. Si algun campo no sale como se espera, pega la respuesta y se ajusta.
/// </summary>
public static class SteamInfoService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    // ===================== Busqueda por nombre =====================

    /// <summary>AppId del juego buscando por nombre en la Steam Store. Prefiere una
    /// coincidencia exacta entre los primeros resultados; si no, el primero.</summary>
    public static async Task<int?> FindAppIdAsync(string gameName)
    {
        try
        {
            var url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(gameName)}&cc=es&l=spanish";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));

            if (!doc.RootElement.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array ||
                items.GetArrayLength() == 0)
            {
                return null;
            }

            int? first = null;
            foreach (var item in items.EnumerateArray().Take(5))
            {
                if (!item.TryGetProperty("id", out var idElement) || !idElement.TryGetInt32(out var id)) continue;

                first ??= id;

                var name = GetString(item, "name");
                if (string.Equals(name, gameName, StringComparison.OrdinalIgnoreCase)) return id;
            }

            return first;
        }
        catch
        {
            // Sin conexion o respuesta inesperada: simplemente no hay coincidencia.
            return null;
        }
    }

    // ===================== Ficha (Steam Store) =====================

    public static async Task<FetchedGameInfo?> FetchDetailsAsync(int appId)
    {
        try
        {
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=spanish&cc=es";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));

            if (!doc.RootElement.TryGetProperty(appId.ToString(CultureInfo.InvariantCulture), out var entry)) return null;
            if (!entry.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True) return null;
            if (!entry.TryGetProperty("data", out var data)) return null;

            var info = new FetchedGameInfo
            {
                FetchedAt = DateTime.Now,
                Description = Clean(GetString(data, "short_description")),
                Genres = JoinObjects(data, "genres", "description"),
                Developers = JoinStrings(data, "developers"),
                Publishers = JoinStrings(data, "publishers"),
                HeaderImageUrl = GetString(data, "header_image"),
                StoreUrl = $"https://store.steampowered.com/app/{appId}"
            };

            if (data.TryGetProperty("release_date", out var releaseDate))
            {
                info.ReleaseDate = GetString(releaseDate, "date");
            }

            if (data.TryGetProperty("metacritic", out var metacritic) &&
                metacritic.TryGetProperty("score", out var score) &&
                score.TryGetInt32(out var scoreValue))
            {
                info.MetacriticScore = scoreValue;
            }

            if (data.TryGetProperty("screenshots", out var shots) && shots.ValueKind == JsonValueKind.Array)
            {
                foreach (var shot in shots.EnumerateArray().Take(8))
                {
                    var thumb = GetString(shot, "path_thumbnail");
                    var full = GetString(shot, "path_full");
                    if (thumb == null) continue;

                    info.Screenshots.Add(new ScreenshotInfo { ThumbUrl = thumb, FullUrl = full ?? thumb });
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo obtener la ficha de Steam del juego {appId}", ex);
            return null;
        }
    }

    // ===================== Logros (Web API) =====================

    public static async Task<AchievementsResult> FetchAchievementsAsync(int appId, string apiKey, string steamId)
    {
        var result = new AchievementsResult();
        var key = Uri.EscapeDataString(apiKey);

        // 1) Esquema: nombre, descripcion e iconos de cada logro. Pide clave.
        try
        {
            var schemaUrl = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={key}&appid={appId}&l=spanish";
            using var response = await Http.GetAsync(schemaUrl);

            if (!response.IsSuccessStatusCode)
            {
                result.Error = response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
                    ? "Steam ha rechazado la clave. Comprueba que esta bien copiada (Configuracion > Cuenta de Steam)."
                    : $"Steam no ha devuelto los logros del juego (error {(int)response.StatusCode}).";
                return result;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            if (!doc.RootElement.TryGetProperty("game", out var game) ||
                !game.TryGetProperty("availableGameStats", out var stats) ||
                !stats.TryGetProperty("achievements", out var list) ||
                list.ValueKind != JsonValueKind.Array)
            {
                result.Error = "Este juego no tiene logros en Steam.";
                return result;
            }

            foreach (var achievement in list.EnumerateArray())
            {
                var apiName = GetString(achievement, "name");
                if (apiName == null) continue;

                result.Items.Add(new AchievementInfo
                {
                    ApiName = apiName,
                    Name = GetString(achievement, "displayName") ?? apiName,
                    Description = Clean(GetString(achievement, "description")),
                    Hidden = GetInt(achievement, "hidden") == 1,
                    IconUrl = GetString(achievement, "icon"),
                    IconGrayUrl = GetString(achievement, "icongray")
                });
            }

            if (result.Items.Count == 0)
            {
                result.Error = "Este juego no tiene logros en Steam.";
                return result;
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Fallo al pedir el esquema de logros del juego {appId}", ex);
            result.Error = "No se ha podido contactar con Steam. Comprueba tu conexion.";
            return result;
        }

        // 2) Porcentaje global de jugadores que lo tienen. No pide clave; si falla, se omite.
        try
        {
            var percentUrl = $"https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/?gameid={appId}";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(percentUrl));

            if (doc.RootElement.TryGetProperty("achievementpercentages", out var container) &&
                container.TryGetProperty("achievements", out var list) &&
                list.ValueKind == JsonValueKind.Array)
            {
                var percents = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in list.EnumerateArray())
                {
                    var name = GetString(entry, "name");
                    if (name == null || !entry.TryGetProperty("percent", out var percentElement)) continue;

                    double value;
                    if (percentElement.ValueKind == JsonValueKind.Number) value = percentElement.GetDouble();
                    else if (percentElement.ValueKind == JsonValueKind.String &&
                             double.TryParse(percentElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) value = parsed;
                    else continue;

                    percents[name] = value;
                }

                foreach (var item in result.Items)
                {
                    if (percents.TryGetValue(item.ApiName, out var percent)) item.GlobalPercent = percent;
                }
            }
        }
        catch
        {
            // Sin porcentajes: la lista sigue siendo util.
        }

        // 3) Progreso del jugador. Pide clave, SteamID y perfil con detalles de juego publicos.
        try
        {
            var playerUrl = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?key={key}&steamid={Uri.EscapeDataString(steamId)}&appid={appId}&l=spanish";
            using var response = await Http.GetAsync(playerUrl);

            if (!response.IsSuccessStatusCode)
            {
                result.PlayerWarning = response.StatusCode switch
                {
                    HttpStatusCode.Forbidden =>
                        "No se ha podido leer tu progreso: tu perfil de Steam o los detalles de juego estan en privado (Steam > Perfil > Editar perfil > Privacidad).",
                    HttpStatusCode.BadRequest =>
                        "No se ha podido leer tu progreso: este juego no esta en tu cuenta de Steam o no tiene estadisticas.",
                    _ => $"No se ha podido leer tu progreso (error {(int)response.StatusCode})."
                };
                return result;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            if (doc.RootElement.TryGetProperty("playerstats", out var playerStats) &&
                playerStats.TryGetProperty("achievements", out var list) &&
                list.ValueKind == JsonValueKind.Array)
            {
                var byName = new Dictionary<string, AchievementInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in result.Items) byName.TryAdd(item.ApiName, item);

                foreach (var entry in list.EnumerateArray())
                {
                    var apiName = GetString(entry, "apiname");
                    if (apiName == null || !byName.TryGetValue(apiName, out var item)) continue;

                    item.Achieved = GetInt(entry, "achieved") == 1;

                    var unlock = GetLong(entry, "unlocktime");
                    if (item.Achieved && unlock > 0)
                    {
                        item.UnlockTime = DateTimeOffset.FromUnixTimeSeconds(unlock).LocalDateTime;
                    }
                }

                result.HasPlayerData = true;
            }
            else
            {
                result.PlayerWarning = "Steam no ha devuelto tu progreso en este juego.";
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Fallo al pedir el progreso de logros del juego {appId}", ex);
            result.PlayerWarning = "No se ha podido leer tu progreso.";
        }

        return result;
    }

    // ===================== SteamID del usuario =====================

    /// <summary>Intenta averiguar tu SteamID64 sin que tengas que buscarlo: primero la cuenta
    /// activa en el registro (si Steam esta abierto) y, si no, la cuenta mas reciente de
    /// config\loginusers.vdf. Devuelve null si no se encuentra.</summary>
    public static string? TryDetectSteamId64()
    {
        const long SteamId64Base = 76561197960265728;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
            if (key?.GetValue("ActiveUser") is int accountId && accountId > 0)
            {
                return (SteamId64Base + accountId).ToString(CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // sin acceso al registro: probamos con el archivo.
        }

        try
        {
            var steamPath = LibraryPathsService.Instance.SteamPath ?? LibraryPathsService.AutoDetectSteamPath();
            if (string.IsNullOrWhiteSpace(steamPath)) return null;

            var vdf = Path.Combine(steamPath, "config", "loginusers.vdf");
            if (!File.Exists(vdf)) return null;

            var text = File.ReadAllText(vdf);

            string? fallback = null;
            foreach (Match match in Regex.Matches(text, "\"(7656\\d{13})\"\\s*\\{([^}]*)\\}"))
            {
                var id = match.Groups[1].Value;
                fallback ??= id;

                if (Regex.IsMatch(match.Groups[2].Value, "\"MostRecent\"\\s*\"1\"")) return id;
            }

            return fallback;
        }
        catch
        {
            return null;
        }
    }

    // ===================== Utilidades de JSON =====================

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int GetInt(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : 0;

    private static long GetLong(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var number)
            ? number
            : 0;

    private static string? JoinStrings(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array) return null;

        var values = array.EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return values.Count == 0 ? null : string.Join(", ", values);
    }

    private static string? JoinObjects(JsonElement element, string property, string field)
    {
        if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array) return null;

        var values = array.EnumerateArray()
            .Select(v => GetString(v, field))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return values.Count == 0 ? null : string.Join(", ", values);
    }

    /// <summary>Quita etiquetas HTML y decodifica entidades (&amp;quot; etc.) del texto de Steam.</summary>
    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var withoutTags = Regex.Replace(text, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }
}
