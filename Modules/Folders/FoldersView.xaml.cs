using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.Folders;

public partial class FoldersView : UserControl
{
    private readonly FolderShortcutsService _service = new();

    public FoldersView()
    {
        InitializeComponent();
        Refresh();
    }

    /// <summary>ItemsSource es una lista simple (sin favoritos ni orden, de
    /// momento) filtrada por Hidden; se reconstruye tras cada cambio.</summary>
    private void Refresh()
    {
        FoldersItems.ItemsSource = _service.Folders.Where(f => !f.Hidden).ToList();
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = _service.Folders.Any(f => !f.Hidden) ? Visibility.Collapsed : Visibility.Visible;
    }

    // ===================== Acciones =====================

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

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (((FrameworkElement)sender).DataContext is FolderShortcut folder)
        {
            _service.Open(folder);
        }
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e) => _service.Open(GetFolderFromMenuItem(sender));

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
