using System.Linq;
using System.IO;
using System.Windows.Media.Imaging;
using Launcher.Core.Models;
using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Runtime.InteropServices;

namespace Launcher.Core.Services;

/// <summary>
/// Envuelve GlobalSystemMediaTransportControlsSessionManager (Windows.Media.Control)
/// para exponer que se esta reproduciendo ahora mismo en el sistema, sin integrar
/// cada app por separado: funciona con Spotify, el reproductor nativo, y con
/// cualquier pestana de navegador que use la Media Session API (YouTube, Netflix,
/// Prime Video, etc), porque todos ellos registran su sesion ante Windows.
/// </summary>

public class NowPlayingService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private string? _lastTrackKey;

    public NowPlayingInfo Info { get; } = new();

    public async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch
        {
            _manager = null;
        }
    }

    /// <summary>Vuelve a leer el estado actual. Pensado para llamarse cada 1s desde la UI.</summary>
    /// <summary>Se dispara cuando cambia la cancion/video actual (tras cargar ya la
    /// caratula), para que la UI pueda animar la transicion.</summary>
    public event Action? TrackChanged;
    private string? _lastSourceAppId;
    public async Task RefreshAsync()
    {
        var session = GetActiveSession();
        if (session == null)
        {
            Info.HasSession = false;
            _lastTrackKey = null;
            return;
        }

        GlobalSystemMediaTransportControlsSessionMediaProperties props;
        try
        {
            props = await session.TryGetMediaPropertiesAsync();
        }
        catch
        {
            Info.HasSession = false;
            return;
        }

        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();

        Info.HasSession = true;
        Info.Title = string.IsNullOrWhiteSpace(props.Title) ? "(sin titulo)" : props.Title;
        Info.Artist = props.Artist ?? "";
        Info.SourceApp = FriendlyAppName(session.SourceAppUserModelId);
        Info.IsPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        var duration = (timeline.EndTime - timeline.StartTime).TotalSeconds;
        var position = (timeline.Position - timeline.StartTime).TotalSeconds;
        Info.DurationSeconds = Math.Max(0, duration);
        Info.PositionSeconds = Math.Max(0, Math.Min(position, Info.DurationSeconds));

        var trackKey = $"{Info.Title}|{Info.Artist}|{session.SourceAppUserModelId}";
        _lastSourceAppId = session.SourceAppUserModelId;

        if (trackKey != _lastTrackKey)
        {
            _lastTrackKey = trackKey;
            Info.AlbumArt = await LoadThumbnailAsync(props.Thumbnail);
            TrackChanged?.Invoke();
        }
    }

    public async Task<bool> TogglePlayPauseAsync()
    {
        var session = GetActiveSession();
        if (session == null) return false;
        try { return await session.TryTogglePlayPauseAsync(); }
        catch { return false; }
    }

    public async Task<bool> NextAsync()
    {
        var session = GetActiveSession();
        if (session == null) return false;
        try { return await session.TrySkipNextAsync(); }
        catch { return false; }
    }

    public async Task<bool> PreviousAsync()
    {
        var session = GetActiveSession();
        if (session == null) return false;
        try { return await session.TrySkipPreviousAsync(); }
        catch { return false; }
    }

    public async Task<bool> SeekAsync(double positionSeconds)
    {
        var session = GetActiveSession();
        if (session == null) return false;
        try { return await session.TryChangePlaybackPositionAsync(TimeSpan.FromSeconds(positionSeconds).Ticks); }
        catch { return false; }
    }

    /// <summary>Intenta traer al frente la ventana de la app que esta sonando
    /// ahora mismo (Spotify, el navegador...). Es un "mejor esfuerzo": si no
    /// encuentra un proceso con ventana visible que coincida, no hace nada.</summary>
    public bool TryBringSourceAppToFront()
    {
        if (string.IsNullOrWhiteSpace(_lastSourceAppId)) return false;

        var lower = _lastSourceAppId.ToLowerInvariant();
        var candidates = new List<string>();

        if (lower.Contains("spotify")) candidates.Add("Spotify");
        else if (lower.Contains("msedge")) candidates.Add("msedge");
        else if (lower.Contains("chrome")) candidates.Add("chrome");
        else if (lower.Contains("firefox")) candidates.Add("firefox");
        else if (lower.Contains("vlc")) candidates.Add("vlc");
        else return false;

        foreach (var name in candidates)
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName(name))
            {
                if (process.MainWindowHandle == IntPtr.Zero) continue;
                NativeMethods.ShowWindow(process.MainWindowHandle, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(process.MainWindowHandle);
                return true;
            }
        }

        return false;
    }

    private static class NativeMethods
    {
        public const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }

    /// <summary>
    /// Elige la sesion "mas relevante" entre todas las apps que reportan estado a
    /// Windows: prioriza la que este reproduciendo activamente (para no mostrar,
    /// p.ej., una pestana de YouTube en pausa en vez de Spotify sonando), y si
    /// nada esta en Play cae a la sesion "actual" de Windows o a la primera disponible.
    /// </summary>
    private GlobalSystemMediaTransportControlsSession? GetActiveSession()
    {
        if (_manager == null) return null;

        var sessions = _manager.GetSessions();
        if (sessions.Count == 0) return null;

        var playing = sessions.FirstOrDefault(s =>
            SafeStatus(s) == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
        if (playing != null) return playing;

        var current = _manager.GetCurrentSession();
        if (current != null) return current;

        return sessions[0];
    }

    private static GlobalSystemMediaTransportControlsSessionPlaybackStatus? SafeStatus(
        GlobalSystemMediaTransportControlsSession session)
    {
        try { return session.GetPlaybackInfo()?.PlaybackStatus; }
        catch { return null; }
    }

    private static async Task<BitmapImage?> LoadThumbnailAsync(IRandomAccessStreamReference? thumbnailRef)
    {
        if (thumbnailRef == null) return null;

        try
        {
            using var stream = await thumbnailRef.OpenReadAsync();

            // No usamos AsStreamForRead() (extension de UWP no disponible en un
            // proyecto .NET normal): leemos el IRandomAccessStream a mano con un
            // DataReader y volcamos los bytes a un MemoryStream de .NET.
            var size = (uint)stream.Size;
            if (size == 0) return null;

            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync(size);

            var bytes = new byte[size];
            reader.ReadBytes(bytes);

            using var memoryStream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = memoryStream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static string FriendlyAppName(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return "";
        var lower = appId.ToLowerInvariant();

        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("msedge")) return "Edge";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("zunemusic") || lower.Contains("groove")) return "Reproductor de Musica";
        if (lower.Contains("wmplayer")) return "Windows Media Player";
        if (lower.Contains("vlc")) return "VLC";

        return appId;
    }
}