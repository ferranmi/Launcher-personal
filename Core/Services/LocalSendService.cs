using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Launcher.Core.Services;

/// <summary>
/// Cliente de envio del protocolo abierto LocalSend v2 (github.com/localsend/protocol).
/// No usa el descubrimiento multicast: se dirige a una IP concreta (la de Tailscale del
/// dispositivo), asi funciona igual dentro de casa que fuera. Solo envia; recibir lo
/// sigue haciendo la propia app de LocalSend.
/// </summary>
public sealed class LocalSendService
{
    private const int Port = 53317;
    private const string Api = "/api/localsend/v2";

    private readonly string _fingerprint = Guid.NewGuid().ToString("N");

    // LocalSend usa certificados autofirmados, asi que no se puede validar la cadena.
    // Solo se contacta con IPs de dispositivos de la tailnet que el usuario ve en pantalla.
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    })
    { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>Devuelve la URL base si LocalSend responde en esa IP (HTTPS y luego HTTP), o null.</summary>
    public async Task<string?> ProbeAsync(string ip, CancellationToken ct)
    {
        foreach (var scheme in new[] { "https", "http" })
        {
            var baseUrl = $"{scheme}://{ip}:{Port}";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));

            try
            {
                using var response = await Http.GetAsync(baseUrl + Api + "/info", timeout.Token);
                if (response.IsSuccessStatusCode) return baseUrl;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { /* sin respuesta a tiempo */ }
            catch (HttpRequestException) { /* puerto cerrado o sin ruta */ }
        }

        return null;
    }

    public async Task<TransferOutcome> SendAsync(
        string baseUrl, IReadOnlyList<string> paths, IProgress<string> progress, CancellationToken ct)
    {
        var pathById = new Dictionary<string, string>();
        var files = new Dictionary<string, object>();

        foreach (var path in paths)
        {
            var info = new FileInfo(path);
            var id = Guid.NewGuid().ToString("N");
            pathById[id] = path;
            files[id] = new { id, fileName = info.Name, size = info.Length, fileType = GuessMime(info.Extension) };
        }

        var request = new
        {
            info = new
            {
                alias = Environment.MachineName,
                version = "2.0",
                deviceModel = "Windows",
                deviceType = "desktop",
                fingerprint = _fingerprint,
                port = Port,
                protocol = "https",
                download = false
            },
            files
        };

        using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        // prepare-upload se queda esperando hasta que el otro dispositivo acepta o rechaza.
        progress.Report("LocalSend: esperando a que el otro dispositivo acepte...");
        using var prepareCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        prepareCts.CancelAfter(TimeSpan.FromMinutes(3));

        HttpResponseMessage prepare;
        try
        {
            prepare = await Http.PostAsync(baseUrl + Api + "/prepare-upload", content, prepareCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fail("no hubo respuesta a tiempo");
        }
        catch (HttpRequestException ex)
        {
            return Fail(ex.Message);
        }

        using (prepare)
        {
            switch ((int)prepare.StatusCode)
            {
                case 204: return new TransferOutcome(true, "LocalSend", "El receptor no necesita ningun fichero.");
                case 403: return new TransferOutcome(false, "LocalSend", "El otro dispositivo ha rechazado el envio.", Rejected: true);
                case 401: return Fail("el receptor pide PIN (aun no soportado)");
                case 409: return Fail("el receptor esta ocupado con otra transferencia");
                case 429: return Fail("demasiadas peticiones");
            }

            if (!prepare.IsSuccessStatusCode) return Fail($"error HTTP {(int)prepare.StatusCode}");

            try
            {
                using var doc = JsonDocument.Parse(await prepare.Content.ReadAsStringAsync(ct));
                var sessionId = doc.RootElement.GetProperty("sessionId").GetString() ?? "";
                var tokens = doc.RootElement.GetProperty("files");

                var sent = 0;
                foreach (var (id, path) in pathById)
                {
                    // Si el receptor no acepta un fichero concreto, no trae token para el.
                    if (!tokens.TryGetProperty(id, out var tokenElement)) continue;

                    progress.Report($"LocalSend: enviando {Path.GetFileName(path)}...");

                    var url = $"{baseUrl}{Api}/upload?sessionId={Uri.EscapeDataString(sessionId)}" +
                              $"&fileId={Uri.EscapeDataString(id)}&token={Uri.EscapeDataString(tokenElement.GetString() ?? "")}";

                    await using var stream = File.OpenRead(path);
                    using var body = new StreamContent(stream);
                    using var upload = await Http.PostAsync(url, body, ct);

                    if (!upload.IsSuccessStatusCode)
                        return Fail($"fallo al enviar {Path.GetFileName(path)} (HTTP {(int)upload.StatusCode})");

                    sent++;
                }

                return sent > 0
                    ? new TransferOutcome(true, "LocalSend", $"{sent} fichero(s) enviados.")
                    : Fail("el receptor no ha aceptado ningun fichero");
            }
            catch (HttpRequestException ex)
            {
                return Fail(ex.Message);
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                LogService.Error("Respuesta inesperada de LocalSend", ex);
                return Fail("respuesta inesperada del receptor");
            }
        }
    }

    private static TransferOutcome Fail(string message) => new(false, "LocalSend", message);

    private static string GuessMime(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".mp4" => "video/mp4",
        ".mkv" => "video/x-matroska",
        ".mp3" => "audio/mpeg",
        ".flac" => "audio/flac",
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        ".zip" => "application/zip",
        ".apk" => "application/vnd.android.package-archive",
        _ => "application/octet-stream"
    };
}
