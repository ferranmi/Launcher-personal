using System.Windows;
using System.Windows.Input;
using Launcher.Core.Services;

namespace Launcher.Modules.Music;

public partial class DownloadWindow : Window
{
    private CancellationTokenSource? _cts;

    /// <summary>Rutas de las canciones descargadas en esta sesion. Quien abre la
    /// ventana las importa a la biblioteca al cerrarse.</summary>
    public List<string> Downloaded { get; } = new();

    public DownloadWindow()
    {
        InitializeComponent();

        Loaded += (_, _) => UrlBox.Focus();
        Closing += (_, _) => _cts?.Cancel(); // cerrar con una descarga en curso la cancela

        if (!ExternalToolsService.Instance.IsReady)
        {
            AppendLog("Primera vez: el Launcher descargara yt-dlp y FFmpeg (unos 100 MB) en su carpeta de datos.");
        }
    }

    private void SetBusy(bool busy)
    {
        DownloadButton.IsEnabled = !busy;
        UpdateButton.IsEnabled = !busy;
        UrlBox.IsEnabled = !busy;
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

    // ===================== Descargar =====================

    private void UrlBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DownloadButton.IsEnabled) Download_Click(sender, e);
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        // Puede ser un enlace (https://...) o texto como "artista - cancion".
        var url = UrlBox.Text.Trim();

        if (url.Length == 0)
        {
            MessageBox.Show(this, "Pega un enlace o escribe \"artista - cancion\".",
                "Falta informacion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var tools = ExternalToolsService.Instance;
        if (!tools.IsReady)
        {
            var confirm = MessageBox.Show(this,
                "Para descargar musica el Launcher necesita bajarse yt-dlp y FFmpeg (unos 100 MB, solo la primera vez). Continuar?",
                "Herramientas necesarias", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        // Progress<T> creado en el hilo de la UI: sus informes llegan aqui ya marshalizados.
        var log = new Progress<string>(AppendLog);

        SetBusy(true);
        try
        {
            await tools.EnsureAsync(log, ct);

            var files = await MusicAutoService.AddAsync(
                url, MusicLibraryService.Instance.Folder, log, ct);

            Downloaded.AddRange(files);
            AppendLog($"Listo: {files.Count} cancion(es) descargada(s).");
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
