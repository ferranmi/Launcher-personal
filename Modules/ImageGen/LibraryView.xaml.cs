using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Launcher.Core.Converters;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.ImageGen;

/// <summary>
/// Biblioteca de generaciones: rejilla con busqueda por texto (prompt, negativo, modelo,
/// extensiones), filtros por modelo/sampler/favoritas, orden por fecha y panel de detalle
/// con el prompt completo y los parametros. Usa su PROPIA vista sobre la coleccion para
/// que sus filtros no afecten a la tira de miniaturas de la pantalla de generacion.
/// </summary>
public partial class LibraryView : UserControl
{
    private const string AllLabel = "Todos";

    private readonly GalleryService _gallery = GalleryService.Instance;
    private readonly ICollectionView _view;
    private GeneratedImage? _selected;
    private bool _rebuilding;

    /// <summary>El usuario quiere cargar los parametros de una imagen en el panel de generacion.</summary>
    public event Action<ImageGenParams>? ReuseRequested;

    public event Action? CloseRequested;

    public LibraryView()
    {
        InitializeComponent();

        _view = new CollectionViewSource { Source = _gallery.Images }.View;
        _view.Filter = Matches;
        ApplySort();

        ImagesList.ItemsSource = _view;
        RebuildFilterLists();
        UpdateCount();

        // Al volver a mostrarse, las listas de filtros se reconstruyen con lo generado entretanto.
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is not true) return;
            RebuildFilterLists();
            _view.Refresh();
            UpdateCount();
        };
    }

    // ===================== Filtros =====================

    private bool Matches(object item)
    {
        if (item is not GeneratedImage image) return false;
        var p = image.Params;

        if (FavOnly.IsChecked == true && !image.Favorite) return false;
        if (ModelFilter.SelectedItem is string model && model != AllLabel && p.ModelName != model) return false;
        if (SamplerFilter.SelectedItem is string sampler && sampler != AllLabel && p.Sampler != sampler) return false;

        var query = SearchBox.Text.Trim();
        if (query.Length == 0) return true;

        var haystack = $"{p.Prompt}\n{p.NegativePrompt}\n{p.ModelName}\n{string.Join(' ', p.ExtensionStates.Keys)}";
        foreach (var token in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!haystack.Contains(token, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    private void RebuildFilterLists()
    {
        _rebuilding = true;
        try
        {
            Fill(ModelFilter, _gallery.Images.Select(i => i.Params.ModelName));
            Fill(SamplerFilter, _gallery.Images.Select(i => i.Params.Sampler));
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private static void Fill(ComboBox combo, IEnumerable<string?> values)
    {
        var keep = combo.SelectedItem as string;

        var items = new[] { AllLabel }
            .Concat(values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .Distinct()
                .OrderBy(v => v, StringComparer.CurrentCultureIgnoreCase))
            .ToList();

        combo.ItemsSource = items;
        combo.SelectedItem = keep != null && items.Contains(keep) ? keep : AllLabel;
    }

    private void ApplySort()
    {
        _view.SortDescriptions.Clear();
        var direction = SortCombo.SelectedIndex == 1 ? ListSortDirection.Ascending : ListSortDirection.Descending;
        _view.SortDescriptions.Add(new SortDescription(nameof(GeneratedImage.CreatedAt), direction));
    }

    private void UpdateCount()
    {
        var shown = _view.Cast<object>().Count();
        CountText.Text = $"{shown} de {_gallery.Images.Count}";
        EmptyText.Visibility = shown == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Refilter();
    private void Filter_Changed(object sender, RoutedEventArgs e) => Refilter();

    private void Refilter()
    {
        if (_view == null || _rebuilding) return;
        _view.Refresh();
        UpdateCount();
    }

    private void Sort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_view == null) return;
        ApplySort();
    }

    // ===================== Detalle =====================

    private void ImagesList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ShowDetail(ImagesList.SelectedItem as GeneratedImage);

    private void ShowDetail(GeneratedImage? image)
    {
        _selected = image;
        DetailEmpty.Visibility = image == null ? Visibility.Visible : Visibility.Collapsed;
        DetailScroll.Visibility = image == null ? Visibility.Collapsed : Visibility.Visible;

        if (image == null)
        {
            DetailImage.Source = null;
            return;
        }

        var p = image.Params;
        var vae = p.Vae ?? "Automatic";
        var scheduler = string.IsNullOrWhiteSpace(p.Scheduler) ? "" : $" / {p.Scheduler}";
        var extensions = p.ExtensionStates.Count > 0 ? string.Join(", ", p.ExtensionStates.Keys) : "ninguna";

        DetailImage.Source = new PathToBitmapConverter()
            .Convert(image.FilePath, typeof(BitmapSource), null, CultureInfo.InvariantCulture) as BitmapSource;

        DetailPrompt.Text = p.Prompt;
        DetailNegative.Text = p.NegativePrompt;
        DetailMeta.Text =
            $"{p.ModelName}\n" +
            $"{p.Sampler}{scheduler} · {p.Steps} steps · CFG {p.CfgScale:0.#}\n" +
            $"{p.Width}x{p.Height} · seed {image.Seed}\n" +
            $"Clip skip {p.ClipSkip} · VAE {vae}\n" +
            $"Extensiones: {extensions}\n" +
            $"{image.CreatedAt:dd/MM/yyyy HH:mm}";

        FavButton.Content = image.Favorite ? "Quitar favorita" : "Favorita";
    }

    // ===================== Acciones =====================

    private void Back_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void Reuse_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;

        // La seed real usada, no el -1 que se pidio, para reproducir la misma imagen.
        var p = _selected.Params.Clone();
        p.Seed = _selected.Seed;
        ReuseRequested?.Invoke(p);
    }

    private void CopyPrompt_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null || string.IsNullOrEmpty(_selected.Params.Prompt)) return;

        try
        {
            Clipboard.SetText(_selected.Params.Prompt);
        }
        catch (Exception ex)
        {
            // El portapapeles puede estar bloqueado un instante por otra aplicacion.
            LogService.Error("No se pudo copiar el prompt", ex);
        }
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;

        _selected.Favorite = !_selected.Favorite;
        _gallery.Save();
        FavButton.Content = _selected.Favorite ? "Quitar favorita" : "Favorita";

        _view.Refresh();
        UpdateCount();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null || !_selected.Exists) return;

        try
        {
            Process.Start("explorer.exe", $"/select,\"{_selected.FilePath}\"");
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo abrir la carpeta de la imagen", ex);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;

        var result = MessageBox.Show(
            Window.GetWindow(this),
            "Se borrara esta imagen generada del disco. Continuar?",
            "Eliminar imagen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        _gallery.Remove(_selected);
        ShowDetail(null);
        UpdateCount();
    }
}
