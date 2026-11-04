using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

/// <summary>Vista de la cola de descargas. No contiene logica de descarga: solo muestra el estado de
/// DownloadQueueService y le pasa las ordenes (pausar, reanudar, cancelar...).</summary>
public partial class DownloadsPanel : UserControl
{
    private readonly DownloadQueueService _queue = DownloadQueueService.Instance;

    public DownloadsPanel()
    {
        InitializeComponent();

        DownloadsList.ItemsSource = _queue.Items;
        _queue.Items.CollectionChanged += (_, _) => UpdateSummary();
        _queue.SummaryChanged += UpdateSummary;

        UpdateSummary();
    }

    private Window? Owner => Window.GetWindow(this);

    private void UpdateSummary()
    {
        var items = _queue.Items;
        var active = items.Count(i => i.State == DownloadState.Downloading);
        var queued = items.Count(i => i.State == DownloadState.Queued);
        var paused = items.Count(i => i.State == DownloadState.Paused);
        var failed = items.Count(i => i.State == DownloadState.Failed);
        var speed = items.Where(i => i.State == DownloadState.Downloading).Sum(i => i.SpeedBps);

        var text = $"{active} activas  \u00b7  {queued} en cola  \u00b7  {paused} pausadas  \u00b7  {failed} con error";
        if (speed > 0) text += $"  \u00b7  {ModelEntry.FormatSize((long)speed)}/s";

        SummaryText.Text = text;
        EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static DownloadItem? Of(object sender) => (sender as FrameworkElement)?.DataContext as DownloadItem;

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (Of(sender) is { } item) _queue.Pause(item);
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        if (Of(sender) is { } item) _queue.Resume(item);
    }

    private void ToEnd_Click(object sender, RoutedEventArgs e)
    {
        if (Of(sender) is { } item) _queue.MoveToEnd(item);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (Of(sender) is not { } item) return;

        var answer = MessageBox.Show(Owner,
            $"Se cancelara la descarga de \"{item.ModelName}\" y se borrara lo ya descargado. Continuar?",
            "Cancelar descarga", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes) _queue.Cancel(item);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (Of(sender) is not { } item) return;

        if (item.State is DownloadState.Paused or DownloadState.Failed)
        {
            var answer = MessageBox.Show(Owner,
                "Se quitara de la lista y se borrara lo ya descargado de forma parcial. Continuar?",
                "Quitar descarga", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
        }

        _queue.Remove(item);
    }

    private void PauseAll_Click(object sender, RoutedEventArgs e) => _queue.PauseAll();
    private void ResumeAll_Click(object sender, RoutedEventArgs e) => _queue.ResumeAll();
    private void ClearFinished_Click(object sender, RoutedEventArgs e) => _queue.ClearFinished();

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Of(sender) is not { } item) return;

        try
        {
            if (File.Exists(item.Destination)) Process.Start("explorer.exe", $"/select,\"{item.Destination}\"");
            else
            {
                var folder = Path.GetDirectoryName(item.Destination);
                if (folder != null && Directory.Exists(folder)) Process.Start("explorer.exe", $"\"{folder}\"");
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo abrir la carpeta de la descarga", ex);
        }
    }

    /// <summary>Copia la linea de uso: la que ya exista en los wildcards, o la etiqueta con las palabras de
    /// activacion de Civitai. No modifica ningun wildcard.</summary>
    private async void CopyLine_Click(object sender, RoutedEventArgs e)
    {
        if (Of(sender) is not { } item) return;

        if (item.TypeLabel == "Checkpoint")
        {
            MessageBox.Show(Owner, "Los checkpoints no se usan con etiquetas en los wildcards.", "Copiar linea",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var name = Path.GetFileNameWithoutExtension(item.Destination);
        var line = await LineInfoService.BuildLineAsync(name, "0.8");

        try
        {
            Clipboard.SetText(line);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo copiar la linea", ex);
        }
    }
}
