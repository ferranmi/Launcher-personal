using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Launcher.Core.Models;
using Launcher.Core.Mvvm;
using NAudio.CoreAudioApi;
using Icon = System.Drawing.Icon;
using NAudio.CoreAudioApi.Interfaces;

namespace Launcher.Core.Services;

/// <summary>
/// Enumera las sesiones de audio activas en el dispositivo de reproduccion por
/// defecto (una por cada app que esta sonando o ha sonado recientemente: Spotify,
/// el navegador, un juego...) usando NAudio/CoreAudio, y expone una
/// ObservableCollection para que la UI del mezclador reaccione sola.
///
/// Tambien expone el volumen maestro del dispositivo y vuelve a comprobar en
/// cada Refresh() cual es el dispositivo de salida por defecto: si el usuario
/// cambia de auriculares a altavoces (o viceversa) en Windows, el mezclador
/// cambia de dispositivo solo en el siguiente ciclo.
/// </summary>
public class AudioMixerService : ViewModelBase
{
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private readonly Dictionary<int, AudioSessionInfo> _byProcessId = new();

    public ObservableCollection<AudioSessionInfo> Sessions { get; } = new();

    private float _masterVolume = 1f;
    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (!SetField(ref _masterVolume, clamped)) return;
            try { if (_device != null) _device.AudioEndpointVolume.MasterVolumeLevelScalar = clamped; }
            catch { /* dispositivo desconectado justo al escribir */ }
        }
    }

    private bool _masterMuted;
    public bool MasterMuted
    {
        get => _masterMuted;
        set
        {
            if (!SetField(ref _masterMuted, value)) return;
            try { if (_device != null) _device.AudioEndpointVolume.Mute = value; }
            catch { /* dispositivo desconectado justo al escribir */ }
        }
    }

    private string _deviceName = "";
    public string DeviceName { get => _deviceName; private set => SetField(ref _deviceName, value); }

    public void Initialize()
    {
        _enumerator = new MMDeviceEnumerator();
        SwitchToDefaultDevice();
    }

    /// <summary>Vuelve a leer las sesiones activas: comprueba si el dispositivo de
    /// salida por defecto ha cambiado, sincroniza el volumen maestro, anade las
    /// sesiones nuevas, actualiza volumen/mute de las conocidas y retira las de
    /// procesos ya cerrados o sesiones expiradas.</summary>
    public void Refresh()
    {
        if (_enumerator == null) return;

        if (HasDefaultDeviceChanged())
        {
            SwitchToDefaultDevice();
        }

        if (_device == null) return;

        SyncMasterVolumeFromDevice();

        SessionCollection sessionCollection;
        try
        {
            sessionCollection = _device.AudioSessionManager.Sessions;
        }
        catch
        {
            return; // dispositivo desconectado entre refrescos
        }

        var seenProcessIds = new HashSet<int>();

        for (var i = 0; i < sessionCollection.Count; i++)
        {
            AudioSessionControl session;
            int processId;
            try
            {
                session = sessionCollection[i];

                // Las sesiones expiradas ya no representan una app activa (Windows
                // las mantiene un rato tras cerrar el proceso); las ignoramos para
                // que no se queden "fantasma" en la lista.
                if (session.State == AudioSessionState.AudioSessionStateExpired) continue;

                processId = (int)session.GetProcessID;
            }
            catch
            {
                continue;
            }

            if (processId == 0) continue; // sesion del sistema, sin app asociada
            seenProcessIds.Add(processId);

            if (_byProcessId.TryGetValue(processId, out var existing))
            {
                existing.RefreshFromSession();
                continue;
            }

            var info = BuildSessionInfo(session, processId);
            if (info == null) continue;

            _byProcessId[processId] = info;
            Sessions.Add(info);
        }

        var stale = _byProcessId.Keys.Where(pid => !seenProcessIds.Contains(pid)).ToList();
        foreach (var pid in stale)
        {
            if (_byProcessId.Remove(pid, out var info))
            {
                Sessions.Remove(info);
            }
        }
    }

    /// <summary>Comprueba si el dispositivo de salida por defecto de Windows ha
    /// cambiado desde la ultima vez (p.ej. el usuario conecto/desconecto unos
    /// auriculares Bluetooth). Comparar el Id es barato, asi que se hace en cada
    /// ciclo de refresco sin problema de rendimiento.</summary>
    private bool HasDefaultDeviceChanged()
    {
        if (_enumerator == null) return false;

        try
        {
            using var current = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return _device == null || current.ID != _device.ID;
        }
        catch
        {
            return _device != null; // ya no hay dispositivo por defecto: forzamos re-inicializar
        }
    }

    private void SwitchToDefaultDevice()
    {
        _byProcessId.Clear();
        Sessions.Clear();

        try
        {
            _device = _enumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            DeviceName = _device?.FriendlyName ?? "";
            SyncMasterVolumeFromDevice();
        }
        catch
        {
            // Sin dispositivo de salida (poco comun): el mezclador se queda vacio
            // en vez de tumbar la app, y lo reintentara en el siguiente Refresh().
            _device = null;
            DeviceName = "";
        }
    }

    /// <summary>Lee el volumen/mute reales del dispositivo sin volver a escribir
    /// sobre el (evita bucles si el usuario cambia el volumen con las teclas del
    /// teclado o el icono de Windows en vez de desde nuestro mezclador).</summary>
    private void SyncMasterVolumeFromDevice()
    {
        if (_device == null) return;

        try
        {
            var vol = _device.AudioEndpointVolume.MasterVolumeLevelScalar;
            var muted = _device.AudioEndpointVolume.Mute;

            if (Math.Abs(vol - _masterVolume) > 0.001f)
            {
                _masterVolume = vol;
                OnPropertyChanged(nameof(MasterVolume));
            }

            if (muted != _masterMuted)
            {
                _masterMuted = muted;
                OnPropertyChanged(nameof(MasterMuted));
            }
        }
        catch
        {
            // ignorar: se reintentara en el proximo ciclo
        }
    }

    private static AudioSessionInfo? BuildSessionInfo(AudioSessionControl session, int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var processName = process.ProcessName;

            var displayName = SafeDisplayName(session);
            if (string.IsNullOrWhiteSpace(displayName)) displayName = FriendlyProcessName(processName);

            var icon = TryExtractIcon(process);

            return new AudioSessionInfo(session, processId, processName, displayName, icon);
        }
        catch
        {
            return null;
        }
    }

    private static string SafeDisplayName(AudioSessionControl session)
    {
        try { return session.DisplayName ?? ""; }
        catch { return ""; }
    }

    private static BitmapSource? TryExtractIcon(Process process)
    {
        try
        {
            var modulePath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(modulePath) || !File.Exists(modulePath)) return null;

            using var extracted = Icon.ExtractAssociatedIcon(modulePath);
            if (extracted == null) return null;

            var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                extracted.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static string FriendlyProcessName(string processName)
    {
        var lower = processName.ToLowerInvariant();
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("msedge")) return "Edge";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("discord")) return "Discord";
        if (lower.Contains("vlc")) return "VLC";
        return processName;
    }
}