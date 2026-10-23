using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Launcher.Core.Services;

/// <summary>Datos que yt-dlp conoce de un enlace. Solo son pistas: no se fia de ellos.</summary>
public record MusicSourceInfo(
    string Title, string? Channel, string? Artist, string? Track, string? Album, double? Duration);

/// <summary>Un resultado de busqueda en YouTube.</summary>
public record MusicSearchResult(string Url, string Title, string Channel, double? Duration);

/// <summary>
/// Envoltorio de yt-dlp (que ExternalToolsService ya habra dejado en %AppData%\Launcher\tools):
/// leer datos de un enlace, buscar en YouTube y descargar el audio como MP3.
/// </summary>
public static class MusicDownloadService
{
    // ===================== Informacion de un enlace =====================

    public static async Task<MusicSourceInfo?> GetInfoAsync(string url, CancellationToken ct)
    {
        var json = await RunCaptureAsync(
            new[] { "--dump-single-json", "--no-playlist", "--no-warnings", "--skip-download", url }, ct);

        if (string.IsNullOrWhiteSpace(json)) return null;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Un enlace de lista de reproduccion pura no es una cancion concreta.
        if (Str(root, "_type") == "playlist")
        {
            throw new InvalidOperationException("Ese enlace es una lista de reproduccion. Pega el enlace de una cancion.");
        }

        return new MusicSourceInfo(
            Str(root, "title") ?? "",
            Str(root, "channel") ?? Str(root, "uploader"),
            Str(root, "artist"),
            Str(root, "track"),
            Str(root, "album"),
            Num(root, "duration"));
    }

    // ===================== Busqueda en YouTube =====================

    public static async Task<List<MusicSearchResult>> SearchAsync(string query, int count, CancellationToken ct)
    {
        var output = await RunCaptureAsync(
            new[] { "--flat-playlist", "--dump-json", "--no-warnings", $"ytsearch{count}:{query}" }, ct);

        var results = new List<MusicSearchResult>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var id = Str(root, "id");
                var url = Str(root, "url");
                if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    url = "https://www.youtube.com/watch?v=" + id;
                }

                results.Add(new MusicSearchResult(
                    url,
                    Str(root, "title") ?? "",
                    Str(root, "channel") ?? Str(root, "uploader") ?? "",
                    Num(root, "duration")));
            }
            catch
            {
                // Linea que no es JSON valido (aviso suelto de yt-dlp): se ignora.
            }
        }

        return results;
    }

    // ===================== Descarga =====================

    /// <summary>Descarga el audio como MP3 en outputFolder y devuelve las rutas finales. Los datos
    /// (titulo, artista...) los escribe despues MusicAutoService; aqui solo se incrusta la miniatura
    /// del video como caratula de respaldo.</summary>
    public static async Task<List<string>> DownloadAsync(
        string url, string outputFolder, IProgress<string> log, CancellationToken ct)
    {
        Directory.CreateDirectory(outputFolder);

        var args = new[]
        {
            "-x", "--audio-format", "mp3", "--audio-quality", "0",
            "--embed-thumbnail", "--convert-thumbnails", "jpg",
            "--no-playlist", "--newline",
            "--ffmpeg-location", ExternalToolsService.ToolsFolder,
            "-P", outputFolder,
            "-o", "%(title)s.%(ext)s",
            // Imprime la ruta final de cada archivo para poder tratarlo despues.
            "--print", "after_move:filepath", "--no-simulate",
            url
        };

        var files = new List<string>();

        void HandleLine(string? data)
        {
            if (string.IsNullOrWhiteSpace(data)) return;
            var line = data.Trim();

            if (Path.IsPathRooted(line) && File.Exists(line))
            {
                lock (files) files.Add(line);
                log.Report("Guardado: " + Path.GetFileName(line));
            }
            else
            {
                log.Report(line);
            }
        }

        using var process = new Process { StartInfo = CreateStartInfo(args) };
        process.OutputDataReceived += (_, e) => HandleLine(e.Data);
        process.ErrorDataReceived += (_, e) => HandleLine(e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var reg = ct.Register(() => Kill(process));

        await process.WaitForExitAsync(CancellationToken.None);
        ct.ThrowIfCancellationRequested();

        List<string> result;
        lock (files) result = files.ToList();

        if (process.ExitCode != 0 && result.Count == 0)
        {
            throw new InvalidOperationException(
                $"yt-dlp termino con error (codigo {process.ExitCode}). Prueba a pulsar \"Actualizar yt-dlp\".");
        }

        return result;
    }

    // ===================== Utilidades =====================

    private static ProcessStartInfo CreateStartInfo(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ExternalToolsService.Instance.YtDlpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    /// <summary>Ejecuta yt-dlp y devuelve todo lo que escribe por la salida estandar.</summary>
    private static async Task<string> RunCaptureAsync(IEnumerable<string> args, CancellationToken ct)
    {
        using var process = new Process { StartInfo = CreateStartInfo(args) };
        process.Start();

        using var reg = ct.Register(() => Kill(process));

        // Se leen las dos salidas a la vez para que ninguna llene su buffer y bloquee el proceso.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(CancellationToken.None);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        ct.ThrowIfCancellationRequested();

        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
        {
            var last = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault();
            throw new InvalidOperationException(last ?? $"yt-dlp termino con error (codigo {process.ExitCode}).");
        }

        return stdout;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
}
