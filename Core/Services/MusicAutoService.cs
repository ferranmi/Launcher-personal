using System.IO;
using System.Text.RegularExpressions;
using Launcher.Core.Models;

namespace Launcher.Core.Services;

/// <summary>Resultado del primer paso: los datos identificados y las fuentes de audio posibles,
/// listos para que el usuario los revise antes de descargar nada.</summary>
public class MusicPreview
{
    public string Input { get; init; } = "";
    public MusicMetadata Meta { get; init; } = new();

    /// <summary>Fuentes de audio candidatas, la mejor primero. Con un enlace solo hay una.</summary>
    public List<MusicSearchResult> Sources { get; init; } = new();
}

/// <summary>
/// Flujo automatico en dos pasos. Acepta un enlace o texto "artista - cancion":
///   1. PrepareAsync: identifica la cancion (MusicBrainz) y localiza la fuente de audio, sin descargar.
///      Enlace: lee titulo/canal del video y valida varias interpretaciones.
///      Texto: identifica "artista - cancion" y busca en YouTube la version que mejor coincide.
///   2. DownloadAsync: descarga la fuente elegida y escribe los datos revisados (titulo, artista,
///      album, ano, caratula oficial), renombrando el archivo a "Artista - Titulo.mp3".
/// Si MusicBrainz no da una coincidencia fiable, usa el titulo limpiado y lo marca como no verificado.
/// </summary>
public static class MusicAutoService
{
    private static readonly string[] BadWords =
    {
        "live", "cover", "remix", "karaoke", "reaction", "slowed", "reverb",
        "sped up", "8d", "instrumental", "nightcore", "tutorial"
    };

    public static bool IsUrl(string input) =>
        Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    // ===================== Paso 1: identificar y previsualizar =====================

    public static async Task<MusicPreview> PrepareAsync(string input, IProgress<string> log, CancellationToken ct)
    {
        input = input.Trim();
        return IsUrl(input)
            ? await PrepareFromUrlAsync(input, log, ct)
            : await PrepareFromTextAsync(input, log, ct);
    }

    private static async Task<MusicPreview> PrepareFromUrlAsync(string url, IProgress<string> log, CancellationToken ct)
    {
        log.Report("Leyendo datos del enlace...");
        var info = await MusicDownloadService.GetInfoAsync(url, ct)
                   ?? throw new InvalidOperationException("No he podido leer ese enlace.");

        var candidates = MusicTitleCleaner.Candidates(info.Title, info.Artist, info.Track, info.Channel);

        var meta = await MusicBrainzService.ResolveAsync(candidates, info.Duration, log, ct)
                   ?? Fallback(candidates, log);

        return new MusicPreview
        {
            Input = url,
            Meta = meta,
            Sources = { new MusicSearchResult(url, info.Title, info.Channel ?? "", info.Duration) }
        };
    }

    private static async Task<MusicPreview> PrepareFromTextAsync(string text, IProgress<string> log, CancellationToken ct)
    {
        var candidates = MusicTitleCleaner.Candidates(text, null, null, null);
        if (candidates.Count == 0) throw new InvalidOperationException("Escribe algo como \"artista - cancion\".");

        var meta = await MusicBrainzService.ResolveAsync(candidates, null, log, ct)
                   ?? Fallback(candidates, log);

        // Con la cancion identificada la busqueda en YouTube es mucho mas precisa.
        var query = string.IsNullOrWhiteSpace(meta.Artist) ? meta.Title : $"{meta.Artist} {meta.Title}";

        log.Report("Buscando versiones en YouTube...");
        var results = await MusicDownloadService.SearchAsync(query, 6, ct);
        if (results.Count == 0) throw new InvalidOperationException("No he encontrado esa cancion en YouTube.");

        // La duracion oficial de MusicBrainz permite distinguir la version de estudio de directos o remixes.
        var ordered = results
            .Select(r => (Result: r, Score: ScoreResult(r, meta.Artist, meta.Title, meta.DurationSeconds, query)))
            .OrderByDescending(x => x.Score)
            .Select(x => x.Result)
            .ToList();

        return new MusicPreview { Input = text, Meta = meta, Sources = ordered };
    }

    /// <summary>Sin coincidencia fiable: titulo limpiado como mejor suposicion (prefiere un candidato con artista).</summary>
    private static MusicMetadata Fallback(List<(string Artist, string Title)> candidates, IProgress<string> log)
    {
        var pick = candidates.FirstOrDefault(c => c.Artist.Length > 0);
        if (string.IsNullOrWhiteSpace(pick.Title)) pick = candidates[0];

        log.Report("No he encontrado una coincidencia fiable en MusicBrainz: uso el titulo limpiado (revisalo antes de guardar).");

        return new MusicMetadata { Title = pick.Title, Artist = pick.Artist, Verified = false };
    }

    /// <summary>Puntua un resultado de YouTube: prefiere canales "- Topic", duracion cercana y
    /// penaliza directos, remixes, versiones aceleradas... salvo que se pidieran.</summary>
    private static double ScoreResult(MusicSearchResult r, string artist, string title, double? duration, string query)
    {
        double score = 0;
        var nt = MusicTitleCleaner.Normalize(r.Title);
        var nc = MusicTitleCleaner.Normalize(r.Channel);

        if (title.Length > 0 && nt.Contains(MusicTitleCleaner.Normalize(title))) score += 2;

        if (artist.Length > 0)
        {
            var na = MusicTitleCleaner.Normalize(artist);
            if (nt.Contains(na) || nc.Contains(na)) score += 1.5;
        }

        if (r.Channel.EndsWith("- Topic", StringComparison.OrdinalIgnoreCase)) score += 3;
        if (nt.Contains("officialaudio")) score += 1;

        if (duration is { } d && r.Duration is { } rd)
        {
            var diff = Math.Abs(rd - d);
            score += diff <= 3 ? 3 : diff <= 8 ? 1.5 : diff > 20 ? -3 : 0;
        }

        foreach (var word in BadWords)
        {
            var pattern = $@"\b{Regex.Escape(word)}\b";
            if (Regex.IsMatch(r.Title, pattern, RegexOptions.IgnoreCase) &&
                !Regex.IsMatch(query, pattern, RegexOptions.IgnoreCase))
            {
                score -= 4;
            }
        }

        return score;
    }

    /// <summary>Miniatura de un video de YouTube (para la previsualizacion cuando no hay caratula oficial).</summary>
    public static string? ThumbnailUrl(string url)
    {
        var m = Regex.Match(url, @"(?:v=|youtu\.be/|shorts/)([\w-]{11})");
        return m.Success ? $"https://i.ytimg.com/vi/{m.Groups[1].Value}/hqdefault.jpg" : null;
    }

    // ===================== Paso 2: descargar con los datos revisados =====================

    /// <summary>Descarga la fuente elegida y escribe en el MP3 los datos que el usuario ha revisado.</summary>
    public static async Task<List<string>> DownloadAsync(
        MusicSearchResult source, MusicMetadata meta, string folder, IProgress<string> log, CancellationToken ct)
    {
        log.Report("Descargando audio...");
        var files = await MusicDownloadService.DownloadAsync(source.Url, folder, log, ct);
        return files.Select(f => Finalize(f, meta)).ToList();
    }

    // ===================== Escribir datos en el archivo =====================

    /// <summary>Escribe las etiquetas, incrusta la caratula oficial (si la hay) y renombra el archivo
    /// a "Artista - Titulo.mp3". Si algo falla, devuelve la ruta original sin tocar.</summary>
    private static string Finalize(string path, MusicMetadata meta)
    {
        try
        {
            using (var f = TagLib.File.Create(path))
            {
                f.Tag.Title = meta.Title;
                f.Tag.Performers = string.IsNullOrWhiteSpace(meta.Artist) ? Array.Empty<string>() : new[] { meta.Artist };
                f.Tag.AlbumArtists = f.Tag.Performers;
                f.Tag.Album = meta.Album;
                f.Tag.Year = meta.Year;
                f.Tag.Comment = null; // yt-dlp puede dejar aqui la descripcion entera del video

                // Sin caratula oficial se conserva la miniatura del video que yt-dlp ya incrusto.
                if (meta.CoverBytes != null)
                {
                    var picture = new TagLib.Picture(new TagLib.ByteVector(meta.CoverBytes))
                    {
                        Type = TagLib.PictureType.FrontCover
                    };
                    f.Tag.Pictures = new TagLib.IPicture[] { picture };
                }

                f.Save();
            }

            return Rename(path, meta);
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudieron escribir los datos en '{path}'", ex);
            return path;
        }
    }

    private static string Rename(string path, MusicMetadata meta)
    {
        var baseName = string.IsNullOrWhiteSpace(meta.Artist) ? meta.Title : $"{meta.Artist} - {meta.Title}";
        foreach (var c in Path.GetInvalidFileNameChars()) baseName = baseName.Replace(c, '_');
        baseName = baseName.Trim();
        if (baseName.Length > 120) baseName = baseName[..120].TrimEnd();
        if (baseName.Length == 0) return path;

        var directory = Path.GetDirectoryName(path)!;
        var target = Path.Combine(directory, baseName + Path.GetExtension(path));

        if (string.Equals(target, path, StringComparison.OrdinalIgnoreCase)) return path;

        target = MusicLibraryService.UniquePath(target);
        File.Move(path, target);
        return target;
    }

    // ===================== Autocompletar una cancion ya importada =====================

    /// <summary>Busca los datos correctos de una cancion de la biblioteca a partir de sus
    /// etiquetas actuales y del nombre del archivo. Devuelve null si no hay coincidencia fiable.</summary>
    public static async Task<MusicMetadata?> ResolveForTrackAsync(MusicTrack track, CancellationToken ct)
    {
        var candidates = MusicTitleCleaner.Candidates(track.Title, track.Artist, track.Title, null);

        foreach (var c in MusicTitleCleaner.Candidates(Path.GetFileNameWithoutExtension(track.FilePath), null, null, null))
        {
            if (!candidates.Any(x =>
                    string.Equals(x.Artist, c.Artist, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.Title, c.Title, StringComparison.OrdinalIgnoreCase)))
            {
                candidates.Add(c);
            }
        }

        double? duration = track.Duration.TotalSeconds > 0 ? track.Duration.TotalSeconds : null;
        return await MusicBrainzService.ResolveAsync(candidates, duration, null, ct);
    }
}
