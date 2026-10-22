using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;

namespace Launcher.Core.Services;

/// <summary>
/// Gestiona las herramientas externas que necesita el modulo Musica (yt-dlp y FFmpeg)
/// para que el usuario no tenga que instalar nada: el propio Launcher las descarga la
/// primera vez a %AppData%\Launcher\tools y las usa desde ahi.
/// </summary>
public sealed class ExternalToolsService
{
    public static ExternalToolsService Instance { get; } = new();

    public static readonly string ToolsFolder = Path.Combine(AppSettingsService.AppFolder, "tools");

    // Descarga directa del ultimo yt-dlp.exe publicado y de una build "essentials" de FFmpeg.
    private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string FfmpegUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };

    public string YtDlpPath => Path.Combine(ToolsFolder, "yt-dlp.exe");
    public string FfmpegPath => Path.Combine(ToolsFolder, "ffmpeg.exe");

    public bool IsReady => File.Exists(YtDlpPath) && File.Exists(FfmpegPath);

    private ExternalToolsService() { }

    /// <summary>Descarga lo que falte. No hace nada si ya estan las dos herramientas.</summary>
    public async Task EnsureAsync(IProgress<string> log, CancellationToken ct)
    {
        Directory.CreateDirectory(ToolsFolder);

        if (!File.Exists(YtDlpPath))
        {
            log.Report("Descargando yt-dlp...");
            await DownloadFileAsync(YtDlpUrl, YtDlpPath, "yt-dlp", log, ct);
        }

        if (!File.Exists(FfmpegPath))
        {
            var zipPath = Path.Combine(ToolsFolder, "ffmpeg.zip.part");
            try
            {
                log.Report("Descargando FFmpeg (unos 100 MB, solo la primera vez)...");
                await DownloadFileAsync(FfmpegUrl, zipPath, "FFmpeg", log, ct);

                log.Report("Extrayendo ffmpeg.exe...");
                await Task.Run(() =>
                {
                    using var zip = ZipFile.OpenRead(zipPath);
                    var entry = zip.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith("bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException("No se encontro ffmpeg.exe dentro del zip descargado.");
                    entry.ExtractToFile(FfmpegPath, overwrite: true);
                }, ct);
            }
            finally
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { /* limpieza best-effort */ }
            }
        }

        log.Report("Herramientas listas.");
    }

    /// <summary>yt-dlp se rompe cada cierto tiempo cuando YouTube y otras webs cambian;
    /// su propio actualizador (-U) se baja la version nueva.</summary>
    public async Task UpdateYtDlpAsync(IProgress<string> log, CancellationToken ct)
    {
        if (!File.Exists(YtDlpPath))
        {
            await EnsureAsync(log, ct);
            return;
        }

        log.Report("Buscando actualizacion de yt-dlp...");

        var psi = new ProcessStartInfo
        {
            FileName = YtDlpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-U");

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log.Report(e.Data.Trim()); };
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log.Report(e.Data.Trim()); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var reg = ct.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
        await process.WaitForExitAsync(CancellationToken.None);
    }

    private static async Task DownloadFileAsync(string url, string destination, string label,
                                                IProgress<string> log, CancellationToken ct)
    {
        var temp = destination + ".download";

        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using (var target = File.Create(temp))
            {
                var buffer = new byte[81920];
                long done = 0;
                var lastPercent = -1;
                int read;

                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                    done += read;

                    if (total is > 0)
                    {
                        var percent = (int)(done * 100 / total.Value);
                        if (percent / 10 != lastPercent / 10)
                        {
                            lastPercent = percent;
                            log.Report($"{label}: {percent}%");
                        }
                    }
                }
            }

            File.Move(temp, destination, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { /* limpieza best-effort */ }
        }
    }
}
