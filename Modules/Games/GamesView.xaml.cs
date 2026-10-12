using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Launcher.Core.Interfaces;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Modules.Games.Providers;

namespace Launcher.Modules.Games;

public partial class GamesView : UserControl
{
    private enum FilterMode { All, Favorites, Steam, Epic, Manual, Hidden }

    private readonly GameLibraryService _library = GameLibraryService.Instance;
    private readonly ICollectionView _view;

    private FilterMode _filter = FilterMode.All;
    private string _search = "";
    private bool _syncing;
    private bool _startupSyncDone;
    private bool _windowKeysHooked;

    public GamesView()
    {
        InitializeComponent();

        ApplyCardSize();
        AppSettingsService.Instance.Changed += (_, _) => ApplyCardSize();

        _view = CollectionViewSource.GetDefaultView(_library.Games);
        _view.Filter = FilterPredicate;
        ApplySort();

        GamesItems.ItemsSource = _view;
        UpdateEmptyState();

        SearchBox.PreviewKeyDown += SearchBox_PreviewKeyDown;
        Loaded += GamesView_Loaded;
    }

    // ===================== Ajustes =====================

    /// <summary>Ancho/alto de tarjeta segun el ajuste. La plantilla los lee con DynamicResource,
    /// asi que el cambio se ve al instante sin recrear la vista.</summary>
    private void ApplyCardSize()
    {
        var (width, height) = AppSettingsService.Instance.CardSize switch
        {
            "small" => (130.0, 162.0),
            "large" => (200.0, 250.0),
            _ => (160.0, 200.0)
        };

        Resources["Games.CardWidth"] = width;
        Resources["Games.CardHeight"] = height;
    }

    private async void GamesView_Loaded(object sender, RoutedEventArgs e)
    {
        // Atajo Ctrl+F a nivel de ventana (para que funcione aunque el foco este en el sidebar).
        if (!_windowKeysHooked && Window.GetWindow(this) is { } window)
        {
            _windowKeysHooked = true;
            window.PreviewKeyDown += Window_PreviewKeyDown;
        }

        // Loaded se dispara cada vez que la vista vuelve al arbol visual (al cambiar de
        // modulo): solo queremos sincronizar la primera vez.
        if (_startupSyncDone) return;
        _startupSyncDone = true;

        if (!AppSettingsService.Instance.SyncOnStartup) return;

        var paths = LibraryPathsService.Instance;
        if (!paths.HasSteamPath && !paths.HasEpicPath) return;

        try
        {
            await RunSyncAsync();
        }
        catch (Exception ex)
        {
            // La sincronizacion automatica es silenciosa: si falla, solo queda en el registro.
            LogService.Error("Fallo la sincronizacion al abrir", ex);
        }
    }

    // ===================== Atajos de teclado =====================

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsVisible) return; // la vista esta en cache pero no se esta mostrando

        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SearchBox.Clear();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(_search))
        {
            // Enter lanza el primer resultado: buscar y jugar sin tocar el raton.
            var first = _view.Cast<Game>().FirstOrDefault();
            if (first != null) LaunchGame(first);
            e.Handled = true;
        }
    }

    // ===================== Arrastrar y soltar =====================

    private void GamesView_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void GamesView_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;

        var exes = files
            .Where(f => string.Equals(Path.GetExtension(f), ".exe", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exes.Count == 0)
        {
            MessageBox.Show(Window.GetWindow(this), "Arrastra archivos .exe para anadirlos como juegos.",
                "Anadir juegos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var covers = new CoverArtService();

        foreach (var exe in exes)
        {
            var alreadyAdded = _library.Games.Any(g =>
                string.Equals(g.ExecutablePath, exe, StringComparison.OrdinalIgnoreCase));
            if (alreadyAdded) continue;

            var game = new Game
            {
                Name = Path.GetFileNameWithoutExtension(exe),
                ExecutablePath = exe,
                Source = GameSource.Manual
            };

            // Primero aparece la tarjeta; la portada llega despues si se encuentra.
            _library.Add(game);
            _view.Refresh();
            UpdateEmptyState();

            var cover = await covers.SearchAndDownloadAsync(game.Name, game.Id);
            if (cover != null)
            {
                game.CoverImagePath = cover;
                _library.Update(game);
            }
        }
    }

    // ===================== Filtro / busqueda / orden =====================

    private bool FilterPredicate(object obj)
    {
        if (obj is not Game game) return false;

        var showingHidden = _filter == FilterMode.Hidden;
        if (game.Hidden != showingHidden) return false;

        switch (_filter)
        {
            case FilterMode.Favorites when !game.Favorite:
            case FilterMode.Steam when game.Source != GameSource.Steam:
            case FilterMode.Epic when game.Source != GameSource.Epic:
            case FilterMode.Manual when game.Source != GameSource.Manual:
                return false;
        }

        if (!string.IsNullOrWhiteSpace(_search) &&
            !game.Name.Contains(_search, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private void ApplySort()
    {
        _view.SortDescriptions.Clear();

        switch (SortCombo.SelectedIndex)
        {
            case 1: // Ultimo jugado
                _view.SortDescriptions.Add(new SortDescription(nameof(Game.LastPlayed), ListSortDirection.Descending));
                break;
            case 2: // Mas jugado
                _view.SortDescriptions.Add(new SortDescription(nameof(Game.PlayTimeMinutes), ListSortDirection.Descending));
                break;
            case 3: // Favoritos primero
                _view.SortDescriptions.Add(new SortDescription(nameof(Game.Favorite), ListSortDirection.Descending));
                _view.SortDescriptions.Add(new SortDescription(nameof(Game.Name), ListSortDirection.Ascending));
                break;
            default: // Nombre
                _view.SortDescriptions.Add(new SortDescription(nameof(Game.Name), ListSortDirection.Ascending));
                break;
        }
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = _view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text;
        _view.Refresh();
        UpdateEmptyState();
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_view == null) return;
        ApplySort();
        _view.Refresh();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_view == null) return;

        _filter = sender switch
        {
            var s when s == FilterFavorites => FilterMode.Favorites,
            var s when s == FilterSteam => FilterMode.Steam,
            var s when s == FilterEpic => FilterMode.Epic,
            var s when s == FilterManual => FilterMode.Manual,
            var s when s == FilterHidden => FilterMode.Hidden,
            _ => FilterMode.All
        };

        _view.Refresh();
        UpdateEmptyState();
    }

    // ===================== Acciones =====================

    private void AddGame_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddEditGameWindow { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            _library.Add(dialog.Result);
            _view.Refresh();
            UpdateEmptyState();
        }
    }

    /// <summary>Sincroniza con Steam/Epic y devuelve cuantos juegos nuevos se anadieron.
    /// Compartido por el boton manual y por la sincronizacion al abrir.</summary>
    private async Task<int> RunSyncAsync()
    {
        if (_syncing) return 0;
        _syncing = true;

        SyncButton.IsEnabled = false;
        SyncButton.Content = "Sincronizando...";

        try
        {
            var providers = new List<IGameProvider>();
            if (LibraryPathsService.Instance.HasSteamPath) providers.Add(new SteamGameProvider());
            if (LibraryPathsService.Instance.HasEpicPath) providers.Add(new EpicGameProvider());

            var beforeCount = _library.Games.Count;
            await _library.SyncAsync(providers);

            _view.Refresh();
            UpdateEmptyState();

            return _library.Games.Count - beforeCount;
        }
        finally
        {
            _syncing = false;
            SyncButton.IsEnabled = true;
            SyncButton.Content = "Sincronizar";
        }
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        var settings = LibraryPathsService.Instance;

        // Si no hay ninguna carpeta configurada todavia, pedimos antes de sincronizar
        // en vacio (lo cual solo confundiria: "sincronice y no aparecio nada").
        if (!settings.HasSteamPath && !settings.HasEpicPath)
        {
            var configured = OpenFolderConfig();
            if (!configured) return; // el usuario cancelo, no sincronizamos
        }

        if (_syncing) return;

        var added = await RunSyncAsync();

        MessageBox.Show(
            Window.GetWindow(this),
            added > 0 ? $"Se han anadido {added} juego(s) nuevo(s)." : "No se ha encontrado ningun juego nuevo.",
            "Sincronizacion",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ConfigureFolders_Click(object sender, RoutedEventArgs e) => OpenFolderConfig();

    /// <summary>Abre el dialogo de carpetas de Steam/Epic. Devuelve true si el usuario guardo.</summary>
    private bool OpenFolderConfig()
    {
        var dialog = new ConfigureSyncFoldersWindow { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        return dialog.Saved;
    }

    private void LaunchGame(Game game, bool asAdmin = false)
    {
        if (_library.Launch(game, asAdmin)) return;

        MessageBox.Show(Window.GetWindow(this),
            $"No se ha podido lanzar \"{game.Name}\". Comprueba que la ruta del ejecutable sigue siendo valida (Editar).",
            "Lanzar juego", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static bool IsLocalFile(Game game) =>
        !game.ExecutablePath.Contains("://", StringComparison.Ordinal) && File.Exists(game.ExecutablePath);

    private Game GetGameFromMenuItem(object sender)
    {
        var menuItem = (MenuItem)sender;
        var contextMenu = (ContextMenu)menuItem.Parent;
        var owner = (FrameworkElement)contextMenu.PlacementTarget;
        return (Game)owner.DataContext;
    }

    private void Card_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: Game game, ContextMenu: { } menu }) return;

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            switch (item.Tag as string)
            {
                case "Favorite":
                    item.Header = game.Favorite ? "Quitar de favoritos" : "Marcar como favorito";
                    break;
                case "Hide":
                    item.Header = game.Hidden ? "Mostrar" : "Ocultar";
                    break;
                case "Admin":
                case "OpenFolder":
                    // Solo tienen sentido con un .exe local, no con steam:// ni protocolos.
                    item.IsEnabled = IsLocalFile(game);
                    break;
            }
        }
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (((FrameworkElement)sender).DataContext is Game game)
        {
            LaunchGame(game);
        }
    }

    private void PlayMenuItem_Click(object sender, RoutedEventArgs e) => LaunchGame(GetGameFromMenuItem(sender));

    private void InfoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var game = GetGameFromMenuItem(sender);
        new GameDetailsWindow(game) { Owner = Window.GetWindow(this) }.ShowDialog();
    }
    
    private void RunAsAdminMenuItem_Click(object sender, RoutedEventArgs e) =>
        LaunchGame(GetGameFromMenuItem(sender), asAdmin: true);

    private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var game = GetGameFromMenuItem(sender);
        if (!IsLocalFile(game)) return;

        try
        {
            // Abre el explorador con el .exe seleccionado.
            Process.Start("explorer.exe", $"/select,\"{game.ExecutablePath}\"");
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo abrir la carpeta del juego", ex);
        }
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var game = GetGameFromMenuItem(sender);
        var dialog = new AddEditGameWindow(game) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            _library.Update(game);
            _view.Refresh();
        }
    }

    private void FavoriteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var game = GetGameFromMenuItem(sender);
        game.Favorite = !game.Favorite;
        _library.Update(game);
        _view.Refresh();
    }

    private void HideMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var game = GetGameFromMenuItem(sender);
        game.Hidden = !game.Hidden;
        _library.Update(game);
        _view.Refresh();
        UpdateEmptyState();
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var game = GetGameFromMenuItem(sender);

        if (AppSettingsService.Instance.ConfirmDelete)
        {
            var fromStore = game.Source != GameSource.Manual && !string.IsNullOrWhiteSpace(game.ExternalId);
            var extra = fromStore
                ? "\n\nNo volvera a aparecer al sincronizar. Puedes restaurarlo en Configuracion > Juegos."
                : "";

            var result = MessageBox.Show(
                Window.GetWindow(this),
                $"Se quitara \"{game.Name}\" del launcher. Esto no borra el juego ni sus archivos.{extra}",
                "Eliminar juego",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;
        }

        _library.Remove(game);
        _view.Refresh();
        UpdateEmptyState();
    }
}