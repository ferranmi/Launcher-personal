using System.Diagnostics;
using System.IO;
using System.Text;

namespace Launcher.Core.Services;

/// <summary>
/// Descarga el audio de una URL con yt-dlp (que ExternalToolsService ya habra dejado
/// en %AppData%\Launcher\tools), lo convierte a MP3 con FFmpeg y le incrusta
/// metadatos y caratula. Devuelve las rutas de los archivos finales.
/// </summary>
public static class MusicDownloadService
{
    public static async Task<List<string>> DownloadAsync(
        string url, string outputFolder, IProgress<string> log, CancellationToken ct)
    {
        var tools = ExternalToolsService.Instance;
        Directory.CreateDirectory(outputFolder);

        var psi = new ProcessStartInfo
        {
            FileName = tools.YtDlpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Solo el audio, en MP3 de la mejor calidad, con titulo/artista y caratula incrustados.
        foreach (var arg in new[]
                 {
                     "-x", "--audio-format", "mp3", "--audio-quality", "0",
                     "--embed-metadata", "--embed-thumbnail", "--convert-thumbnails", "jpg",
                     "--no-playlist", "--newline",
                     "--ffmpeg-location", ExternalToolsService.ToolsFolder,
                     "-P", outputFolder,
                     "-o", "%(title)s.%(ext)s",
                     // Imprime la ruta final de cada archivo para poder importarlo despues.
                     "--print", "after_move:filepath", "--no-simulate",
                     url
                 })
        {
            psi.ArgumentList.Add(arg);
        }

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

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => HandleLine(e.Data);
        process.ErrorDataReceived += (_, e) => HandleLine(e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var reg = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        });

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
}
