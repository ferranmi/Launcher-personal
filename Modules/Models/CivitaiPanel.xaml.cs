using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

/// <summary>
/// Explorador de Civitai: busqueda con filtros, versiones, archivos y descarga. Nada se descarga sin pasar por la
/// ventana de confirmacion. Las colecciones publicas solo exponen metadatos en la API de Civitai, asi que su
/// contenido se descarga pegando los enlaces de sus modelos.
/// </summary>
public partial class CivitaiPanel : UserControl
{
    // Modelos base conocidos (ejemplos de la documentacion de Civitai). Solo se usan hasta que se carga la lista
    // real desde la API, o si no se puede cargar.
    private static readonly string[] FallbackBaseModels =
    {
        "SD 1.5", "SD 2.1", "SD 3.5", "SDXL 1.0", "Flux.1 D", "Illustrious", "Pony", "Hunyuan Video"
    };

    private readonly CivitaiService _civitai = CivitaiService.Instance;
    private readonly ObservableCollection<CivitaiModel> _results = new();
    private readonly Dictionary<long, long> _preferredVersion = new();

    private string? _cursor;
    private string? _collectionsCursor;
    private bool _busy;
    private bool _ready;
    private bool _filtersOpen;
    private bool _hasSearched;
    private string _mode = "search";
    private string _sourceNote = "";
    private CivitaiModel? _detail;

    // Busqueda de modelos en curso (null si no hay ninguna). Una busqueda nueva cancela la anterior.
    private CancellationTokenSource? _searchCts;

    // Filtros con los que se hizo la ultima busqueda: "Cargar mas" debe usar los mismos (el cursor
    // solo vale para la consulta que lo genero), aunque el usuario haya tocado los filtros despues.
    private CivitaiSearchOptions _lastOptions = new();

    /// <summary>Se han anadido descargas a la cola: la ventana principal puede mostrar la pestana Descargas.</summary>
    public event Action? DownloadsQueued;

    public CivitaiPanel()
    {
        InitializeComponent();

        CardsList.ItemsSource = _results;
        BaseModelList.ItemsSource = FallbackBaseModels;
        BaseModelHint.Text = "lista local (cargando la de Civitai...)";
        _ready = true;

        ApplySortState();
        ApplyTypeState();
        UpdateFiltersButton();
        UpdateEmpty();

        _ = LoadBaseModelsAsync();
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
        var searchMode = _mode == "search";

        CardsList.Visibility = searchMode ? Visibility.Visible : Visibility.Collapsed;
        CollectionsGrid.Visibility = _mode == "collections" ? Visibility.Visible : Visibility.Collapsed;
        LinksGrid.Visibility = _mode == "links" ? Visibility.Visible : Visibility.Collapsed;

        SearchBox.Tag = _mode == "collections" ? "buscar colecciones..." : "buscar en Civitai (o pegar un enlace)...";
        FiltersButton.IsEnabled = NsfwCheck.IsEnabled = searchMode;
        FiltersPanel.Visibility = searchMode && _filtersOpen ? Visibility.Visible : Visibility.Collapsed;
        MoreButton.Visibility = (searchMode && _cursor != null) || (_mode == "collections" && _collectionsCursor != null)
            ? Visibility.Visible
            : Visibility.Collapsed;

        UpdateEmpty();
    }

    private void UpdateEmpty()
    {
        var empty = _mode == "search" && _results.Count == 0;
        EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = _hasSearched ? "> sin resultados con estos filtros_" : "> escribe algo y pulsa Buscar_";
    }

    /// <summary>Lo usa Referencias: abre la busqueda con un nombre y recuerda de donde viene la peticion.</summary>
    public async void SearchFor(string text, string sourceNote)
    {
        _sourceNote = sourceNote;
        SearchBox.Text = text;
        ClearFilters(); // una busqueda desde fuera parte siempre de filtros limpios

        foreach (var chip in ModesPanel.Children.OfType<RadioButton>())
        {
            if ((string)chip.Tag == "search") chip.IsChecked = true;
        }

        _mode = "search";
        ApplyMode();
        await SearchAsync(reset: true);
    }

    // ===================== Filtros =====================

    private void FiltersButton_Click(object sender, RoutedEventArgs e)
    {
        _filtersOpen = !_filtersOpen;
        ApplyMode();
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e) => ClearFilters();

    private void ClearFilters()
    {
        SortCombo.SelectedIndex = 0;
        PeriodCombo.SelectedIndex = 0;

        foreach (var box in TypesPanel.Children.OfType<CheckBox>()) box.IsChecked = false;

        BaseModelList.SelectedItems.Clear();
        TagBox.Text = "";
        UserBox.Text = "";
        GenCheck.IsChecked = false;
        PlatformCheck.IsChecked = false;
        EarlyCheck.IsChecked = false;
        CheckpointTypeCombo.SelectedIndex = 0;

        ApplySortState();
        ApplyTypeState();
        UpdateFiltersButton();
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return; // el evento salta al construir la ventana, antes de que exista PeriodCombo
        ApplySortState();
    }

    private void TypeCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        ApplyTypeState();
    }

    /// <summary>Enter dentro de los cuadros de etiqueta o creador lanza la busqueda.</summary>
    private async void FilterBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await RunSearchAsync();
    }

    /// <summary>El periodo solo tiene sentido para ordenes basados en una metrica; en Newest/Oldest no se usa.</summary>
    private void ApplySortState()
    {
        PeriodCombo.IsEnabled = SortValue() is not ("Newest" or "Oldest");
    }

    /// <summary>El tipo de checkpoint solo se aplica a checkpoints.</summary>
    private void ApplyTypeState()
    {
        var checkpoint = CheckpointCheck.IsChecked == true;
        CheckpointTypeCombo.IsEnabled = checkpoint;
        if (!checkpoint) CheckpointTypeCombo.SelectedIndex = 0;
    }

    private static string TagOf(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

    private string SortValue()
    {
        var value = TagOf(SortCombo);
        return value.Length == 0 ? "Most Downloaded" : value;
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private CivitaiSearchOptions BuildOptions()
    {
        // Periodo y tipo de checkpoint solo se envian si su control esta activo y tiene un valor elegido.
        var period = PeriodCombo.IsEnabled ? TagOf(PeriodCombo) : "";
        var checkpointType = CheckpointTypeCombo.IsEnabled ? TagOf(CheckpointTypeCombo) : "";

        var options = new CivitaiSearchOptions
        {
            Query = NullIfBlank(SearchBox.Text),
            Nsfw = NsfwCheck.IsChecked == true,
            Sort = SortValue(),
            Period = NullIfBlank(period),
            CheckpointType = NullIfBlank(checkpointType),
            Tag = NullIfBlank(TagBox.Text),
            Username = NullIfBlank(UserBox.Text),
            SupportsGeneration = GenCheck.IsChecked == true,
            FromPlatform = PlatformCheck.IsChecked == true,
            EarlyAccess = EarlyCheck.IsChecked == true
        };

        foreach (var box in TypesPanel.Children.OfType<CheckBox>())
        {
            if (box.IsChecked == true && box.Tag is string type) options.Types.Add(type);
        }

        foreach (var baseModel in BaseModelList.SelectedItems.OfType<string>()) options.BaseModels.Add(baseModel);

        return options;
    }

    /// <summary>Cuantos filtros se apartan de los valores por defecto (se muestra en el boton Filtros).</summary>
    private int ActiveFilterCount()
    {
        var count = 0;
        if (SortCombo.SelectedIndex > 0) count++;
        if (PeriodCombo.IsEnabled && PeriodCombo.SelectedIndex > 0) count++;
        if (TypesPanel.Children.OfType<CheckBox>().Any(b => b.IsChecked == true)) count++;
        if (BaseModelList.SelectedItems.Count > 0) count++;
        if (CheckpointTypeCombo.IsEnabled && CheckpointTypeCombo.SelectedIndex > 0) count++;
        if (!string.IsNullOrWhiteSpace(TagBox.Text)) count++;
        if (!string.IsNullOrWhiteSpace(UserBox.Text)) count++;
        if (GenCheck.IsChecked == true) count++;
        if (PlatformCheck.IsChecked == true) count++;
        if (EarlyCheck.IsChecked == true) count++;
        return count;
    }

    private void UpdateFiltersButton()
    {
        var count = ActiveFilterCount();
        FiltersButton.Content = count == 0 ? "Filtros" : $"Filtros ({count})";
    }

    /// <summary>Sustituye la lista local de modelos base por la que publica Civitai, conservando lo ya marcado.</summary>
    private async Task LoadBaseModelsAsync()
    {
        try
        {
            var list = await _civitai.GetBaseModelsAsync();
            if (list.Count == 0)
            {
                BaseModelHint.Text = "lista local (Civitai no devolvio ninguna)";
                return;
            }

            var selected = BaseModelList.SelectedItems.OfType<string>().ToList();
            BaseModelList.ItemsSource = list;
            foreach (var item in selected.Where(list.Contains)) BaseModelList.SelectedItems.Add(item);

            BaseModelHint.Text = $"{list.Count} modelos base (lista de Civitai)";
        }
        catch (CivitaiException)
        {
            BaseModelHint.Text = "lista local (no se pudo leer la de Civitai)";
        }
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
        if (_mode == "collections")
        {
            await SearchCollectionsAsync(reset: true);
            return;
        }

        // Un enlace de Civitai pegado en la barra se resuelve directamente (un numero suelto NO: se busca como texto).
        if (LooksLikeLink(SearchBox.Text))
        {
            if (_busy && _searchCts == null) return; // otra operacion en curso
            CancelSearch();
            await ResolveAsync(SearchBox.Text);
            return;
        }

        _sourceNote = ""; // una busqueda manual ya no viene de una referencia
        await SearchAsync(reset: true);
    }

    private static bool LooksLikeLink(string? text) =>
        !string.IsNullOrWhiteSpace(text) &&
        Regex.IsMatch(text, @"civitai\.com/models/\d+|modelVersionId=\d+", RegexOptions.IgnoreCase) &&
        CivitaiService.ParseLinks(text).Count > 0;

    /// <summary>Cancela la busqueda de modelos en curso. Su "finally" ya no tocara el estado de la ventana.</summary>
    private void CancelSearch()
    {
        var cts = _searchCts;
        if (cts == null) return;

        _searchCts = null;
        _busy = false;
        cts.Cancel();
    }

    private async Task SearchAsync(bool reset)
    {
        if (!reset && _busy) return;                      // "Cargar mas" no se solapa con otra operacion
        if (reset && _busy && _searchCts == null) return; // hay otra operacion (resolver / colecciones) en curso

        if (reset) CancelSearch(); // una busqueda nueva sustituye a la que estuviera en curso

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        var ct = cts.Token;

        SetBusy(true, "buscando en Civitai...");
        SearchButton.IsEnabled = true; // se puede lanzar otra busqueda y sustituir esta
        UpdateFiltersButton();

        try
        {
            if (reset)
            {
                _results.Clear();
                _cursor = null;
                _preferredVersion.Clear();
                ShowDetail(null);
                _lastOptions = BuildOptions();
            }

            var page = await _civitai.SearchModelsAsync(_lastOptions, _cursor, ct);
            ct.ThrowIfCancellationRequested(); // si la han sustituido mientras llegaba, no se anade nada

            foreach (var model in page.Items)
            {
                if (_results.All(r => r.Id != model.Id)) _results.Add(model);
            }

            _cursor = page.NextCursor;
            _hasSearched = true;
            SetBusy(false, $"{_results.Count} resultados" + (_cursor != null ? " (hay mas)" : ""));
        }
        catch (CivitaiException ex)
        {
            if (ReferenceEquals(_searchCts, cts)) SetBusy(false, ex.Message);
        }
        catch (OperationCanceledException)
        {
            // Sustituida por otra busqueda: la nueva se encarga del estado.
        }
        finally
        {
            if (ReferenceEquals(_searchCts, cts))
            {
                _searchCts = null;
                _busy = false;
                SearchButton.IsEnabled = true;
                ResolveButton.IsEnabled = true;
                MoreButton.IsEnabled = true;
                ApplyMode();
            }
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

    private async void Resolve_Click(object sender, RoutedEventArgs e) => await ResolveAsync(LinksBox.Text);

    private async Task ResolveAsync(string text)
    {
        if (_busy) return;

        var links = CivitaiService.ParseLinks(text);
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
