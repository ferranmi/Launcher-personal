using System.Net.Http;
using System.Text;
using System.Text.Json;
using Launcher.Core.Models;

namespace Launcher.Core.Services;

/// <summary>
/// Resuelve los datos "correctos" de una cancion (titulo, artista, album, ano y caratula
/// oficial) consultando MusicBrainz y Cover Art Archive, ambos gratuitos y sin clave.
/// Recibe varios candidatos (artista, titulo) y se queda con el que mejor casa, comparando
/// titulo, artista y duracion; si ninguno supera el umbral devuelve null en vez de inventar.
/// </summary>
public static class MusicBrainzService
{
    // MusicBrainz pide identificar la aplicacion; si quieres, anade aqui tu email: "Launcher/0.1 (tu@email)".
    private const string UserAgent = "LauncherPersonal/0.1 (personal project)";

    /// <summary>Puntuacion minima para dar una coincidencia por buena.</summary>
    private const double AcceptThreshold = 0.8;

    private static readonly HttpClient Http = CreateClient();

    // MusicBrainz permite como maximo 1 peticion por segundo.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime _lastCall = DateTime.MinValue;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    private sealed class Match
    {
        public double Score;
        public string Title = "";
        public string Artist = "";
        public string Album = "";
        public uint Year;
        public double? Length;
        public List<string> CoverUrls = new();
    }

    // ===================== API publica =====================

    public static async Task<MusicMetadata?> ResolveAsync(
        IEnumerable<(string Artist, string Title)> candidates,
        double? durationSeconds,
        IProgress<string>? log,
        CancellationToken ct)
    {
        Match? best = null;
        var queries = 0;

        foreach (var (artist, title) in candidates)
        {
            if (queries++ >= 5) break;
            ct.ThrowIfCancellationRequested();

            log?.Report(string.IsNullOrWhiteSpace(artist)
                ? $"Buscando en MusicBrainz: {title}"
                : $"Buscando en MusicBrainz: {artist} - {title}");

            var lucene = $"recording:\"{Escape(title)}\"";
            if (!string.IsNullOrWhiteSpace(artist)) lucene += $" AND artist:\"{Escape(artist)}\"";

            var url = "https://musicbrainz.org/ws/2/recording/?fmt=json&limit=8&query=" + Uri.EscapeDataString(lucene);

            using var doc = await GetJsonAsync(url, ct);
            if (doc == null || !doc.RootElement.TryGetProperty("recordings", out var recordings)) continue;

            foreach (var rec in recordings.EnumerateArray())
            {
                var match = Evaluate(rec, artist, title, durationSeconds);
                if (match != null && (best == null || match.Score > best.Score)) best = match;
            }

            if (best is { Score: >= 0.95 }) break;
        }

        if (best == null || best.Score < AcceptThreshold) return null;

        var cover = await FetchCoverAsync(best.CoverUrls, ct);

        return new MusicMetadata
        {
            Title = best.Title,
            Artist = best.Artist,
            Album = best.Album,
            Year = best.Year,
            DurationSeconds = best.Length,
            CoverBytes = cover,
            Verified = true
        };
    }

    // ===================== Evaluacion de una grabacion =====================

    private static Match? Evaluate(JsonElement rec, string queryArtist, string queryTitle, double? durationSeconds)
    {
        var title = Str(rec, "title");
        if (title.Length == 0) return null;

        // Credito de artista completo ("A feat. B") y nombres sueltos.
        var credit = new StringBuilder();
        var names = new List<string>();
        if (rec.TryGetProperty("artist-credit", out var credits))
        {
            foreach (var c in credits.EnumerateArray())
            {
                var name = Str(c, "name");
                if (name.Length == 0 && c.TryGetProperty("artist", out var a)) name = Str(a, "name");
                credit.Append(name).Append(Str(c, "joinphrase"));
                if (name.Length > 0) names.Add(name);
            }
        }

        var creditText = credit.ToString().Trim();
        if (creditText.Length == 0) return null;

        var titleSim = MusicTitleCleaner.Similarity(queryTitle, title);

        double artistSim = 0;
        if (!string.IsNullOrWhiteSpace(queryArtist))
        {
            artistSim = MusicTitleCleaner.Similarity(queryArtist, creditText);
            foreach (var n in names) artistSim = Math.Max(artistSim, MusicTitleCleaner.Similarity(queryArtist, n));
        }

        var score = 0.5 * titleSim + 0.35 * artistSim;

        if (rec.TryGetProperty("length", out var len) && len.ValueKind == JsonValueKind.Number &&
            durationSeconds is { } expected)
        {
            var diff = Math.Abs(len.GetDouble() / 1000.0 - expected);
            score += diff <= 4 ? 0.2 : diff <= 10 ? 0.05 : diff > 30 ? -0.15 : 0;
        }

        if (rec.TryGetProperty("score", out var mb) && mb.ValueKind == JsonValueKind.Number)
        {
            score += mb.GetDouble() / 100.0 * 0.15;
        }

        // Versiones en directo, remix, karaoke... solo valen si el usuario las pidio.
        var disambiguation = Str(rec, "disambiguation").ToLowerInvariant();
        foreach (var word in new[] { "live", "remix", "karaoke", "cover", "demo", "instrumental" })
        {
            if (disambiguation.Contains(word) && !queryTitle.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                score -= 0.25;
                break;
            }
        }

        var ranked = RankReleases(rec);

        var year = ParseYear(Str(rec, "first-release-date"));
        if (year == 0 && ranked.Count > 0) year = ParseYear(ranked[0].Date);

        var match = new Match
        {
            Score = score,
            Title = title,
            Artist = creditText,
            Album = ranked.Count > 0 ? ranked[0].Title : "",
            Year = year,
            Length = rec.TryGetProperty("length", out var lengthMs) && lengthMs.ValueKind == JsonValueKind.Number
                ? lengthMs.GetDouble() / 1000.0
                : null
        };

        // Orden de busqueda de caratula: mejor edicion, su grupo, y las siguientes ediciones.
        foreach (var r in ranked.Take(1)) match.CoverUrls.Add($"https://coverartarchive.org/release/{r.Id}/front-500");
        if (ranked.Count > 0 && !string.IsNullOrEmpty(ranked[0].GroupId))
        {
            match.CoverUrls.Add($"https://coverartarchive.org/release-group/{ranked[0].GroupId}/front-500");
        }
        foreach (var r in ranked.Skip(1).Take(2)) match.CoverUrls.Add($"https://coverartarchive.org/release/{r.Id}/front-500");

        return match;
    }

    private sealed record ReleaseInfo(string Id, string Title, string Date, string GroupId, int Rank);

    /// <summary>Ordena las ediciones donde aparece la grabacion: primero el album oficial mas antiguo,
    /// despues singles/EP, y al final recopilatorios, directos y similares.</summary>
    private static List<ReleaseInfo> RankReleases(JsonElement rec)
    {
        var list = new List<ReleaseInfo>();
        if (!rec.TryGetProperty("releases", out var releases)) return list;

        foreach (var r in releases.EnumerateArray())
        {
            var id = Str(r, "id");
            if (id.Length == 0) continue;

            string primary = "", groupId = "";
            var hasSecondary = false;

            if (r.TryGetProperty("release-group", out var g))
            {
                groupId = Str(g, "id");
                primary = Str(g, "primary-type");
                hasSecondary = g.TryGetProperty("secondary-types", out var st) &&
                               st.ValueKind == JsonValueKind.Array && st.GetArrayLength() > 0;
            }

            var rank = hasSecondary ? 4
                : primary == "Album" ? 0
                : primary is "Single" or "EP" ? 1
                : primary.Length == 0 ? 2
                : 3;

            var status = Str(r, "status");
            if (status.Length > 0 && status != "Official") rank += 10;

            var date = Str(r, "date");
            list.Add(new ReleaseInfo(id, Str(r, "title"), date.Length > 0 ? date : "9999", groupId, rank));
        }

        return list.OrderBy(x => x.Rank).ThenBy(x => x.Date, StringComparer.Ordinal).ToList();
    }

    // ===================== Caratula =====================

    private static async Task<byte[]?> FetchCoverAsync(IEnumerable<string> urls, CancellationToken ct)
    {
        foreach (var url in urls)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var response = await Http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) continue;

                var type = response.Content.Headers.ContentType?.MediaType ?? "";
                if (!type.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue;

                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length > 1000) return bytes;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Sin caratula en esta edicion o fallo de red: probamos con la siguiente.
            }
        }

        return null;
    }

    // ===================== HTTP / utilidades =====================

    private static async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Gate.WaitAsync(ct);
            try
            {
                var wait = TimeSpan.FromMilliseconds(1100) - (DateTime.UtcNow - _lastCall);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);

                using var response = await Http.GetAsync(url, ct);
                _lastCall = DateTime.UtcNow;

                if (response.IsSuccessStatusCode)
                {
                    return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                }

                // Solo reintentamos si MusicBrainz esta saturado; cualquier otro error se abandona.
                if ((int)response.StatusCode is not (503 or 429)) return null;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogService.Error("Consulta a MusicBrainz fallida", ex);
                return null;
            }
            finally
            {
                Gate.Release();
            }

            await Task.Delay(2000, ct);
        }

        return null;
    }

    private static string Escape(string s) => s.Replace("\\", " ").Replace("\"", " ").Trim();

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static uint ParseYear(string date) =>
        date.Length >= 4 && uint.TryParse(date.AsSpan(0, 4), out var y) && y >= 1900 ? y : 0;
}
