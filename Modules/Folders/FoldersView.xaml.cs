using System.Diagnostics;
using System.IO;
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

    // Explorador del panel lateral
    private readonly Stack<string> _back = new();
    private string? _currentPath;

    public FoldersView()
    {
        InitializeComponent();
        Refresh();

        // Peticiones externas (buscador, asistente...): abrir una ruta en el panel.
        FolderNavigationService.PathRequested += OnPathRequested;
        Loaded += (_, _) => ConsumePending();
        Unloaded += (_, _) => { }; // la vista se cachea en MainWindow: no desuscribimos
    }

    private void OnPathRequested() => Dispatcher.BeginInvoke(ConsumePending);

    private void ConsumePending()
    {
        var path = FolderNavigationService.TakePending();
        if (path != null) OpenInPanel(path);
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

    // ===================== Panel lateral (explorador) =====================

    /// <summary>Abre una carpeta nueva en el panel (apila la anterior para "Atras").</summary>
    public void OpenInPanel(string path)
    {
        string? fileToSelect = null;

        if (File.Exists(path))
        {
            fileToSelect = path;
            path = Path.GetDirectoryName(path) ?? path;
        }

        if (!Directory.Exists(path)) return;

        if (_currentPath != null &&
            !string.Equals(_currentPath, path, StringComparison.OrdinalIgnoreCase))
        {
            _back.Push(_currentPath);
        }

        ShowDirectory(path);

        if (fileToSelect != null)
        {
            var entry = (EntriesList.ItemsSource as List<FileSystemEntry>)?
                .FirstOrDefault(e => string.Equals(e.FullPath, fileToSelect, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                EntriesList.SelectedItem = entry;
                EntriesList.ScrollIntoView(entry);
            }
        }
    }

    private void ShowDirectory(string path)
    {
        _currentPath = path;
        SidePanel.Visibility = Visibility.Visible;

        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        PanelTitle.Text = string.IsNullOrEmpty(name) ? path : name;
        PanelPath.Text = path;

        BackButton.IsEnabled = _back.Count > 0;
        UpButton.IsEnabled = Directory.GetParent(path) != null;

        var entries = new List<FileSystemEntry>();
        try
        {
            var info = new DirectoryInfo(path);

            foreach (var dir in info.EnumerateDirectories()
                         .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden) &&
                                     !d.Attributes.HasFlag(FileAttributes.System))
                         .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                entries.Add(new FileSystemEntry { Name = dir.Name, FullPath = dir.FullName, IsDirectory = true });
            }

            foreach (var file in info.EnumerateFiles()
                         .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden) &&
                                     !f.Attributes.HasFlag(FileAttributes.System))
                         .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                entries.Add(new FileSystemEntry
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false,
                    Detail = FileSystemEntry.FormatSize(file.Length)
                });
            }

            PanelMessage.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            PanelMessage.Text = "> carpeta vacia_";
        }
        catch (UnauthorizedAccessException)
        {
            PanelMessage.Text = "> sin permiso para leer esta carpeta_";
            PanelMessage.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo leer la carpeta {path}", ex);
            PanelMessage.Text = "> no se pudo leer la carpeta_";
            PanelMessage.Visibility = Visibility.Visible;
        }

        EntriesList.ItemsSource = entries;
    }

    private void Entries_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EntriesList.SelectedItem is FileSystemEntry entry) ActivateEntry(entry);
    }

    private void Entries_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && EntriesList.SelectedItem is FileSystemEntry entry)
        {
            ActivateEntry(entry);
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            Up_Click(sender, e);
            e.Handled = true;
        }
    }

    private void ActivateEntry(FileSystemEntry entry)
    {
        if (entry.IsDirectory)
        {
            OpenInPanel(entry.FullPath);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = entry.FullPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo abrir {entry.FullPath}", ex);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_back.Count == 0) return;
        ShowDirectory(_back.Pop());
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPath == null) return;
        var parent = Directory.GetParent(_currentPath);
        if (parent != null) OpenInPanel(parent.FullName);
    }

    private void ClosePanel_Click(object sender, RoutedEventArgs e)
    {
        SidePanel.Visibility = Visibility.Collapsed;
        _currentPath = null;
        _back.Clear();
        EntriesList.ItemsSource = null;
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

    private void OpenShortcutInPanel(FolderShortcut folder)
    {
        if (!folder.Exists)
        {
            MessageBox.Show(Window.GetWindow(this),
                $"La carpeta \"{folder.Name}\" ya no existe en:\n{folder.Path}",
                "Carpeta no encontrada", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Al abrir desde un acceso directo empezamos un recorrido nuevo.
        _back.Clear();
        _currentPath = null;
        OpenInPanel(folder.Path);
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Un solo clic abre en el panel (es lo que se espera de un acceso directo).
        if (((FrameworkElement)sender).DataContext is FolderShortcut folder)
        {
            OpenShortcutInPanel(folder);
        }
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e) =>
        OpenShortcutInPanel(GetFolderFromMenuItem(sender));

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
