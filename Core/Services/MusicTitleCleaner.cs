using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Launcher.Core.Services;

/// <summary>
/// Convierte el titulo "sucio" de un video ("Artista - Cancion (Official Video) [4K] | Canal")
/// en candidatos (artista, titulo) que luego se comprueban contra MusicBrainz. Nunca da por
/// buena una sola interpretacion: el titulo puede venir como "Artista - Cancion" o al reves, y
/// el canal solo es una pista, no el artista.
/// </summary>
public static class MusicTitleCleaner
{
    // Palabras que delatan relleno de YouTube dentro de parentesis/corchetes.
    private static readonly Regex JunkWords = new(
        @"\b(official|video|audio|lyrics?|letra|oficial|visuali[sz]er|hd|4k|1080p|720p|hq|mv|clip|explicit|" +
        @"feat|ft|featuring|remaster(ed)?|subtitulad[oa]|traducid[oa]|full album|music video)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Brackets = new(@"[\(\[\{][^\)\]\}]*[\)\]\}]", RegexOptions.Compiled);
    private static readonly Regex AfterPipe = new(@"\s*[\|\uFF5C].*$", RegexOptions.Compiled);

    private static readonly Regex TrailingJunk = new(
        @"[\s\-\u2013\u2014]*(official\s+(music\s+)?(video|audio)|lyrics?(\s+video)?|letra|video\s+oficial|" +
        @"audio\s+oficial|music\s+video|visuali[sz]er)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Feat = new(
        @"\s+(feat|ft|featuring)\.?\s+.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DashSplit = new(@"\s[-\u2013\u2014\u2015]\s", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    // ===================== Limpieza =====================

    /// <summary>Quita relleno de YouTube pero conserva la estructura "A - B".</summary>
    public static string CleanBase(string raw)
    {
        var s = raw ?? "";
        s = Brackets.Replace(s, m => JunkWords.IsMatch(m.Value) ? "" : m.Value);
        s = AfterPipe.Replace(s, "");
        s = TrailingJunk.Replace(s, "");
        s = s.Replace("\"", "").Replace("\u201C", "").Replace("\u201D", "");
        return Spaces.Replace(s, " ").Trim(' ', '-', '\u2013', '\u2014');
    }

    /// <summary>Quita "ft. X" / "feat. X" del final de un trozo (artista o titulo).</summary>
    public static string StripFeat(string s) =>
        Spaces.Replace(Feat.Replace(s ?? "", ""), " ").Trim();

    /// <summary>"Artista - Topic" / "ArtistaVEVO" -> "Artista". Solo una pista de artista.</summary>
    public static string ArtistFromChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return "";

        var s = channel.Trim();
        s = Regex.Replace(s, @"\s*-\s*Topic$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s*VEVO$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s+Official$", "", RegexOptions.IgnoreCase);
        return s.Trim();
    }

    private static string FirstArtist(string artist)
    {
        var first = artist.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return (first ?? artist).Trim();
    }

    // ===================== Candidatos =====================

    /// <summary>
    /// Lista ordenada de (artista, titulo) posibles. Orden: metadatos que ya trae la fuente,
    /// "A - B", "B - A", canal + titulo, y por ultimo solo el titulo. MusicBrainz decide cual vale.
    /// </summary>
    public static List<(string Artist, string Title)> Candidates(
        string rawTitle, string? hintArtist, string? hintTrack, string? channel)
    {
        var list = new List<(string Artist, string Title)>();

        void Add(string artist, string title)
        {
            artist = StripFeat(artist);
            title = StripFeat(title);
            if (string.IsNullOrWhiteSpace(title)) return;

            var exists = list.Any(c =>
                string.Equals(c.Artist, artist, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Title, title, StringComparison.OrdinalIgnoreCase));
            if (!exists) list.Add((artist.Trim(), title.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(hintArtist) && !string.IsNullOrWhiteSpace(hintTrack))
        {
            Add(FirstArtist(hintArtist), CleanBase(hintTrack));
        }

        var cleaned = CleanBase(rawTitle);
        var parts = DashSplit.Split(cleaned, 2);

        if (parts.Length == 2)
        {
            Add(parts[0], parts[1]);
            Add(parts[1], parts[0]); // a veces el video viene como "Cancion - Artista"
        }

        var fromChannel = ArtistFromChannel(channel);
        if (fromChannel.Length > 0)
        {
            Add(fromChannel, parts.Length == 2 ? parts[1] : cleaned);
        }

        if (parts.Length == 2) Add("", parts[1]);
        Add("", cleaned);

        return list;
    }

    // ===================== Comparacion de textos =====================

    /// <summary>Minusculas, sin acentos ni signos: "Beyoncé - Halo!" -> "beyoncehalo".</summary>
    public static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";

        var decomposed = s.Replace("&", "and").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    /// <summary>Parecido entre dos textos, de 0 a 1. Si uno contiene al otro cuenta como casi igual.</summary>
    public static double Similarity(string? a, string? b)
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        if (na.Length == 0 || nb.Length == 0) return 0;
        if (na == nb) return 1;

        var ratio = 1.0 - Levenshtein(na, nb) / (double)Math.Max(na.Length, nb.Length);

        if (na.Length >= 4 && nb.Length >= 4 && (na.Contains(nb) || nb.Contains(na)))
        {
            ratio = Math.Max(ratio, 0.9);
        }

        return ratio;
    }

    private static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }
}
