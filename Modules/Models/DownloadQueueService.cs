using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Threading;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

public class DownloadHttpException : Exception
{
    public bool Retryable { get; }
    public DownloadHttpException(string message, bool retryable) : base(message) => Retryable = retryable;
}

/// <summary>
/// Cola de descargas. Reglas: solo entra lo que el usuario ha confirmado; nunca se sobrescribe un archivo; la
/// pausa manual se respeta siempre; se descarga a "archivo.part" y solo se renombra al terminar y verificar
/// (SHA256 cuando Civitai lo publica); las descargas se pueden reanudar con Range. La API Key solo se envia a
/// civitai.com, nunca al almacenamiento al que redirige. Tras reiniciar el Launcher, las descargas que
/// estaban en curso quedan PAUSADAS (no se reanudan solas).
/// </summary>
public sealed class DownloadQueueService
{
    public static DownloadQueueService Instance { get; } = new();

    private static readonly string StatePath = Path.Combine(AppSettingsService.AppFolder, "models-downloads.json");

    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan // las descargas grandes se controlan con CancellationToken
    };

    private readonly Dictionary<string, CancellationTokenSource> _running = new();
    private readonly Dictionary<string, (long Bytes, DateTime At)> _lastSample = new();
    private readonly SpeedLimiter _limiter = new();
    private readonly DispatcherTimer _timer;

    private int _target = 1;
    private double _speedBeforeRaise;
    private DateTime _noRaiseUntil = DateTime.MinValue;
    private DateTime _lastAdapt = DateTime.MinValue;

    public ObservableCollection<DownloadItem> Items { get; } = new();

    /// <summary>Se dispara (en el hilo de la UI) cuando un archivo termina y queda verificado en su sitio.</summary>
    public event Action<DownloadItem>? ItemCompleted;

    /// <summary>Cambia algo que afecta al resumen (cola, velocidad, estados).</summary>
    public event Action? SummaryChanged;

    private static ModelsSettings Settings => ModelsSettings.Instance;

    private DownloadQueueService()
    {
        Load();
        ApplySettings();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    // ===================== Ajustes =====================

    private int MaxConcurrent => Math.Clamp(Settings.MaxConcurrentDownloads, 1, 8);
    private int Target => Settings.AutoConcurrency ? Math.Clamp(_target, 1, MaxConcurrent) : MaxConcurrent;

    public void ApplySettings()
    {
        _limiter.LimitBytesPerSec = Math.Max(0, Settings.SpeedLimitKBps) * 1024L;
        _target = 1;
        _speedBeforeRaise = 0;
        Pump();
    }

    // ===================== Alta y control =====================

    /// <summary>Anade una descarga ya confirmada por el usuario. Devuelve la existente si ya estaba en cola.</summary>
    public DownloadItem Enqueue(DownloadRequest request)
    {
        var existing = Items.FirstOrDefault(i =>
            string.Equals(i.Destination, request.Destination, StringComparison.OrdinalIgnoreCase) &&
            i.State is not (DownloadState.Completed or DownloadState.Cancelled or DownloadState.Failed));
        if (existing != null) return existing;

        var item = new DownloadItem
        {
            FileName = Path.GetFileName(request.Destination),
            ModelName = request.Model.Name,
            VersionName = request.Version.Name,
            TypeLabel = request.Model.TypeLabel,
            ModelId = request.Model.Id,
            VersionId = request.Version.Id,
            Url = request.File.DownloadUrl ?? request.Version.DownloadUrl ?? "",
            Destination = request.Destination,
            Sha256 = request.File.Hashes?.Sha256,
            TrainedWords = request.Version.TrainedWords.ToList(),
            SourceNote = request.SourceNote,
            TotalBytes = request.File.SizeBytes,
            State = DownloadState.Queued
        };

        Items.Add(item);
        Save();
        Pump();
        return item;
    }

    public void Pause(DownloadItem item)
    {
        if (!item.CanPause) return;

        item.State = DownloadState.Paused; // primero el estado: asi el motor sabe que es una pausa y no un fallo
        if (_running.TryGetValue(item.Id, out var cts)) cts.Cancel();

        Save();
        Pump();
    }

    public void Resume(DownloadItem item)
    {
        if (!item.CanResume && !item.CanRetry) return;

        item.Error = null;
        item.State = DownloadState.Queued;
        Save();
        Pump();
    }

    public void Cancel(DownloadItem item)
    {
        if (!item.CanCancel) return;

        item.State = DownloadState.Cancelled;
        if (_running.TryGetValue(item.Id, out var cts)) cts.Cancel();
        else DeletePartial(item);

        Save();
        Pump();
    }

    public void MoveToEnd(DownloadItem item)
    {
        if (!item.CanMoveToEnd) return;

        var index = Items.IndexOf(item);
        if (index >= 0 && index < Items.Count - 1) Items.Move(index, Items.Count - 1);
        Save();
        Pump();
    }

    public void Remove(DownloadItem item)
    {
        if (!item.CanRemove) return;

        if (item.State != DownloadState.Completed) DeletePartial(item);
        Items.Remove(item);
        Save();
        RaiseSummary();
    }

    public void PauseAll()
    {
        foreach (var item in Items.Where(i => i.CanPause).ToList()) Pause(item);
    }

    public void ResumeAll()
    {
        foreach (var item in Items.Where(i => i.CanResume).ToList()) Resume(item);
    }

    public void ClearFinished()
    {
        foreach (var item in Items.Where(i => i.State is DownloadState.Completed or DownloadState.Cancelled).ToList()) Items.Remove(item);
        Save();
        RaiseSummary();
    }

    private static void DeletePartial(DownloadItem item)
    {
        try
        {
            var part = item.Destination + ".part";
            if (File.Exists(part)) File.Delete(part);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo borrar el archivo parcial", ex);
        }
    }

    // ===================== Planificador =====================

    private void Pump()
    {
        var active = Items.Count(i => i.State == DownloadState.Downloading);

        foreach (var item in Items.Where(i => i.State == DownloadState.Queued).ToList())
        {
            if (active >= Target) break;

            Start(item);
            active++;
        }

        RaiseSummary();
    }

    private void Start(DownloadItem item)
    {
        var cts = new CancellationTokenSource();
        _running[item.Id] = cts;
        item.State = DownloadState.Downloading;
        item.Error = null;
        _lastSample.Remove(item.Id);

        _ = RunAsync(item, cts);
    }

    private void Tick()
    {
        var now = DateTime.UtcNow;

        foreach (var item in Items.Where(i => i.State == DownloadState.Downloading))
        {
            if (_lastSample.TryGetValue(item.Id, out var last))
            {
                var seconds = (now - last.At).TotalSeconds;
                if (seconds > 0)
                {
                    var instant = (item.ReceivedBytes - last.Bytes) / seconds;
                    item.SpeedBps = item.SpeedBps <= 0 ? instant : item.SpeedBps * 0.6 + instant * 0.4;
                }
            }

            _lastSample[item.Id] = (item.ReceivedBytes, now);
        }

        AdaptConcurrency(now);
        RaiseSummary();
    }

    /// <summary>Heuristica de descargas simultaneas: si hay cola y el cupo actual esta ocupado, prueba a subir uno;
    /// si subirlo no mejora la velocidad total al menos un 10%, la conexion esta saturada: vuelve a bajar y no
    /// vuelve a subir durante un minuto. Nunca pasa del maximo configurado ni reanuda descargas pausadas.</summary>
    private void AdaptConcurrency(DateTime now)
    {
        if (!Settings.AutoConcurrency || (now - _lastAdapt).TotalSeconds < 8) return;
        _lastAdapt = now;

        var downloading = Items.Where(i => i.State == DownloadState.Downloading).ToList();
        var queued = Items.Count(i => i.State == DownloadState.Queued);
        var total = downloading.Sum(i => i.SpeedBps);

        if (downloading.Count < _target) return; // el cupo actual aun no se ha llegado a usar

        if (_speedBeforeRaise > 0 && _target > 1 && total < _speedBeforeRaise * 1.10)
        {
            _target--;
            _noRaiseUntil = DateTime.UtcNow.AddSeconds(60);
            _speedBeforeRaise = 0;
        }
        else if (queued > 0 && _target < MaxConcurrent && now >= _noRaiseUntil)
        {
            _speedBeforeRaise = total;
            _target++;
            Pump();
        }
    }

    private void RaiseSummary() => SummaryChanged?.Invoke();

    // ===================== Motor de descarga =====================

    private async Task RunAsync(DownloadItem item, CancellationTokenSource cts)
    {
        var ct = cts.Token;
        var retries = Math.Max(0, Settings.DownloadRetries);

        try
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await DownloadOnceAsync(item, ct);
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (DownloadHttpException ex) when (!ex.Retryable)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < retries)
                {
                    LogService.Error($"Descarga de {item.FileName}: reintento {attempt + 1}/{retries}", ex);
                    item.Error = $"reintentando ({attempt + 1}/{retries}): {ex.Message}";
                    await Task.Delay(TimeSpan.FromSeconds(3 * (attempt + 1)), ct);
                }
            }

            await FinishAsync(item, ct);
        }
        catch (OperationCanceledException)
        {
            // Pausa o cancelacion: quien la pidio ya fijo el estado.
        }
        catch (Exception ex)
        {
            LogService.Error($"Fallo la descarga de {item.FileName}", ex);
            item.Error = ex.Message;
            item.State = DownloadState.Failed;
        }
        finally
        {
            _running.Remove(item.Id);
            cts.Dispose();
            item.SpeedBps = 0;

            if (item.State == DownloadState.Cancelled) DeletePartial(item);

            Save();
            Pump();
        }
    }

    private async Task<HttpResponseMessage> SendWithRedirectsAsync(string url, long rangeStart, CancellationToken ct)
    {
        var apiKey = CivitaiService.Instance.GetApiKey();
        var current = new Uri(url);

        for (var hop = 0; hop < 6; hop++)
        {
            if (current.Scheme != Uri.UriSchemeHttps)
            {
                throw new DownloadHttpException("El enlace de descarga no es https: se rechaza por seguridad.", false);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, current);

            var isCivitai = current.Host.Equals("civitai.com", StringComparison.OrdinalIgnoreCase) ||
                            current.Host.EndsWith(".civitai.com", StringComparison.OrdinalIgnoreCase);
            if (isCivitai && apiKey != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            if (rangeStart > 0) request.Headers.Range = new RangeHeaderValue(rangeStart, null);

            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                response.Dispose();
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                continue;
            }

            return response;
        }

        throw new DownloadHttpException("Demasiadas redirecciones al descargar.", false);
    }

    private async Task DownloadOnceAsync(DownloadItem item, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(item.Url)) throw new DownloadHttpException("El archivo no tiene enlace de descarga.", false);

        Directory.CreateDirectory(Path.GetDirectoryName(item.Destination)!);
        var part = item.Destination + ".part";
        var existing = File.Exists(part) ? new FileInfo(part).Length : 0;

        using var response = await SendWithRedirectsAsync(item.Url, existing, ct).ConfigureAwait(false);

        switch (response.StatusCode)
        {
            case HttpStatusCode.RequestedRangeNotSatisfiable:
                if (existing > 0 && item.TotalBytes > 0 && existing >= item.TotalBytes)
                {
                    item.ReceivedBytes = existing; // el parcial ya estaba completo: solo falta verificar
                    return;
                }
                File.Delete(part);
                throw new DownloadHttpException("El archivo parcial no era valido: se reintenta desde cero.", true);

            case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                throw new DownloadHttpException(
                    "Civitai exige una API Key valida para este archivo (o es de acceso anticipado). " +
                    "Guarda tu clave en Configuracion > Civitai.", false);

            case HttpStatusCode.NotFound:
                throw new DownloadHttpException("El archivo ya no esta disponible en Civitai.", false);

            case HttpStatusCode.TooManyRequests:
                throw new DownloadHttpException("Civitai limita las descargas ahora mismo.", true);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new DownloadHttpException($"Civitai respondio con error {(int)response.StatusCode}.", (int)response.StatusCode >= 500);
        }

        var resumed = response.StatusCode == HttpStatusCode.PartialContent && existing > 0;
        if (!resumed) existing = 0; // un 200 significa descarga completa: se empieza de cero

        var total = response.Content.Headers.ContentRange?.Length
                    ?? (response.Content.Headers.ContentLength is { } length ? length + existing : item.TotalBytes);
        if (total > 0) item.TotalBytes = total;

        var received = existing;
        item.ReceivedBytes = received;

        await using var network = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var file = new FileStream(part, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write,
            FileShare.Read, 81920, useAsync: true);

        var buffer = new byte[81920];
        var clock = System.Diagnostics.Stopwatch.StartNew();
        long lastNotify = 0;
        int read;

        while ((read = await network.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
        {
            await _limiter.WaitAsync(read, ct).ConfigureAwait(false);
            await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            received += read;

            if (clock.ElapsedMilliseconds - lastNotify >= 250)
            {
                item.ReceivedBytes = received;
                lastNotify = clock.ElapsedMilliseconds;
            }
        }

        item.ReceivedBytes = received;

        if (total > 0 && received != total)
        {
            throw new IOException($"Descarga incompleta ({ModelEntry.FormatSize(received)} de {ModelEntry.FormatSize(total)}).");
        }
    }

    private async Task FinishAsync(DownloadItem item, CancellationToken ct)
    {
        var part = item.Destination + ".part";
        if (!File.Exists(part)) throw new IOException("Falta el archivo temporal de la descarga.");

        if (!string.IsNullOrEmpty(item.Sha256))
        {
            item.State = DownloadState.Verifying;

            string actual;
            await using (var stream = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            {
                actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
            }

            if (!string.Equals(actual, item.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(part);
                throw new DownloadHttpException(
                    "La verificacion SHA256 fallo: el archivo descargado no coincide con el que publica Civitai. Se ha descartado.", false);
            }
        }

        if (File.Exists(item.Destination))
        {
            throw new IOException($"Ya existe \"{Path.GetFileName(item.Destination)}\" en el destino: no se sobrescribe. " +
                                  "El archivo descargado se conserva con extension .part.");
        }

        File.Move(part, item.Destination);

        item.ReceivedBytes = item.TotalBytes > 0 ? item.TotalBytes : item.ReceivedBytes;
        item.State = DownloadState.Completed;

        ModelInfoStore.Instance.Set(new ModelInfo
        {
            Name = Path.GetFileNameWithoutExtension(item.Destination),
            CivitaiModelId = item.ModelId,
            CivitaiVersionId = item.VersionId,
            TrainedWords = item.TrainedWords
        });

        LogService.Info($"Descarga completada: {item.Destination}");
        ItemCompleted?.Invoke(item);
    }

    // ===================== Persistencia =====================

    private void Load()
    {
        try
        {
            if (!File.Exists(StatePath)) return;

            var items = JsonSerializer.Deserialize<List<DownloadItem>>(File.ReadAllText(StatePath)) ?? new List<DownloadItem>();
            foreach (var item in items)
            {
                if (item.State is DownloadState.Downloading or DownloadState.Queued or DownloadState.Verifying)
                {
                    // Interrumpida por el cierre: queda pausada, el usuario decide cuando seguir.
                    item.State = DownloadState.Paused;
                    item.Error = "interrumpida al cerrar el Launcher: pulsa Reanudar";
                }

                var part = item.Destination + ".part";
                if (item.State == DownloadState.Paused && File.Exists(part)) item.ReceivedBytes = new FileInfo(part).Length;

                Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer models-downloads.json", ex);
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(AppSettingsService.AppFolder);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(Items.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo guardar models-downloads.json", ex);
        }
    }

    // ===================== Limite de velocidad global =====================

    private sealed class SpeedLimiter
    {
        private readonly object _gate = new();
        private long _limit;
        private DateTime _windowStart = DateTime.UtcNow;
        private long _windowBytes;

        public long LimitBytesPerSec
        {
            set { lock (_gate) _limit = value; }
        }

        public async Task WaitAsync(int bytes, CancellationToken ct)
        {
            var delay = TimeSpan.Zero;

            lock (_gate)
            {
                if (_limit <= 0) return;

                var now = DateTime.UtcNow;
                if ((now - _windowStart).TotalSeconds >= 1)
                {
                    _windowStart = now;
                    _windowBytes = 0;
                }

                _windowBytes += bytes;
                if (_windowBytes > _limit)
                {
                    delay = _windowStart.AddSeconds(1) - now;
                }
            }

            if (delay > TimeSpan.Zero) await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }
}
