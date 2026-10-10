using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Launcher.Core.Models;

namespace Launcher.Modules.Commands;

public partial class CommandsView : UserControl
{
    private enum FilterMode { All, Apps, Peripherals, SystemTools, Cli }

    private readonly CommandsService _service = new();
    private readonly ICollectionView _view;

    private FilterMode _filter = FilterMode.All;
    private string _search = "";

    public CommandsView()
    {
        InitializeComponent();

        _view = CollectionViewSource.GetDefaultView(_service.Commands);
        _view.Filter = FilterPredicate;
        ApplySort();

        CommandsItems.ItemsSource = _view;
        UpdateEmptyState();
    }

    // ===================== Filtro / busqueda / orden =====================

    private bool FilterPredicate(object obj)
    {
        if (obj is not CommandShortcut cmd) return false;

        switch (_filter)
        {
            case FilterMode.Apps when cmd.Category != CommandCategory.App:
            case FilterMode.Peripherals when cmd.Category != CommandCategory.Peripheral:
            case FilterMode.SystemTools when cmd.Category != CommandCategory.System:
            case FilterMode.Cli when cmd.Category != CommandCategory.Cli:
                return false;
        }

        if (!string.IsNullOrWhiteSpace(_search) &&
            !cmd.Name.Contains(_search, StringComparison.OrdinalIgnoreCase))
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
            case 1: // Favoritos primero
                _view.SortDescriptions.Add(new SortDescription(nameof(CommandShortcut.Favorite), ListSortDirection.Descending));
                _view.SortDescriptions.Add(new SortDescription(nameof(CommandShortcut.Name), ListSortDirection.Ascending));
                break;
            case 2: // Usado recientemente
                _view.SortDescriptions.Add(new SortDescription(nameof(CommandShortcut.LastUsed), ListSortDirection.Descending));
                break;
            default: // Nombre
                _view.SortDescriptions.Add(new SortDescription(nameof(CommandShortcut.Name), ListSortDirection.Ascending));
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
            var s when s == FilterApps => FilterMode.Apps,
            var s when s == FilterPeripherals => FilterMode.Peripherals,
            var s when s == FilterSystem => FilterMode.SystemTools,
            var s when s == FilterCli => FilterMode.Cli,
            _ => FilterMode.All
        };

        _view.Refresh();
        UpdateEmptyState();
    }

    // ===================== Acciones =====================

    private void AddCommand_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddEditCommandWindow { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            _service.Add(dialog.Result);
            _view.Refresh();
            UpdateEmptyState();
        }
    }

    private void Suggestions_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CommandSuggestionsWindow(_service.Commands) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.Result.Count > 0)
        {
            _service.AddRange(dialog.Result);
            _view.Refresh();
            UpdateEmptyState();
        }
    }

    private CommandShortcut GetCommandFromMenuItem(object sender)
    {
        var menuItem = (MenuItem)sender;
        var contextMenu = (ContextMenu)menuItem.Parent;
        var owner = (FrameworkElement)contextMenu.PlacementTarget;
        return (CommandShortcut)owner.DataContext;
    }

    private void Card_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CommandShortcut cmd, ContextMenu: { } menu }) return;

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Tag as string == "Favorite")
            {
                item.Header = cmd.Favorite ? "Quitar de favoritos" : "Marcar como favorito";
            }
        }
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (((FrameworkElement)sender).DataContext is CommandShortcut cmd)
        {
            _service.Launch(cmd);
        }
    }

    private void RunMenuItem_Click(object sender, RoutedEventArgs e) => _service.Launch(GetCommandFromMenuItem(sender));

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var cmd = GetCommandFromMenuItem(sender);
        var dialog = new AddEditCommandWindow(cmd) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            _service.Update(cmd);
            _view.Refresh();
        }
    }

    private void FavoriteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var cmd = GetCommandFromMenuItem(sender);
        cmd.Favorite = !cmd.Favorite;
        _service.Update(cmd);
        _view.Refresh();
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var cmd = GetCommandFromMenuItem(sender);
        var result = MessageBox.Show(
            Window.GetWindow(this),
            $"Se quitara \"{cmd.Name}\" de Comandos.",
            "Eliminar acceso directo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _service.Remove(cmd);
            _view.Refresh();
            UpdateEmptyState();
        }
    }
}