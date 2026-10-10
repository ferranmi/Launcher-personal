using System.Windows.Media.Imaging;
using Launcher.Core.Mvvm;

namespace Launcher.Core.Models;

/// <summary>
/// Estado actual de reproduccion, expuesto como view model para que MediaView
/// se actualice solo via bindings cuando NowPlayingService refresca los datos.
/// </summary>
public class NowPlayingInfo : ViewModelBase
{
    private bool _hasSession;
    public bool HasSession { get => _hasSession; set => SetField(ref _hasSession, value); }

    private string _title = "";
    public string Title { get => _title; set => SetField(ref _title, value); }

    private string _artist = "";
    public string Artist { get => _artist; set => SetField(ref _artist, value); }

    private string _sourceApp = "";
    public string SourceApp { get => _sourceApp; set => SetField(ref _sourceApp, value); }

    private bool _isPlaying;
    public bool IsPlaying { get => _isPlaying; set => SetField(ref _isPlaying, value); }

    private BitmapImage? _albumArt;
    public BitmapImage? AlbumArt
    {
        get => _albumArt;
        set { SetField(ref _albumArt, value); OnPropertyChanged(nameof(HasAlbumArt)); }
    }

    public bool HasAlbumArt => AlbumArt != null;

    private double _positionSeconds;
    public double PositionSeconds
    {
        get => _positionSeconds;
        set { SetField(ref _positionSeconds, value); OnPropertyChanged(nameof(PositionText)); }
    }

    private double _durationSeconds;
    public double DurationSeconds
    {
        get => _durationSeconds;
        set { SetField(ref _durationSeconds, value); OnPropertyChanged(nameof(DurationText)); }
    }

    public string PositionText => FormatTime(PositionSeconds);
    public string DurationText => FormatTime(DurationSeconds);

    private static string FormatTime(double totalSeconds)
    {
        if (double.IsNaN(totalSeconds) || totalSeconds < 0) totalSeconds = 0;
        var t = TimeSpan.FromSeconds(totalSeconds);
        return t.Hours > 0 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }
}