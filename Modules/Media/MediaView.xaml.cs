using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.Media;

public partial class MediaView : UserControl
{
    private readonly NowPlayingService _nowPlaying = new();
    private readonly AudioMixerService _mixer = new();
    private readonly NowPlayingInfo _info;
    private readonly DispatcherTimer _timer;

    private bool _isDraggingSlider;
    private bool _isDraggingMaster;
    private bool _isInitialized;

    public MediaView()
    {
        InitializeComponent();

        _info = _nowPlaying.Info;
        DataContext = _info;

        _nowPlaying.TrackChanged += OnTrackChanged;

        MixerItems.ItemsSource = _mixer.Sessions;
        _mixer.Sessions.CollectionChanged += (_, _) => UpdateMixerEmptyState();
        _mixer.PropertyChanged += Mixer_PropertyChanged;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await RefreshAsync();

        Loaded += MediaView_Loaded;
        Unloaded += MediaView_Unloaded;
    }

    private async void MediaView_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            await _nowPlaying.InitializeAsync();
            _mixer.Initialize();
        }

        await RefreshAsync();
        _timer.Start();
    }

    private void MediaView_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
    }

    private async Task RefreshAsync()
    {
        if (!_isDraggingSlider)
        {
            await _nowPlaying.RefreshAsync();
            ProgressSlider.Maximum = Math.Max(_info.DurationSeconds, 0.01);
            ProgressSlider.Value = _info.PositionSeconds;
        }

        _mixer.Refresh();
        UpdateMixerEmptyState();

        DeviceNameText.Text = string.IsNullOrWhiteSpace(_mixer.DeviceName) ? "sin dispositivo" : _mixer.DeviceName;

        if (!_isDraggingMaster)
        {
            MasterVolumeSlider.Value = _mixer.MasterVolume;
        }
        MasterMuteToggle.IsChecked = _mixer.MasterMuted;
    }

    private void UpdateMixerEmptyState()
    {
        MixerEmptyState.Visibility = _mixer.Sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===================== Now Playing =====================

    private async void PlayPause_Click(object sender, RoutedEventArgs e) => await _nowPlaying.TogglePlayPauseAsync();
    private async void Next_Click(object sender, RoutedEventArgs e) => await _nowPlaying.NextAsync();
    private async void Previous_Click(object sender, RoutedEventArgs e) => await _nowPlaying.PreviousAsync();

    private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSlider = true;
    }

    private async void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingSlider) return;
        _isDraggingSlider = false;
        await _nowPlaying.SeekAsync(ProgressSlider.Value);
    }

    /// <summary>Pequeno fundido al cambiar de cancion/video: da sensacion de
    /// transicion en vez de que la caratula y el texto salten de golpe.</summary>
    private void OnTrackChanged()
    {
        Dispatcher.Invoke(() =>
        {
            var fadeIn = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(280));
            NowPlayingContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        });
    }

    /// <summary>Doble clic en la caratula o el badge de la app: intenta traer al
    /// frente la ventana de la app que esta sonando (Spotify, el navegador...).</summary>
    private void SourceBadge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        _nowPlaying.TryBringSourceAppToFront();
    }

    // ===================== Mezclador =====================

    private void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!MasterVolumeSlider.IsMouseCaptureWithin && !MasterVolumeSlider.IsFocused) return;
        _isDraggingMaster = true;
        _mixer.MasterVolume = (float)e.NewValue;
    }

    private void MasterMuteToggle_Click(object sender, RoutedEventArgs e)
    {
        _mixer.MasterMuted = MasterMuteToggle.IsChecked == true;
    }

    private void Mixer_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Reservado por si en el futuro quieres reaccionar a otros cambios del
        // mezclador (p.ej. nombre del dispositivo) directamente desde aqui.
    }
}