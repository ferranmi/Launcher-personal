using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

/// <summary>
/// Biblioteca local: filtros, orden, vista de tarjetas o lista y panel de detalles. Solo lectura:
/// no mueve, renombra ni borra nada (renombrar con simulacion llega en el paso 4).
/// </summary>
public partial class LibraryPanel : UserControl
{
    private const int PageSize = 80;
    private const string AllCategories = "Todas las carpetas";
    private const string DefaultWeight = "0.8";

    private readonly ModelCatalogService _catalog = ModelCatalogService.Instance;
    private readonly ModelsSettings _settings = ModelsSettings.Instance;

    // Las tarjetas se cargan por paginas (un WrapPanel no virtualiza y cada una decodifica una imagen).
    // La lista usa todo el resultado: ahi si hay virtualizacion.
    private readonly ObservableCollection<ModelEntry> _cardItems = new();
    private List<ModelEntry> _filtered = new();

    private ModelEntry? _selected;
    private ModelType? _typeFilter;
    private string _search = "";

    private bool _ready;
    private bool _syncingSelection;
    private bool _updatingCategories;

    public LibraryPanel()
    {
        InitializeComponent();

        CardsList.ItemsSource = _cardItems;

        SortCombo.SelectedIndex = Math.Clamp(_settings.LibrarySort, 0, SortCombo.Items.Count - 1);
        if (_settings.LibraryView == "list") ViewList.IsChecked = true;
        else ViewCards.IsChecked = true;

        _catalog.Changed += (_, _) => Reload();

        _ready = true;
        ApplyView();
        Reload();
    }

    // ===================== Carga y filtrado =====================

    private void Reload()
    {
        RebuildCategories();
        ApplyFilter();
    }

    private void RebuildCategories()
    {
        var current = CategoryCombo.SelectedItem as string ?? AllCategories;

        var categories = _catalog.Models
            .Select(m => m.Category)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _updatingCategories = true;
        CategoryCombo.Items.Clear();
        CategoryCombo.Items.Add(AllCategories);
        foreach (var category in categories) CategoryCombo.Items.Add(category);
        CategoryCombo.SelectedItem = categories.Contains(current) ? current : AllCategories;
        _updatingCategories = false;
    }

    private void ApplyFilter()
    {
        IEnumerable<ModelEntry> query = _catalog.Models;

        if (_typeFilter is { } type) query = query.Where(m => m.Type == type);

        if (CategoryCombo.SelectedItem is string category && category != AllCategories)
        {
            query = query.Where(m => string.Equals(m.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (_search.Length > 0)
        {
            query = query.Where(m =>
                m.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                m.RelativeFolder.Contains(_search, StringComparison.OrdinalIgnoreCase));
        }

        query = SortCombo.SelectedIndex switch
        {
            1 => query.OrderByDescending(m => m.SizeBytes),
            2 => query.OrderByDescending(m => m.Modified),
            3 => query.OrderBy(m => m.Type).ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => query.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
        };

        _filtered = query.ToList();

        _syncingSelection = true;
        _cardItems.Clear();
        foreach (var model in _filtered.Take(PageSize)) _cardItems.Add(model);
        ListList.ItemsSource = _filtered;
        _syncingSelection = false;

        // Tras un reescaneo las instancias son nuevas: se vuelve a localizar el modelo por su ruta.
        var again = _selected == null
            ? null
            : _filtered.FirstOrDefault(m => string.Equals(m.FullPath, _selected.FullPath, StringComparison.OrdinalIgnoreCase));
        SetSelected(again);

        UpdateFooter();
    }

    private void UpdateFooter()
    {
        var total = _catalog.Models.Count;
        var remaining = _filtered.Count - _cardItems.Count;
        var cards = ViewCards.IsChecked == true;

        ShowMoreButton.Visibility = cards && remaining > 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowMoreButton.Content = $"Mostrar mas ({remaining} restantes)";

        CountText.Text = $"{_filtered.Count} de {total} modelos";

        EmptyText.Visibility = _filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = total == 0
            ? "> sin modelos: configura la carpeta de A1111 en Configuracion_"
            : "> sin resultados_";
    }

    // ===================== Controles =====================

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text.Trim();
        if (_ready) ApplyFilter();
    }

    private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && !_updatingCategories) ApplyFilter();
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;

        _settings.LibrarySort = SortCombo.SelectedIndex;
        _settings.Save();
        ApplyFilter();
    }

    private void Type_Checked(object sender, RoutedEventArgs e)
    {
        _typeFilter = (string)((RadioButton)sender).Tag switch
        {
            "lora" => ModelType.Lora,
            "checkpoint" => ModelType.Checkpoint,
            "lycoris" => ModelType.LyCoris,
            _ => null
        };

        if (_ready) ApplyFilter();
    }

    private void View_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        _settings.LibraryView = (string)((RadioButton)sender).Tag;
        _settings.Save();
        ApplyView();
    }

    private void ApplyView()
    {
        var cards = ViewCards.IsChecked == true;
        CardsList.Visibility = cards ? Visibility.Visible : Visibility.Collapsed;
        ListList.Visibility = cards ? Visibility.Collapsed : Visibility.Visible;
        UpdateFooter();

        // La seleccion se conserva al cambiar de vista.
        if (_selected != null) SetSelected(_selected);
    }

    private void ShowMore_Click(object sender, RoutedEventArgs e)
    {
        foreach (var model in _filtered.Skip(_cardItems.Count).Take(PageSize)) _cardItems.Add(model);
        UpdateFooter();
    }

    public async Task RescanAsync()
    {
        if (_catalog.IsScanning) return;

        RescanButton.IsEnabled = false;
        RescanButton.Content = "Escaneando...";

        try
        {
            await _catalog.ScanAsync(); // dispara Changed -> Reload
        }
        finally
        {
            RescanButton.IsEnabled = true;
            RescanButton.Content = "Reescanear";
        }
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e) => await RescanAsync();

    // ===================== Seleccion y detalles =====================

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection) return;

        if (((ListBox)sender).SelectedItem is ModelEntry entry) SetSelected(entry);
    }

    private void SetSelected(ModelEntry? entry)
    {
        _selected = entry;

        _syncingSelection = true;
        if (!ReferenceEquals(CardsList.SelectedItem, entry)) CardsList.SelectedItem = entry;
        if (!ReferenceEquals(ListList.SelectedItem, entry)) ListList.SelectedItem = entry;
        _syncingSelection = false;

        ShowDetail(entry);
    }

    private async void ShowDetail(ModelEntry? entry)
    {
        DetailEmpty.Visibility = entry == null ? Visibility.Visible : Visibility.Collapsed;
        DetailScroll.Visibility = entry == null ? Visibility.Collapsed : Visibility.Visible;
        DetailScroll.DataContext = entry;

        if (entry == null) return;

        DetailScroll.ScrollToTop();

        var sameKind = _catalog.FindAllByName(entry.Name)
            .Count(m => (m.Type == ModelType.Checkpoint) == (entry.Type == ModelType.Checkpoint));

        if (sameKind > 1)
        {
            DuplicateText.Text = entry.Type == ModelType.Checkpoint
                ? $"Hay {sameKind} archivos con este nombre."
                : $"Hay {sameKind} archivos con este nombre: una etiqueta <lora:{entry.Name}> seria ambigua.";
            DuplicateText.Visibility = Visibility.Visible;
        }
        else
        {
            DuplicateText.Visibility = Visibility.Collapsed;
        }

        var isCheckpoint = entry.Type == ModelType.Checkpoint;
        CopyTagButton.IsEnabled = !isCheckpoint;

        if (isCheckpoint)
        {
            RefsText.Text = "Los checkpoints no se usan con etiquetas en los wildcards.";
            return;
        }

        RefsText.Text = "buscando...";

        Dictionary<string, List<WildcardRefLocation>> index;
        try
        {
            index = await WildcardService.Instance.GetReferenceIndexAsync();
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo construir el indice de referencias", ex);
            if (ReferenceEquals(_selected, entry)) RefsText.Text = "no se pudieron leer los wildcards";
            return;
        }

        if (!ReferenceEquals(_selected, entry)) return; // el usuario ya eligio otro mientras se leia

        if (!ModelsSettings.Instance.WildcardFoldersEffective().Any(Directory.Exists))
        {
            RefsText.Text = "sin carpeta de wildcards configurada";
            return;
        }

        if (!index.TryGetValue(entry.Name, out var locations) || locations.Count == 0)
        {
            RefsText.Text = "No aparece en ningun wildcard.";
            return;
        }

        var sb = new StringBuilder();
        var files = locations.Select(l => l.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        sb.AppendLine($"{locations.Count} referencia(s) en {files} wildcard(s):");

        foreach (var location in locations.Take(8))
        {
            sb.AppendLine($"  {location.FileName}  (linea {location.LineNumber})");
        }

        if (locations.Count > 8) sb.AppendLine($"  ... y {locations.Count - 8} mas");

        RefsText.Text = sb.ToString().TrimEnd();
    }

    // ===================== Acciones sobre el modelo =====================

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;

        if (!File.Exists(_selected.FullPath))
        {
            MessageBox.Show(Window.GetWindow(this),
                "El archivo ya no existe. Pulsa Reescanear para actualizar la biblioteca.",
                "Abrir ubicacion", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start("explorer.exe", $"/select,\"{_selected.FullPath}\"");
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo abrir la ubicacion del modelo", ex);
        }
    }

    private void CopyName_Click(object sender, RoutedEventArgs e)
    {
        if (_selected != null) CopyToClipboard(_selected.Name);
    }

    private void CopyTag_Click(object sender, RoutedEventArgs e)
    {
        if (_selected != null && _selected.Type != ModelType.Checkpoint)
        {
            CopyToClipboard($"<lora:{_selected.Name}:{DefaultWeight}>");
        }
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            // El portapapeles puede estar bloqueado un instante por otra aplicacion.
            LogService.Error("No se pudo copiar al portapapeles", ex);
        }
    }
}
