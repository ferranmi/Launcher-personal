using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

/// <summary>
/// Explorador de Civitai: busqueda, versiones, archivos y descarga. Nada se descarga sin pasar por la ventana de
/// confirmacion. Las colecciones publicas solo exponen metadatos en la API de Civitai, asi que su contenido se
/// descarga pegando los enlaces de sus modelos.
/// </summary>
public partial class CivitaiPanel : UserControl
{
    private readonly CivitaiService _civitai = CivitaiService.Instance;
    private readonly ObservableCollection<CivitaiModel> _results = new();
    private readonly Dictionary<long, long> _preferredVersion = new();

    private string? _cursor;
    private string? _collectionsCursor;
    private bool _busy;
    private bool _ready;
    private string _mode = "search";
    private string _sourceNote = "";
    private CivitaiModel? _detail;

    /// <summary>Se han anadido descargas a la cola: la ventana principal puede mostrar la pestana Descargas.</summary>
    public event Action? DownloadsQueued;

    public CivitaiPanel()
    {
        InitializeComponent();

        CardsList.ItemsSource = _results;
        _ready = true;
        UpdateEmpty();
    }

    private Window? Owner => Window.GetWindow(this);

    // ===================== Modos =====================

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        _mode = (string)((RadioButton)sender).Tag;
        ApplyMode();
    }

    private void ApplyMode()
    {
        CardsList.Visibility = _mode == "search" ? Visibility.Visible : Visibility.Collapsed;
        CollectionsGrid.Visibility = _mode == "collections" ? Visibility.Visible : Visibility.Collapsed;
        LinksGrid.Visibility = _mode == "links" ? Visibility.Visible : Visibility.Collapsed;

        SearchBox.Tag = _mode == "collections" ? "buscar colecciones..." : "buscar en Civitai...";
        TypeCombo.IsEnabled = SortCombo.IsEnabled = NsfwCheck.IsEnabled = _mode == "search";
        MoreButton.Visibility = (_mode == "search" && _cursor != null) || (_mode == "collections" && _collectionsCursor != null)
            ? Visibility.Visible
            : Visibility.Collapsed;

        UpdateEmpty();
    }

    private void UpdateEmpty()
    {
        var empty = _mode == "search" && _results.Count == 0;
        EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = "> escribe algo y pulsa Buscar_";
    }

    /// <summary>Lo usa Referencias: abre la busqueda con un nombre y recuerda de donde viene la peticion.</summary>
    public async void SearchFor(string text, string sourceNote)
    {
        _sourceNote = sourceNote;
        SearchBox.Text = text;
        TypeCombo.SelectedIndex = 0;

        foreach (var chip in ModesPanel.Children.OfType<RadioButton>())
        {
            if ((string)chip.Tag == "search") chip.IsChecked = true;
        }

        _mode = "search";
        ApplyMode();
        await SearchAsync(reset: true);
    }

    // ===================== Busqueda =====================

    private void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;
        SearchButton.IsEnabled = !busy;
        ResolveButton.IsEnabled = !busy;
        MoreButton.IsEnabled = !busy;
        if (status != null) StatusText.Text = status;
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await RunSearchAsync();

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await RunSearchAsync();
    }

    private async Task RunSearchAsync()
    {
        if (_mode == "collections") await SearchCollectionsAsync(reset: true);
        else
        {
            _sourceNote = ""; // una busqueda manual ya no viene de una referencia
            await SearchAsync(reset: true);
        }
    }

    private async Task SearchAsync(bool reset)
    {
        if (_busy) return;
        SetBusy(true, "buscando en Civitai...");

        try
        {
            if (reset)
            {
                _results.Clear();
                _cursor = null;
                _preferredVersion.Clear();
                ShowDetail(null);
            }

            var type = TypeCombo.SelectedIndex switch { 1 => "LORA", 2 => "LoCon", 3 => "Checkpoint", _ => null };
            var sort = SortCombo.SelectedIndex switch { 1 => "Highest Rated", 2 => "Newest", 3 => "Most Liked", _ => "Most Downloaded" };

            var page = await _civitai.SearchModelsAsync(SearchBox.Text, type, sort, NsfwCheck.IsChecked == true, _cursor);

            foreach (var model in page.Items)
            {
                if (_results.All(r => r.Id != model.Id)) _results.Add(model);
            }

            _cursor = page.NextCursor;
            SetBusy(false, $"{_results.Count} resultados" + (_cursor != null ? " (hay mas)" : ""));
        }
        catch (CivitaiException ex)
        {
            SetBusy(false, ex.Message);
        }
        catch (OperationCanceledException)
        {
            SetBusy(false);
        }
        finally
        {
            _busy = false;
            SearchButton.IsEnabled = true;
            ResolveButton.IsEnabled = true;
            MoreButton.IsEnabled = true;
            ApplyMode();
        }
    }

    private async Task SearchCollectionsAsync(bool reset)
    {
        if (_busy) return;
        SetBusy(true, "buscando colecciones...");

        try
        {
            if (reset) _collectionsCursor = null;

            var page = await _civitai.SearchCollectionsAsync(SearchBox.Text, _collectionsCursor);

            var items = reset
                ? page.Items
                : ((CollectionsList.ItemsSource as IEnumerable<CivitaiCollection>) ?? Enumerable.Empty<CivitaiCollection>()).Concat(page.Items).ToList();

            CollectionsList.ItemsSource = items;
            _collectionsCursor = page.NextCursor;
            SetBusy(false, $"{items.Count()} colecciones" + (_collectionsCursor != null ? " (hay mas)" : ""));
        }
        catch (CivitaiException ex)
        {
            SetBusy(false, ex.Message);
        }
        finally
        {
            _busy = false;
            SearchButton.IsEnabled = true;
            ResolveButton.IsEnabled = true;
            MoreButton.IsEnabled = true;
            ApplyMode();
        }
    }

    private async void More_Click(object sender, RoutedEventArgs e)
    {
        if (_mode == "collections") await SearchCollectionsAsync(reset: false);
        else await SearchAsync(reset: false);
    }

    private void OpenCollection_Click(object sender, RoutedEventArgs e)
    {
        if (CollectionsList.SelectedItem is CivitaiCollection collection) OpenUrl(collection.Url);
    }

    // ===================== Pegar enlaces =====================

    private async void Resolve_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        var links = CivitaiService.ParseLinks(LinksBox.Text);
        if (links.Count == 0)
        {
            StatusText.Text = "no se ha reconocido ningun enlace o id de modelo";
            return;
        }

        SetBusy(true);
        _results.Clear();
        _cursor = null;
        _preferredVersion.Clear();
        ShowDetail(null);

        var failed = 0;

        try
        {
            for (var i = 0; i < links.Count; i++)
            {
                StatusText.Text = $"resolviendo {i + 1}/{links.Count}...";

                try
                {
                    var model = await _civitai.GetModelAsync(links[i].ModelId);
                    if (_results.All(r => r.Id != model.Id)) _results.Add(model);
                    if (links[i].VersionId is { } versionId) _preferredVersion[model.Id] = versionId;
                }
                catch (CivitaiException)
                {
                    failed++;
                }
            }
        }
        finally
        {
            _busy = false;
            SearchButton.IsEnabled = true;
            ResolveButton.IsEnabled = true;
            MoreButton.IsEnabled = true;
        }

        StatusText.Text = $"{_results.Count} modelos resueltos" + (failed > 0 ? $", {failed} no se pudieron obtener" : "");
        _sourceNote = "enlaces pegados";

        // Los resultados se ven en el modo Buscar.
        foreach (var chip in ModesPanel.Children.OfType<RadioButton>())
        {
            if ((string)chip.Tag == "search") chip.IsChecked = true;
        }
    }

    // ===================== Detalle =====================

    private void CardsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = CardsList.SelectedItems.Cast<CivitaiModel>().ToList();
        DownloadButton.Content = selected.Count > 1 ? $"Descargar ({selected.Count})..." : "Descargar...";

        var focus = e.AddedItems.OfType<CivitaiModel>().LastOrDefault() ?? selected.LastOrDefault();
        ShowDetail(focus);
    }

    private void ShowDetail(CivitaiModel? model)
    {
        _detail = model;

        DetailEmpty.Visibility = model == null ? Visibility.Visible : Visibility.Collapsed;
        DetailScroll.Visibility = model == null ? Visibility.Collapsed : Visibility.Visible;
        DetailScroll.DataContext = model;
        if (model == null) return;

        DetailScroll.ScrollToTop();
        TagsText.Text = model.Tags.Count == 0 ? "-" : string.Join(", ", model.Tags.Take(14));

        VersionCombo.ItemsSource = model.ModelVersions;
        var preferred = _preferredVersion.TryGetValue(model.Id, out var id)
            ? model.ModelVersions.FirstOrDefault(v => v.Id == id)
            : null;
        VersionCombo.SelectedItem = preferred ?? model.ModelVersions.FirstOrDefault(); // la mas reciente por defecto
    }

    private void VersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VersionCombo.SelectedItem is not CivitaiVersion version)
        {
            FilesText.Text = "";
            WordsText.Text = "";
            InstalledText.Text = "";
            return;
        }

        var sb = new StringBuilder();
        foreach (var file in version.Files.Take(6)) sb.AppendLine(file.Display + (file.Primary ? "   [principal]" : ""));
        FilesText.Text = sb.Length == 0 ? "esta version no tiene archivos descargables" : sb.ToString().TrimEnd();

        WordsText.Text = version.TrainedWords.Count == 0 ? "-" : string.Join(", ", version.TrainedWords);

        var file0 = version.PrimaryFile;
        var installed = file0 == null ? null : ModelCatalogService.Instance.Models.FirstOrDefault(m =>
            string.Equals(m.FileName, file0.Name, StringComparison.OrdinalIgnoreCase));

        if (installed == null)
        {
            InstalledText.Text = "No instalada en la biblioteca.";
            InstalledText.Foreground = (System.Windows.Media.Brush)FindResource("Brush.TextDim");
        }
        else if (file0 != null && Math.Abs(installed.SizeBytes - file0.SizeBytes) < 2048)
        {
            InstalledText.Text = $"INSTALADA (esta version) en {installed.DisplayFolder}.";
            InstalledText.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Accent");
        }
        else
        {
            InstalledText.Text = $"Hay un archivo con el mismo nombre en {installed.DisplayFolder}, pero con otro tamano (otra version?).";
            InstalledText.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Warning");
        }
    }

    private void OpenModel_Click(object sender, RoutedEventArgs e)
    {
        if (_detail != null) OpenUrl(_detail.Url);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            if (url.StartsWith("https://civitai.com/", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo abrir el enlace", ex);
        }
    }

    // ===================== Descargar (siempre con confirmacion) =====================

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        var models = CardsList.SelectedItems.Cast<CivitaiModel>().ToList();
        if (models.Count == 0 && _detail != null) models.Add(_detail);
        if (models.Count == 0) return;

        var request = models.Select(m =>
        {
            long? preferred = m == _detail && VersionCombo.SelectedItem is CivitaiVersion v
                ? v.Id
                : _preferredVersion.TryGetValue(m.Id, out var id) ? id : null;
            return (m, preferred);
        }).ToList();

        var dialog = new ConfirmDownloadWindow(request, _sourceNote) { Owner = Owner };
        if (dialog.ShowDialog() != true) return;

        var queue = DownloadQueueService.Instance;
        var added = 0;
        DownloadButton.IsEnabled = false;

        try
        {
            foreach (var candidate in dialog.Chosen)
            {
                var version = candidate.Version!;
                var file = candidate.File!;

                // Se pide la version completa: hashes, palabras de activacion y enlaces actualizados.
                try
                {
                    var detailed = await _civitai.GetVersionAsync(version.Id);
                    file = detailed.Files.FirstOrDefault(f => f.Id == file.Id) ?? detailed.PrimaryFile ?? file;
                    version = detailed;
                }
                catch (CivitaiException)
                {
                    // Se sigue con los datos de la busqueda.
                }

                queue.Enqueue(new DownloadRequest(candidate.Model, version, file, candidate.FinalPath, _sourceNote));
                added++;
            }
        }
        finally
        {
            DownloadButton.IsEnabled = true;
        }

        StatusText.Text = $"{added} descarga(s) anadida(s) a la cola";
        if (added > 0) DownloadsQueued?.Invoke();
    }
}
