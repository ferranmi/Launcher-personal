using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.Music;

public partial class DownloadWindow : Window
{
    private CancellationTokenSource? _cts;
    private MusicPreview? _preview;

    private readonly MediaPlayer _player = new();
    private bool _listening;

    /// <summary>Rutas de las canciones guardadas en esta sesion. Quien abre la ventana las
    /// importa a la biblioteca al cerrarse.</summary>
    public List<string> Downloaded { get; } = new();

    public DownloadWindow()
    {
        InitializeComponent();

        Loaded += (_, _) => UrlBox.Focus();

        _player.MediaEnded += (_, _) => StopListening();
        _player.MediaFailed += (_, _) =>
        {
            StopListening();
            AppendLog("No se ha podido reproducir la vista previa. Puedes guardar la cancion igualmente.");
        };

        Closing += (_, _) =>
        {
            _cts?.Cancel(); // cerrar con una operacion en curso la cancela
            StopListening();
            _player.Close();
        };

        if (!ExternalToolsService.Instance.IsReady)
        {
            AppendLog("Primera vez: el Launcher descargara yt-dlp y FFmpeg (unos 100 MB) en su carpeta de datos.");
        }
    }

    private void SetBusy(bool busy)
    {
        SearchButton.IsEnabled = !busy;
        UpdateButton.IsEnabled = !busy;
        UrlBox.IsEnabled = !busy;
        PreviewPanel.IsEnabled = !busy;
        CancelRunButton.IsEnabled = busy;
    }

    private void AppendLog(string line)
    {
        LogBox.AppendText(line + Environment.NewLine);

        // yt-dlp emite muchas lineas de progreso: nos quedamos solo con el final.
        if (LogBox.Text.Length > 20000)
        {
            LogBox.Text = LogBox.Text[^10000..];
        }

        LogBox.ScrollToEnd();
    }

    // ===================== Paso 1: buscar y previsualizar =====================

    private void UrlBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SearchButton.IsEnabled) Search_Click(sender, e);
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        // Puede ser un enlace (https://...) o texto como "artista - cancion".
        var input = UrlBox.Text.Trim();

        if (input.Length == 0)
        {
            MessageBox.Show(this, "Pega un enlace o escribe \"artista - cancion\".",
                "Falta informacion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var tools = ExternalToolsService.Instance;
        if (!tools.IsReady)
        {
            var confirm = MessageBox.Show(this,
                "Para buscar y descargar musica el Launcher necesita bajarse yt-dlp y FFmpeg (unos 100 MB, solo la primera vez). Continuar?",
                "Herramientas necesarias", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }

        StopListening();
        HidePreview();

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        // Progress<T> creado en el hilo de la UI: sus informes llegan aqui ya marshalizados.
        var log = new Progress<string>(AppendLog);

        SetBusy(true);
        try
        {
            await tools.EnsureAsync(log, ct);

            _preview = await MusicAutoService.PrepareAsync(input, log, ct);
            ShowPreview(_preview);
            AppendLog("Revisa la ficha y pulsa \"Guardar en la biblioteca\" cuando este bien.");
        }
        catch (OperationCanceledException)
        {
            AppendLog("Cancelado.");
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la busqueda de musica", ex);
            AppendLog("Error: " + ex.Message);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    private void ShowPreview(MusicPreview preview)
    {
        var m = preview.Meta;

        TitleBox.Text = m.Title;
        ArtistBox.Text = m.Artist;
        AlbumBox.Text = m.Album;
        YearBox.Text = m.Year > 0 ? m.Year.ToString() : "";

        VerifiedText.Text = m.Verified
            ? "Verificado en MusicBrainz"
            : "Sin verificar: revisa los datos antes de guardar";
        VerifiedText.Foreground = (Brush)FindResource(m.Verified ? "Brush.Accent" : "Brush.Warning");

        SourceCombo.ItemsSource = preview.Sources.Select(Describe).ToList();
        SourceCombo.SelectedIndex = 0;
        SourcePanel.Visibility = preview.Sources.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        ListenButton.Content = "Escuchar";
        UpdateCover();
        PreviewPanel.Visibility = Visibility.Visible;
    }

    private void HidePreview()
    {
        PreviewPanel.Visibility = Visibility.Collapsed;
        CoverImage.Source = null;
        _preview = null;
    }

    private static string Describe(MusicSearchResult r)
    {
        var duration = r.Duration is { } d ? TimeSpan.FromSeconds(d).ToString(@"m\:ss") : "?";
        return $"{r.Title}  \u00b7  {r.Channel}  \u00b7  {duration}";
    }

    private MusicSearchResult? SelectedSource()
    {
        if (_preview == null) return null;
        var i = SourceCombo.SelectedIndex;
        return i >= 0 && i < _preview.Sources.Count ? _preview.Sources[i] : null;
    }

    /// <summary>Caratula oficial si MusicBrainz la tiene; si no, la miniatura del video elegido.</summary>
    private void UpdateCover()
    {
        if (_preview == null) return;

        try
        {
            if (_preview.Meta.CoverBytes is { } bytes)
            {
                using var ms = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 220;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                CoverImage.Source = bitmap;
                CoverText.Text = "Caratula oficial";
                return;
            }

            var source = SelectedSource();
            var thumb = source != null ? MusicAutoService.ThumbnailUrl(source.Url) : null;
            if (thumb != null)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.DecodePixelWidth = 220;
                bitmap.UriSource = new Uri(thumb);
                bitmap.EndInit();

                CoverImage.Source = bitmap;
                CoverText.Text = "Miniatura del video";
                return;
            }
        }
        catch
        {
            // Imagen ilegible o sin red: se muestra sin caratula.
        }

        CoverImage.Source = null;
        CoverText.Text = "Sin caratula";
    }

    private void SourceCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_preview == null) return;
        StopListening(); // otra version: la vista previa en marcha ya no corresponde
        UpdateCover();
    }

    // ===================== Escuchar un fragmento =====================

    private async void Listen_Click(object sender, RoutedEventArgs e)
    {
        if (_listening)
        {
            StopListening();
            return;
        }

        var source = SelectedSource();
        if (source == null) return;

        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var ct = _cts.Token;

        SetBusy(true);
        try
        {
            AppendLog("Cargando vista previa...");
            var streamUrl = await MusicDownloadService.GetStreamUrlAsync(source.Url, ct);

            if (streamUrl == null)
            {
                AppendLog("No he podido obtener el audio para la vista previa.");
                return;
            }

            _player.Open(new Uri(streamUrl));
            _player.Play();

            _listening = true;
            ListenButton.Content = "Detener";
        }
        catch (OperationCanceledException)
        {
            AppendLog("Vista previa cancelada o demasiado lenta.");
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la vista previa de audio", ex);
            AppendLog("Error: " + ex.Message);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    private void StopListening()
    {
        if (!_listening) return;

        _player.Stop();
        _listening = false;
        ListenButton.Content = "Escuchar";
    }

    // ===================== Paso 2: guardar =====================

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var source = SelectedSource();
        if (_preview == null || source == null) return;

        var title = TitleBox.Text.Trim();
        if (title.Length == 0)
        {
            MessageBox.Show(this, "La cancion necesita un titulo.", "Falta informacion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        uint year = 0;
        if (!string.IsNullOrWhiteSpace(YearBox.Text) && !uint.TryParse(YearBox.Text.Trim(), out year))
        {
            MessageBox.Show(this, "El ano debe ser un numero (por ejemplo, 2019).", "Dato no valido",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StopListening();

        // Se guardan los datos tal y como han quedado en la ficha (el usuario puede haberlos corregido).
        var meta = new MusicMetadata
        {
            Title = title,
            Artist = ArtistBox.Text.Trim(),
            Album = AlbumBox.Text.Trim(),
            Year = year,
            CoverBytes = _preview.Meta.CoverBytes,
            Verified = _preview.Meta.Verified
        };

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var log = new Progress<string>(AppendLog);

        SetBusy(true);
        try
        {
            var files = await MusicAutoService.DownloadAsync(
                source, meta, MusicLibraryService.Instance.Folder, log, ct);

            Downloaded.AddRange(files);
            AppendLog($"Listo: {files.Count} cancion(es) guardada(s).");

            HidePreview();
            UrlBox.Clear();
        }
        catch (OperationCanceledException)
        {
            AppendLog("Cancelado.");
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la descarga de musica", ex);
            AppendLog("Error: " + ex.Message);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    // ===================== Herramientas / cierre =====================

    private async void UpdateTool_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        var log = new Progress<string>(AppendLog);

        SetBusy(true);
        try
        {
            await ExternalToolsService.Instance.UpdateYtDlpAsync(log, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Cancelado.");
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la actualizacion de yt-dlp", ex);
            AppendLog("Error: " + ex.Message);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    private void CancelRun_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = Downloaded.Count > 0;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
