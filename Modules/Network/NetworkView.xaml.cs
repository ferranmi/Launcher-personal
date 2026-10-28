using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Microsoft.Win32;

namespace Launcher.Modules.Network;

public partial class NetworkView : UserControl
{
    private readonly TailscaleService _tailscale = new();
    private readonly FileTransferService _transfer = new();
    private readonly DispatcherTimer _timer;
    private readonly ObservableCollection<StagedFile> _staged = new();

    private NetworkDevice? _target;
    private bool _refreshing;
    private bool _sending;
    private CancellationTokenSource? _sendCts;

    public NetworkView()
    {
        InitializeComponent();

        StagedItems.ItemsSource = _staged;
        _staged.CollectionChanged += (_, _) => UpdateStagingSummary();

        // "tailscale status --json" es barato: 10 s basta para ver quien se conecta o desconecta.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _timer.Tick += async (_, _) => await RefreshAsync();

        Loaded += async (_, _) =>
        {
            await RefreshAsync();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    // ===================== Lista de dispositivos =====================

    private async Task RefreshAsync()
    {
        // No se reconstruye la lista mientras hay un envio en curso.
        if (_refreshing || _sending) return;
        _refreshing = true;

        try
        {
            var (devices, error) = await _tailscale.GetDevicesAsync();

            if (error != null)
            {
                DevicesItems.ItemsSource = null;
                EmptyText.Text = $"> {error}";
                EmptyState.Visibility = Visibility.Visible;
                CountText.Text = "";
                return;
            }

            DevicesItems.ItemsSource = devices;
            EmptyText.Text = "> sin dispositivos_";
            EmptyState.Visibility = devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CountText.Text = $"{devices.Count} dispositivos \u00b7 {devices.Count(d => d.IsOnline)} online";

            // El panel de envio sigue apuntando al mismo dispositivo aunque se haya
            // reconstruido la lista: se vuelve a buscar por IP para tener su estado al dia.
            if (_target != null)
            {
                _target = devices.FirstOrDefault(d => d.Ip == _target.Ip);
                if (_target == null) CloseStaging();
                else UpdateStagingHeader();
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo al refrescar la lista de Red", ex);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private static NetworkDevice? DeviceOf(object sender) => (sender as FrameworkElement)?.DataContext as NetworkDevice;

    // ===================== Tarjetas: abrir el panel de preparacion =====================

    private void Prepare_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceOf(sender) is { CanSend: true } device) OpenStaging(device, null);
    }

    private void Card_DragOver(object sender, DragEventArgs e)
    {
        var allowed = !_sending && DeviceOf(sender) is { CanSend: true } && e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = allowed ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Card_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (_sending || DeviceOf(sender) is not { CanSend: true } device) return;

        // Soltar sobre una tarjeta no envia: prepara los ficheros para revisarlos antes.
        OpenStaging(device, e.Data.GetData(DataFormats.FileDrop) as string[]);
    }

    // ===================== Panel de ficheros preparados =====================

    private void OpenStaging(NetworkDevice device, IEnumerable<string>? files)
    {
        if (_sending) return;

        // Si se cambia de dispositivo, la lista ya preparada se conserva.
        _target = device;
        StagingPanel.Visibility = Visibility.Visible;
        UpdateStagingHeader();

        if (files != null) AddFiles(files);
        UpdateStagingSummary();
    }

    private void CloseStaging()
    {
        _target = null;
        _staged.Clear();
        StagingPanel.Visibility = Visibility.Collapsed;
    }

    private void CloseStaging_Click(object sender, RoutedEventArgs e)
    {
        if (!_sending) CloseStaging();
    }

    private void UpdateStagingHeader()
    {
        if (_target == null) return;

        TargetText.Text = _target.Name;
        TargetHint.Text = _target.CanSend ? "" : "Este dispositivo no esta disponible ahora mismo.";
        UpdateStagingSummary();
    }

    private void UpdateStagingSummary()
    {
        var total = _staged.Sum(f => f.SizeBytes);

        SummaryText.Text = _staged.Count == 0
            ? "ningun fichero preparado"
            : $"{_staged.Count} fichero(s) \u00b7 {StagedFile.FormatSize(total)}  \u00b7  LocalSend si responde; si no, Tailscale";

        StagedEmptyHint.Visibility = _staged.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearButton.IsEnabled = _staged.Count > 0 && !_sending;
        SendButton.Content = _staged.Count > 0 ? $"Enviar {_staged.Count}" : "Enviar";
        SendButton.IsEnabled = _staged.Count > 0 && _target is { CanSend: true } && !_sending;
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        var skippedFolders = false;

        foreach (var path in paths)
        {
            if (Directory.Exists(path)) { skippedFolders = true; continue; }
            if (!File.Exists(path)) continue;
            if (_staged.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase))) continue;

            var file = new StagedFile(path);
            _staged.Add(file);
            _ = LoadThumbnailAsync(file);
        }

        if (skippedFolders) StatusText.Text = "Las carpetas aun no se pueden enviar: se han ignorado.";
    }

    private static async Task LoadThumbnailAsync(StagedFile file)
    {
        // Se llama desde la UI: al volver del await se asigna ya en el hilo correcto.
        file.Thumbnail = await ShellThumbnailService.GetAsync(file.Path, 128);
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Anadir ficheros para enviar", Multiselect = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) AddFiles(dialog.FileNames);
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => _staged.Clear();

    private void RemoveStaged_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is StagedFile file) _staged.Remove(file);
    }

    private void Staging_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = !_sending && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Staging_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (_sending) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] dropped) AddFiles(dropped);
    }

    // ===================== Envio =====================

    private async void SendStaged_Click(object sender, RoutedEventArgs e)
    {
        if (_sending || _target is not { CanSend: true } device || _staged.Count == 0) return;

        var ok = await SendAsync(device, _staged.Select(f => f.Path).ToList());

        // Si ha ido bien se cierra el panel; si no, la lista se conserva para reintentar.
        if (ok) CloseStaging();
    }

    private async Task<bool> SendAsync(NetworkDevice device, IReadOnlyList<string> files)
    {
        _sending = true;
        _sendCts = new CancellationTokenSource();

        DevicesItems.IsEnabled = false;
        StagingPanel.IsEnabled = false;
        SendProgress.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Visible;

        // Se crea en el hilo de la UI: sus avisos llegan ya en ese hilo.
        var progress = new Progress<string>(message => StatusText.Text = message);
        var success = false;

        try
        {
            var result = await _transfer.SendAsync(device, files, progress, _sendCts.Token);
            success = result.Success;
            StatusText.Text = result.Success
                ? $"OK \u00b7 {result.Method}: {result.Message}"
                : $"Error \u00b7 {result.Method}: {result.Message}";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Envio cancelado.";
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo inesperado al enviar ficheros", ex);
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _sendCts?.Dispose();
            _sendCts = null;
            _sending = false;

            DevicesItems.IsEnabled = true;
            StagingPanel.IsEnabled = true;
            SendProgress.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;
            UpdateStagingSummary();
        }

        return success;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _sendCts?.Cancel();
}
