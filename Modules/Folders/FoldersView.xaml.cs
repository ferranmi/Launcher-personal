using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.Folders;

public partial class FoldersView : UserControl
{
    private const double DefaultPanelWidth = 680;
    private const double PanelMinWidth = 380;

    private readonly FolderShortcutsService _service = new();

    private bool _panelOpen;
    private bool _expanded;
    private double _panelWidth = DefaultPanelWidth;

    public FoldersView()
    {
        InitializeComponent();
        Refresh();

        Explorer.CloseRequested += () => SetPanelOpen(false);
        Explorer.ExpandToggled += SetExpanded;

        // Peticiones externas (buscador, asistente, juegos...): abrir una ruta en el explorador.
        FolderNavigationService.PathRequested += OnPathRequested;
        Loaded += (_, _) => ConsumePending();
    }

    private void OnPathRequested() => Dispatcher.BeginInvoke(ConsumePending);

    private void ConsumePending()
    {
        var path = FolderNavigationService.TakePending();
        if (path != null) OpenInPanel(path);
    }

    /// <summary>Abre el explorador (si estaba cerrado) y navega a la ruta.</summary>
    public void OpenInPanel(string path)
    {
        SetPanelOpen(true);
        Explorer.Navigate(path);
    }

    // ===================== Panel =====================

    private void SetPanelOpen(bool open)
    {
        if (open == _panelOpen) return;

        if (open)
        {
            PanelCol.MinWidth = PanelMinWidth;
            PanelCol.Width = new GridLength(_panelWidth);
            Explorer.Visibility = Visibility.Visible;
            PanelSplitter.Visibility = Visibility.Visible;
        }
        else
        {
            if (_expanded)
            {
                Explorer.ResetExpand();
                SetExpanded(false);
            }

            if (PanelCol.ActualWidth > 0) _panelWidth = PanelCol.ActualWidth;
            PanelCol.MinWidth = 0;
            PanelCol.Width = new GridLength(0);
            Explorer.Visibility = Visibility.Collapsed;
            PanelSplitter.Visibility = Visibility.Collapsed;
        }

        _panelOpen = open;
    }

    /// <summary>Ampliado: el explorador ocupa todo el modulo y se ocultan los accesos.</summary>
    private void SetExpanded(bool expanded)
    {
        _expanded = expanded;

        if (expanded)
        {
            if (PanelCol.ActualWidth > 0) _panelWidth = PanelCol.ActualWidth;

            ShortcutsHost.Visibility = Visibility.Collapsed;
            ShortcutsCol.MinWidth = 0;
            ShortcutsCol.Width = new GridLength(0);
            PanelCol.Width = new GridLength(1, GridUnitType.Star);
            PanelSplitter.Visibility = Visibility.Collapsed;
        }
        else
        {
            ShortcutsHost.Visibility = Visibility.Visible;
            ShortcutsCol.MinWidth = 200;
            ShortcutsCol.Width = new GridLength(1, GridUnitType.Star);
            PanelCol.Width = new GridLength(_panelWidth);
            PanelSplitter.Visibility = _panelOpen ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ===================== Accesos directos =====================

    private void Refresh()
    {
        FoldersItems.ItemsSource = _service.Folders.Where(f => !f.Hidden).ToList();
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = _service.Folders.Any(f => !f.Hidden) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddEditFolderWindow { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;

        var alreadyExists = _service.Folders.Any(f =>
            string.Equals(f.Path, dialog.Result.Path, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            MessageBox.Show(Window.GetWindow(this),
                "Esa carpeta ya esta anadida.", "Carpeta duplicada",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _service.Add(dialog.Result);
        Refresh();
    }

    private FolderShortcut GetFolderFromMenuItem(object sender)
    {
        var menuItem = (MenuItem)sender;
        var contextMenu = (ContextMenu)menuItem.Parent;
        var owner = (FrameworkElement)contextMenu.PlacementTarget;
        return (FolderShortcut)owner.DataContext;
    }

    private void OpenShortcut(FolderShortcut folder)
    {
        if (!folder.Exists)
        {
            MessageBox.Show(Window.GetWindow(this),
                $"La carpeta \"{folder.Name}\" ya no existe en:\n{folder.Path}",
                "Carpeta no encontrada", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        OpenInPanel(folder.Path);
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is FolderShortcut folder) OpenShortcut(folder);
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e) => OpenShortcut(GetFolderFromMenuItem(sender));

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var folder = GetFolderFromMenuItem(sender);
        var dialog = new AddEditFolderWindow(folder) { Owner = Window.GetWindow(this) };

        if (dialog.ShowDialog() != true) return;

        if (dialog.DeleteRequested)
        {
            ConfirmAndDelete(folder);
            return;
        }

        _service.Update(folder);
        Refresh();
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e) => ConfirmAndDelete(GetFolderFromMenuItem(sender));

    private void ConfirmAndDelete(FolderShortcut folder)
    {
        var result = MessageBox.Show(
            Window.GetWindow(this),
            $"Se quitara el acceso a \"{folder.Name}\". Esto no borra la carpeta ni su contenido.",
            "Eliminar carpeta",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _service.Remove(folder);
            Refresh();
        }
    }
}
