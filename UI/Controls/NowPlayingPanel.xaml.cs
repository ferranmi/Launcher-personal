using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.UI.Controls;

/// <summary>
/// Reproductor reutilizable (caratula, titulo, artista, progreso y controles). Tiene su propio
/// NowPlayingService y su timer de 1 s, pero el timer solo corre mientras el control es
/// visible: oculto o fuera de pantalla no consume nada.
/// </summary>
public partial class NowPlayingPanel : UserControl
{
    private readonly NowPlayingService _nowPlaying = new();
    private readonly NowPlayingInfo _info;
    private readonly DispatcherTimer _timer;

    private bool _initialized;
    private bool _dragging;
    private bool _busy;

    public NowPlayingPanel()
    {
        InitializeComponent();

        _info = _nowPlaying.Info;
        Root.DataContext = _info;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await RefreshAsync();

        Loaded += async (_, _) =>
        {
            if (!_initialized)
            {
                _initialized = true;
                await _nowPlaying.InitializeAsync();
            }

            UpdateTimer();
        };
        Unloaded += (_, _) => _timer.Stop();
        IsVisibleChanged += (_, _) => UpdateTimer();
    }

    private async void UpdateTimer()
    {
        if (IsLoaded && IsVisible)
        {
            if (!_timer.IsEnabled) _timer.Start();
            await RefreshAsync();
        }
        else
        {
            _timer.Stop();
        }
    }

    private async Task RefreshAsync()
    {
        if (_busy || _dragging) return;
        _busy = true;

        try
        {
            await _nowPlaying.RefreshAsync();
            ProgressSlider.Maximum = Math.Max(_info.DurationSeconds, 0.01);
            ProgressSlider.Value = _info.PositionSeconds;
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo el refresco del reproductor", ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private async void PlayPause_Click(object sender, RoutedEventArgs e) => await _nowPlaying.TogglePlayPauseAsync();
    private async void Next_Click(object sender, RoutedEventArgs e) => await _nowPlaying.NextAsync();
    private async void Previous_Click(object sender, RoutedEventArgs e) => await _nowPlaying.PreviousAsync();

    private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _dragging = true;

    private async void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        await _nowPlaying.SeekAsync(ProgressSlider.Value);
    }
}
