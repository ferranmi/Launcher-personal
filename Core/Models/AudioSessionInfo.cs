using System.Windows.Media.Imaging;
using Launcher.Core.Mvvm;
using NAudio.CoreAudioApi;

namespace Launcher.Core.Models;

/// <summary>
/// Representa el volumen de una app individual en el mezclador (una fila por
/// cada proceso con una sesion de audio activa en el dispositivo de salida por
/// defecto). Envuelve el AudioSessionControl de NAudio: cambiar Volume/IsMuted
/// desde la UI escribe directamente sobre esa sesion en tiempo real.
/// </summary>
public class AudioSessionInfo : ViewModelBase
{
    private readonly AudioSessionControl _session;

    public int ProcessId { get; }
    public string ProcessName { get; }
    public string DisplayName { get; }
    public BitmapSource? Icon { get; }

    private float _volume = 1f;
    public float Volume
    {
        get => _volume;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (!SetField(ref _volume, clamped)) return;
            try { _session.SimpleAudioVolume.Volume = clamped; } catch { /* sesion cerrada entretanto */ }
        }
    }

    private bool _isMuted;
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (!SetField(ref _isMuted, value)) return;
            try { _session.SimpleAudioVolume.Mute = value; } catch { /* sesion cerrada entretanto */ }
        }
    }

    public AudioSessionInfo(AudioSessionControl session, int processId, string processName, string displayName, BitmapSource? icon)
    {
        _session = session;
        ProcessId = processId;
        ProcessName = processName;
        DisplayName = displayName;
        Icon = icon;

        try
        {
            _volume = _session.SimpleAudioVolume.Volume;
            _isMuted = _session.SimpleAudioVolume.Mute;
        }
        catch
        {
            _volume = 1f;
        }
    }

    /// <summary>Sincroniza Volume/IsMuted con el estado real de la sesion sin
    /// volver a escribir sobre ella (evita bucles al refrescar cada segundo si
    /// el usuario ha cambiado el volumen desde el mezclador nativo de Windows).</summary>
    public void RefreshFromSession()
    {
        try
        {
            var vol = _session.SimpleAudioVolume.Volume;
            var muted = _session.SimpleAudioVolume.Mute;

            if (Math.Abs(vol - _volume) > 0.001f)
            {
                _volume = vol;
                OnPropertyChanged(nameof(Volume));
            }

            if (muted != _isMuted)
            {
                _isMuted = muted;
                OnPropertyChanged(nameof(IsMuted));
            }
        }
        catch
        {
            // Si la sesion ya no responde, AudioMixerService la retirara en el
            // proximo Refresh() al no aparecer en la enumeracion.
        }
    }
}