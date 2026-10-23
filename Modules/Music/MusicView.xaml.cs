using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace Launcher.Modules.Music;

public partial class MusicView : UserControl
{
    private readonly MusicLibraryService _library = MusicLibraryService.Instance;
    private readonly ICollectionView _view;

    private string _search = "";
    private bool _loadedOnce;

    public MusicView()
    {
        InitializeComponent();

        _view = CollectionViewSource.GetDefaultView(_library.Tracks);
        _view.Filter = FilterPredicate;
        ApplySort();

        TracksItems.ItemsSource = _view;
        _library.Tracks.CollectionChanged += (_, _) => UpdateState();

        UpdateState();
        Loaded += MusicView_Loaded;
    }

    private async void MusicView_Loaded(object sender, RoutedEventArgs e)
    {
        // Loaded se dispara cada vez que la vista vuelve al arbol visual: solo
        // leemos la carpeta la primera vez.
        if (_loadedOnce) return;
        _loadedOnce = true;

        await _library.LoadAsync();
        UpdateState();
    }

    // ===================== Filtro / busqueda / orden =====================

    private bool FilterPredicate(object obj)
    {
        if (obj is not MusicTrack track) return false;
        if (string.IsNullOrWhiteSpace(_search)) return true;

        return track.Title.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
               track.Artist.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
               track.Album.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplySort()
    {
        _view.SortDescriptions.Clear();

        var property = SortCombo.SelectedIndex switch
        {
            1 => nameof(MusicTrack.Artist),
            2 => nameof(MusicTrack.Album),
            3 => nameof(MusicTrack.Year),
            _ => nameof(MusicTrack.Title)
        };

        _view.SortDescriptions.Add(new SortDescription(property, ListSortDirection.Ascending));

        // Desempate estable por titulo cuando se ordena por otro campo.
        if (property != nameof(MusicTrack.Title))
        {
            _view.SortDescriptions.Add(new SortDescription(nameof(MusicTrack.Title), ListSortDirection.Ascending));
        }
    }

    private void UpdateState()
    {
        var visible = _view.Cast<object>().Count();
        EmptyState.Visibility = visible == 0 ? Visibility.Visible : Visibility.Collapsed;
        InfoText.Text = $"{_library.Folder}  \u00b7  {_library.Tracks.Count} cancion(es)";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text;
        _view.Refresh();
        UpdateState();
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_view == null) return;
        ApplySort();
        _view.Refresh();
    }

    // ===================== Importar =====================

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona las canciones",
            Multiselect = true,
            Filter = "Audio|" + string.Join(";", MusicLibraryService.Extensions.Select(x => "*" + x)) +
                     "|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        await ImportFilesAsync(dialog.FileNames);
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DownloadWindow { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();

        if (dialog.Downloaded.Count == 0) return;

        // Los archivos ya estan en la carpeta de la biblioteca: ImportAsync solo los lee y los anade a la lista.
        await _library.ImportAsync(dialog.Downloaded);
        _view.Refresh();
        UpdateState();
    }

    private async Task ImportFilesAsync(IEnumerable<string> files)
    {
        var list = files.ToList();
        var imported = await _library.ImportAsync(list);

        _view.Refresh();
        UpdateState();

        if (imported == 0)
        {
            MessageBox.Show(Window.GetWindow(this),
                "No se ha importado ninguna cancion (formato no compatible o ya estaba en la biblioteca).",
                "Importar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void MusicView_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void MusicView_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        await ImportFilesAsync(files);
    }

    // ===================== Carpeta de la biblioteca =====================

    private async void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Selecciona la carpeta de tu biblioteca de musica",
            SelectedPath = _library.Folder
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;

        var settings = AppSettingsService.Instance;
        settings.MusicFolder = dialog.SelectedPath;
        settings.Save();

        await _library.LoadAsync();
        _view.Refresh();
        UpdateState();
    }

    // ===================== Acciones sobre una cancion =====================

    private MusicTrack GetTrackFromMenuItem(object sender)
    {
        var menuItem = (MenuItem)sender;
        var contextMenu = (ContextMenu)menuItem.Parent;
        var owner = (FrameworkElement)contextMenu.PlacementTarget;
        return (MusicTrack)owner.DataContext;
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (((FrameworkElement)sender).DataContext is MusicTrack track) _library.Play(track);
    }

    private void PlayMenuItem_Click(object sender, RoutedEventArgs e) =>
        _library.Play(GetTrackFromMenuItem(sender));

    private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e) =>
        _library.ShowInExplorer(GetTrackFromMenuItem(sender));

    /// <summary>Busca en MusicBrainz los datos correctos de una cancion ya importada y los escribe
    /// en el archivo. Si no hay una coincidencia fiable no toca nada.</summary>
    private async void AutoFillMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var track = GetTrackFromMenuItem(sender);

        MusicMetadata? meta = null;
        try
        {
            meta = await MusicAutoService.ResolveForTrackAsync(track, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogService.Error($"Fallo el autocompletado de '{track.Title}'", ex);
        }

        if (meta == null)
        {
            MessageBox.Show(Window.GetWindow(this),
                "No he encontrado una coincidencia fiable (o no hay conexion). Puedes corregirlo a mano con Editar.",
                "Autocompletar datos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string? coverPath = null;
        try
        {
            if (meta.CoverBytes != null)
            {
                coverPath = Path.Combine(Path.GetTempPath(), $"launcher_cover_{Guid.NewGuid():N}.jpg");
                await File.WriteAllBytesAsync(coverPath, meta.CoverBytes);
            }

            var ok = _library.ApplyEdits(track, meta.Title, meta.Artist, meta.Album, meta.Year, coverPath, false);
            if (!ok)
            {
                MessageBox.Show(Window.GetWindow(this),
                    "No se han podido guardar los datos. Comprueba que el archivo no esta en uso.",
                    "Autocompletar datos", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            if (coverPath != null)
            {
                try { File.Delete(coverPath); } catch { /* limpieza best-effort */ }
            }
        }

        _view.Refresh();
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var track = GetTrackFromMenuItem(sender);
        var dialog = new EditTrackWindow(track) { Owner = Window.GetWindow(this) };

        if (dialog.ShowDialog() != true) return;

        var ok = _library.ApplyEdits(track, dialog.TrackTitle, dialog.Artist, dialog.Album, dialog.Year,
                                     dialog.NewCoverPath, dialog.RemoveCover);

        if (!ok)
        {
            MessageBox.Show(Window.GetWindow(this),
                "No se han podido guardar los cambios. Comprueba que el archivo no esta en uso (por ejemplo, sonando en otro reproductor) ni es de solo lectura.",
                "Editar cancion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _view.Refresh();
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var track = GetTrackFromMenuItem(sender);

        var result = MessageBox.Show(
            Window.GetWindow(this),
            $"Se enviara \"{track.Title}\" a la papelera de Windows.",
            "Eliminar cancion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (!_library.Delete(track))
        {
            MessageBox.Show(Window.GetWindow(this), "No se ha podido eliminar el archivo.",
                "Eliminar cancion", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _view.Refresh();
        UpdateState();
    }
}
