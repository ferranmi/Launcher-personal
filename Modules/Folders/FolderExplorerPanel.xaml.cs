using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Microsoft.VisualBasic.FileIO;

namespace Launcher.Modules.Folders;

/// <summary>
/// Explorador de carpetas integrado (sustituye al Explorador de Windows): navegacion
/// con historial, vistas Detalles/Iconos con miniaturas, orden y filtro, visor de
/// imagenes y videos con paso al siguiente/anterior, vista previa, y operaciones
/// de archivos (copiar, cortar, pegar, renombrar, eliminar a la papelera, nueva carpeta).
/// </summary>
public partial class FolderExplorerPanel : UserControl
{
    private enum ViewMode { Details, Icons }
    private enum SortKey { Name, Modified, Type, Size }

    public event Action? CloseRequested;
    public event Action<bool>? ExpandToggled;

    // Navegacion
    private readonly Stack<string> _back = new();
    private readonly Stack<string> _forward = new();
    private string? _currentPath;
    private int _loadVersion;
    private string _error = "";

    // Contenido
    private List<FileSystemEntry> _all = new();
    private List<FileSystemEntry> _visible = new();
    private CancellationTokenSource _thumbCts = new();
    private CancellationTokenSource _previewCts = new();

    // Vista
    private ViewMode _viewMode = ViewMode.Details;
    private SortKey _sortKey = SortKey.Name;
    private bool _sortDesc;
    private string _filter = "";
    private bool _expanded;
    private bool _previewOn = true;
    private readonly bool _ready;

    // Observador de cambios en disco
    private FileSystemWatcher? _watcher;
    private readonly DispatcherTimer _refreshTimer;

    // Visor
    private List<FileSystemEntry> _media = new();
    private int _mediaIndex;
    private bool _videoPlaying;
    private bool _draggingSeek;
    private readonly DispatcherTimer _videoTimer;

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string a, string b);

    public FolderExplorerPanel()
    {
        InitializeComponent();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            if (_currentPath != null) _ = LoadAsync(_currentPath, null, SelectedPaths());
        };

        _videoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _videoTimer.Tick += (_, _) => UpdateVideoProgress();

        EntriesList.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(Header_Click));
        ApplyViewMode();
        UpdateNavButtons();

        Unloaded += (_, _) => CloseViewer();
        _ready = true;
    }

    // ===================== Navegacion =====================

    /// <summary>Abre una carpeta (o la carpeta de un archivo, seleccionandolo).</summary>
    public void Navigate(string path)
    {
        string? select = null;
        if (File.Exists(path))
        {
            select = path;
            path = Path.GetDirectoryName(path) ?? path;
        }

        if (!Directory.Exists(path)) return;
        path = Path.GetFullPath(path);

        if (_currentPath != null && !SamePath(_currentPath, path))
        {
            _back.Push(_currentPath);
            _forward.Clear();
        }

        _ = LoadAsync(path, select);
    }

    /// <summary>Si el panel se cierra con el modo ampliado activo, FoldersView lo desactiva.</summary>
    public void ResetExpand()
    {
        _expanded = false;
        ExpandButton.Content = "\uE740";
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(a.TrimEnd(Path.DirectorySeparatorChar), b.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private void GoBack()
    {
        if (_back.Count == 0 || _currentPath == null) return;
        _forward.Push(_currentPath);
        _ = LoadAsync(_back.Pop());
    }

    private void GoForward()
    {
        if (_forward.Count == 0 || _currentPath == null) return;
        _back.Push(_currentPath);
        _ = LoadAsync(_forward.Pop());
    }

    private void GoUp()
    {
        if (_currentPath == null) return;
        var parent = Directory.GetParent(_currentPath);
        if (parent != null) Navigate(parent.FullName);
    }

    private void UpdateNavButtons()
    {
        BackButton.IsEnabled = _back.Count > 0;
        ForwardButton.IsEnabled = _forward.Count > 0;
        UpButton.IsEnabled = _currentPath != null && Directory.GetParent(_currentPath) != null;
    }

    private async Task LoadAsync(string path, string? selectPath = null, HashSet<string>? keep = null)
    {
        var version = ++_loadVersion;

        // Si se recarga la misma carpeta, conservamos las miniaturas ya cargadas.
        Dictionary<string, FileSystemEntry>? previous = null;
        if (_currentPath != null && SamePath(_currentPath, path))
        {
            previous = new Dictionary<string, FileSystemEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in _all) previous.TryAdd(e.FullPath, e);
        }

        _currentPath = path;
        _thumbCts.Cancel();
        _thumbCts = new CancellationTokenSource();
        AddressBox.Text = path;
        SetWatcher(path);
        UpdateNavButtons();

        List<FileSystemEntry> list;
        var error = "";
        try
        {
            list = await Task.Run(() => Enumerate(path));
        }
        catch (UnauthorizedAccessException)
        {
            list = new List<FileSystemEntry>();
            error = "> sin permiso para leer esta carpeta_";
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo leer la carpeta {path}", ex);
            list = new List<FileSystemEntry>();
            error = "> no se pudo leer la carpeta_";
        }

        if (version != _loadVersion) return; // el usuario ya navego a otra carpeta

        if (previous != null)
        {
            foreach (var entry in list)
            {
                if (previous.TryGetValue(entry.FullPath, out var old) && old.Modified == entry.Modified)
                    entry.AdoptThumbnail(old.Thumbnail);
            }
        }

        _all = list;
        _error = error;
        ApplyView(selectPath, keep);
    }

    private static List<FileSystemEntry> Enumerate(string path)
    {
        var result = new List<FileSystemEntry>();
        foreach (var info in new DirectoryInfo(path).EnumerateFileSystemInfos())
        {
            try
            {
                if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                result.Add(new FileSystemEntry(info));
            }
            catch
            {
                // entrada que desaparece o sin acceso mientras se lista: se omite
            }
        }
        return result;
    }

    // ===================== Filtro, orden y vista =====================

    private void ApplyView(string? selectPath = null, HashSet<string>? keep = null)
    {
        IEnumerable<FileSystemEntry> query = _all;
        if (_filter.Length > 0)
            query = query.Where(e => e.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase));

        var list = query.ToList();
        list.Sort(CompareEntries);
        _visible = list;
        EntriesList.ItemsSource = list;

        if (_error.Length > 0)
            ShowMessage(_error);
        else if (list.Count == 0)
            ShowMessage(_filter.Length > 0 ? "> sin coincidencias_" : "> carpeta vacia_");
        else
            MessageText.Visibility = Visibility.Collapsed;

        var toSelect = new List<FileSystemEntry>();
        if (selectPath != null)
        {
            var match = list.FirstOrDefault(e => string.Equals(e.FullPath, selectPath, StringComparison.OrdinalIgnoreCase));
            if (match != null) toSelect.Add(match);
        }
        else if (keep != null && keep.Count > 0)
        {
            toSelect.AddRange(list.Where(e => keep.Contains(e.FullPath)));
        }

        foreach (var entry in toSelect) EntriesList.SelectedItems.Add(entry);
        if (toSelect.Count > 0)
        {
            var first = toSelect[0];
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => EntriesList.ScrollIntoView(first));
        }

        UpdateStatus();
    }

    private void ShowMessage(string text)
    {
        MessageText.Text = text;
        MessageText.Visibility = Visibility.Visible;
    }

    private int CompareEntries(FileSystemEntry a, FileSystemEntry b)
    {
        if (a.IsDirectory != b.IsDirectory) return a.IsDirectory ? -1 : 1; // carpetas siempre primero

        var result = _sortKey switch
        {
            SortKey.Modified => a.Modified.CompareTo(b.Modified),
            SortKey.Type => string.Compare(a.TypeName, b.TypeName, StringComparison.CurrentCultureIgnoreCase),
            SortKey.Size => a.Size.CompareTo(b.Size),
            _ => 0
        };

        if (result == 0) result = StrCmpLogicalW(a.Name, b.Name); // orden natural: img2 antes que img10
        return _sortDesc ? -result : result;
    }

    private void ApplyViewMode()
    {
        if (_viewMode == ViewMode.Details)
        {
            EntriesList.ItemTemplate = null;
            EntriesList.ItemsPanel = (ItemsPanelTemplate)FindResource("DetailsPanel");
            EntriesList.ItemContainerStyle = (Style)FindResource("DetailsItem");
            EntriesList.View = (ViewBase)FindResource("DetailsView");
            ScrollViewer.SetHorizontalScrollBarVisibility(EntriesList, ScrollBarVisibility.Auto);
        }
        else
        {
            EntriesList.View = null;
            EntriesList.ItemsPanel = (ItemsPanelTemplate)FindResource("IconsPanel");
            EntriesList.ItemContainerStyle = (Style)FindResource("IconItem");
            EntriesList.ItemTemplate = (DataTemplate)FindResource("IconTemplate");
            ScrollViewer.SetHorizontalScrollBarVisibility(EntriesList, ScrollBarVisibility.Disabled);
        }
    }

    private void SetSort(SortKey key, bool? desc = null)
    {
        if (desc != null) _sortDesc = desc.Value;
        else if (key == _sortKey) _sortDesc = !_sortDesc;
        else _sortDesc = false;

        _sortKey = key;
        SortCombo.SelectedIndex = (int)key;
        SortDirButton.Content = _sortDesc ? "\uE74B" : "\uE74A";
        ApplyView(null, SelectedPaths());
    }

    private void Header_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Role: not GridViewColumnHeaderRole.Padding } header) return;
        if (header.Column?.Header is not string title) return;

        SortKey? key = title switch
        {
            "Nombre" => SortKey.Name,
            "Fecha" => SortKey.Modified,
            "Tipo" => SortKey.Type,
            "Tamano" => SortKey.Size,
            _ => null
        };

        if (key != null) SetSort(key.Value);
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        var key = (SortKey)Math.Max(0, SortCombo.SelectedIndex);
        if (key != _sortKey) SetSort(key, false);
    }

    private void SortDir_Click(object sender, RoutedEventArgs e) => SetSort(_sortKey, !_sortDesc);

    private void ViewCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _viewMode = ViewCombo.SelectedIndex == 1 ? ViewMode.Icons : ViewMode.Details;
        ApplyViewMode();
    }

    private void Filter_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        _filter = FilterBox.Text.Trim();
        ApplyView();
    }

    private void Filter_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        FilterBox.Clear();
        EntriesList.Focus();
        e.Handled = true;
    }

    private void IconItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is FileSystemEntry entry)
            entry.RequestThumbnail(_thumbCts.Token);
    }

    // ===================== Barra de herramientas =====================

    private void Back_Click(object sender, RoutedEventArgs e) => GoBack();
    private void Forward_Click(object sender, RoutedEventArgs e) => GoForward();
    private void Up_Click(object sender, RoutedEventArgs e) => GoUp();
    private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();
    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void Expand_Click(object sender, RoutedEventArgs e)
    {
        _expanded = !_expanded;
        ExpandButton.Content = _expanded ? "\uE73F" : "\uE740";
        ExpandToggled?.Invoke(_expanded);
    }

    private void PreviewToggle_Click(object sender, RoutedEventArgs e)
    {
        _previewOn = !_previewOn;
        PreviewPane.Visibility = _previewOn ? Visibility.Visible : Visibility.Collapsed;
        PreviewToggleButton.Foreground = (Brush)FindResource(_previewOn ? "Brush.Accent" : "Brush.TextSecondary");
        if (_previewOn) _ = UpdatePreviewAsync();
    }

    private void NewFolder_Click(object sender, RoutedEventArgs e) => NewFolder();

    private void Drives_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender };

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                var root = drive.RootDirectory.FullName;
                var item = new MenuItem { Header = $"{drive.Name}  {drive.VolumeLabel}".TrimEnd() };
                item.Click += (_, _) => Navigate(root);
                menu.Items.Add(item);
            }
            catch
            {
                // unidad que deja de estar lista mientras se lista
            }
        }

        menu.IsOpen = true;
    }

    private void Address_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var text = AddressBox.Text.Trim().Trim('"');
            if (Directory.Exists(text) || File.Exists(text))
            {
                Navigate(text);
                EntriesList.Focus();
            }
            else
            {
                AddressBox.Foreground = (Brush)FindResource("Brush.Danger");
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            AddressBox.Text = _currentPath ?? "";
            AddressBox.Foreground = (Brush)FindResource("Brush.TextPrimary");
            EntriesList.Focus();
            e.Handled = true;
        }
        else
        {
            AddressBox.Foreground = (Brush)FindResource("Brush.TextPrimary");
        }
    }

    // ===================== Seleccion, estado y vista previa =====================

    private List<FileSystemEntry> Selected => EntriesList.SelectedItems.Cast<FileSystemEntry>().ToList();

    private HashSet<string> SelectedPaths() =>
        Selected.Select(e => e.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void Entries_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStatus();
        _ = UpdatePreviewAsync();
    }

    private void UpdateStatus()
    {
        var sel = Selected;
        var text = $"{_visible.Count} elementos";

        if (sel.Count > 0)
        {
            text += $"  \u00b7  {sel.Count} seleccionado(s)";
            var bytes = sel.Where(s => !s.IsDirectory).Sum(s => s.Size);
            if (bytes > 0) text += $"  \u00b7  {FileSystemEntry.FormatSize(bytes)}";
        }

        StatusText.Text = text;
    }

    private async Task UpdatePreviewAsync()
    {
        _previewCts.Cancel();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;

        if (!_previewOn) return;

        PreviewSnippet.Visibility = Visibility.Collapsed;
        var sel = Selected;

        if (sel.Count != 1)
        {
            PreviewImage.Source = null;
            PreviewTitle.Text = sel.Count == 0 ? "" : $"{sel.Count} elementos seleccionados";
            PreviewDetails.Text = "";
            return;
        }

        var entry = sel[0];
        var details = entry.IsDirectory
            ? $"Carpeta\nModificado: {entry.ModifiedText}\nCreado: {entry.Created:dd/MM/yyyy HH:mm}"
            : $"{entry.TypeName}  \u00b7  {entry.SizeText}\nModificado: {entry.ModifiedText}\nCreado: {entry.Created:dd/MM/yyyy HH:mm}";

        PreviewTitle.Text = entry.Name;
        PreviewDetails.Text = details;
        PreviewImage.Source = entry.Thumbnail;

        var image = await ThumbnailService.GetAsync(entry.FullPath, entry.Kind == EntryKind.Image, 256, ct);
        if (ct.IsCancellationRequested) return;
        if (image != null) PreviewImage.Source = image;

        if (entry.Kind == EntryKind.Image)
        {
            var dims = await Task.Run(() => ImageSize(entry.FullPath));
            if (ct.IsCancellationRequested || dims == null) return;
            PreviewDetails.Text = details + $"\nDimensiones: {dims}";
        }
        else if (entry.Kind == EntryKind.Text)
        {
            var snippet = await Task.Run(() => ReadSnippet(entry.FullPath));
            if (ct.IsCancellationRequested || string.IsNullOrEmpty(snippet)) return;
            PreviewSnippet.Text = snippet;
            PreviewSnippet.Visibility = Visibility.Visible;
        }
    }

    private static string? ImageSize(string path)
    {
        try
        {
            var frame = BitmapFrame.Create(new Uri(path, UriKind.Absolute),
                BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            return $"{frame.PixelWidth} x {frame.PixelHeight}";
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadSnippet(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            var buffer = new char[500];
            var count = reader.Read(buffer, 0, buffer.Length);
            var text = new string(buffer, 0, count);
            return text.Contains('\0') ? null : text;
        }
        catch
        {
            return null;
        }
    }

    // ===================== Abrir elementos =====================

    private void Entries_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Solo si el doble clic fue sobre una fila (no sobre el fondo ni una cabecera).
        if (e.OriginalSource is not DependencyObject source) return;
        if (ItemsControl.ContainerFromElement(EntriesList, source) is not ListViewItem item) return;

        if (item.Content is FileSystemEntry entry) OpenEntry(entry);
    }

    private void OpenEntry(FileSystemEntry entry)
    {
        if (entry.IsDirectory)
        {
            Navigate(entry.FullPath);
            return;
        }

        if (entry.IsMedia)
        {
            OpenViewer(entry);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = entry.FullPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo abrir {entry.FullPath}", ex);
            Warn("No se pudo abrir el archivo.");
        }
    }

    private void Entries_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Clic derecho sobre una fila no seleccionada: la selecciona antes de abrir el menu.
        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(EntriesList, source) is ListViewItem { IsSelected: false } item)
        {
            EntriesList.SelectedItems.Clear();
            item.IsSelected = true;
        }
    }

    private void Entries_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Clic en el fondo vacio: deselecciona (como en el Explorador de Windows).
        if (e.OriginalSource is not DependencyObject source) return;
        if (ItemsControl.ContainerFromElement(EntriesList, source) != null) return;
        if (IsInside<ScrollBar>(source) || IsInside<GridViewColumnHeader>(source)) return;

        EntriesList.SelectedItems.Clear();
    }

    private static bool IsInside<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T) return true;
            d = (d is Visual) ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    // ===================== Menu contextual y atajos =====================

    private void Entries_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (EntriesList.ContextMenu == null) return;

        var count = Selected.Count;
        var canPaste = false;
        try { canPaste = Clipboard.ContainsFileDropList(); } catch { }

        foreach (var item in EntriesList.ContextMenu.Items.OfType<MenuItem>())
        {
            item.IsEnabled = (item.Tag as string) switch
            {
                "open" or "cut" or "copy" or "delete" or "copypath" => count > 0,
                "rename" => count == 1,
                "paste" => canPaste,
                _ => true
            };
        }
    }

    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        switch ((string)((MenuItem)sender).Tag)
        {
            case "open": OpenSelected(); break;
            case "cut": CopyOrCut(cut: true); break;
            case "copy": CopyOrCut(cut: false); break;
            case "paste": Paste(); break;
            case "rename": RenameSelected(); break;
            case "delete": DeleteSelected(); break;
            case "newfolder": NewFolder(); break;
            case "copypath": CopyPath(); break;
        }
    }

    private void OpenSelected()
    {
        var first = Selected.FirstOrDefault();
        if (first != null) OpenEntry(first);
    }

    private void Panel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        // Con el visor abierto, las flechas pasan de una imagen/video a otro.
        if (ViewerOverlay.Visibility == Visibility.Visible)
        {
            switch (key)
            {
                case Key.Left: case Key.Up: StepMedia(-1); break;
                case Key.Right: case Key.Down: StepMedia(1); break;
                case Key.Escape: CloseViewer(); break;
                case Key.Space: TogglePlay(); break;
                default: return;
            }
            e.Handled = true;
            return;
        }

        if ((ctrl && key == Key.L) || key == Key.F4)
        {
            AddressBox.Focus();
            AddressBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.FocusedElement is TextBox) return; // no robar teclas al escribir

        if (alt)
        {
            switch (key)
            {
                case Key.Left: GoBack(); break;
                case Key.Right: GoForward(); break;
                case Key.Up: GoUp(); break;
                default: return;
            }
            e.Handled = true;
            return;
        }

        if (ctrl)
        {
            switch (key)
            {
                case Key.C: CopyOrCut(cut: false); break;
                case Key.X: CopyOrCut(cut: true); break;
                case Key.V: Paste(); break;
                case Key.A: EntriesList.SelectAll(); break;
                case Key.N when shift: NewFolder(); break;
                default: return;
            }
            e.Handled = true;
            return;
        }

        switch (key)
        {
            case Key.Enter: OpenSelected(); break;
            case Key.Delete: DeleteSelected(); break;
            case Key.F2: RenameSelected(); break;
            case Key.F5: Reload(); break;
            case Key.Back: GoBack(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void Panel_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Botones laterales del raton: atras / adelante.
        if (e.ChangedButton == MouseButton.XButton1) { GoBack(); e.Handled = true; }
        else if (e.ChangedButton == MouseButton.XButton2) { GoForward(); e.Handled = true; }
    }

    // ===================== Operaciones con archivos =====================

    private void Reload()
    {
        if (_currentPath != null) _ = LoadAsync(_currentPath, null, SelectedPaths());
    }

    private void CopyOrCut(bool cut)
    {
        var paths = Selected.Select(s => s.FullPath).ToArray();
        if (paths.Length == 0) return;

        try
        {
            var files = new StringCollection();
            files.AddRange(paths);

            var data = new DataObject();
            data.SetFileDropList(files);
            // 2 = mover (cortar), 5 = copiar: asi tambien funciona pegar en otras apps.
            data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(cut ? 2 : 5)));
            Clipboard.SetDataObject(data, true);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo usar el portapapeles", ex);
        }
    }

    private async void Paste()
    {
        if (_currentPath == null) return;

        List<string> sources;
        var cut = false;
        try
        {
            if (!Clipboard.ContainsFileDropList()) return;
            sources = Clipboard.GetFileDropList().Cast<string>().ToList();

            if (Clipboard.GetDataObject()?.GetData("Preferred DropEffect") is MemoryStream ms && ms.Length >= 4)
            {
                var bytes = new byte[4];
                _ = ms.Read(bytes, 0, 4);
                cut = (BitConverter.ToInt32(bytes, 0) & 2) != 0;
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer el portapapeles", ex);
            return;
        }

        var target = _currentPath;
        var errors = new List<string>();
        var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            foreach (var source in sources)
            {
                try
                {
                    var clean = source.TrimEnd(Path.DirectorySeparatorChar);
                    var isDir = Directory.Exists(clean);
                    if (!isDir && !File.Exists(clean)) continue;

                    var name = Path.GetFileName(clean);
                    var sameFolder = SamePath(Path.GetDirectoryName(clean) ?? "", target);

                    if (cut && sameFolder) continue; // mover a donde ya esta: nada que hacer

                    if (isDir && (SamePath(clean, target) ||
                                  target.StartsWith(clean + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    {
                        errors.Add($"{name}: no se puede copiar una carpeta dentro de si misma.");
                        continue;
                    }

                    var dest = UniquePath(target, name);

                    if (isDir)
                    {
                        if (cut) FileSystem.MoveDirectory(clean, dest, UIOption.AllDialogs);
                        else FileSystem.CopyDirectory(clean, dest, UIOption.AllDialogs);
                    }
                    else
                    {
                        if (cut) FileSystem.MoveFile(clean, dest, UIOption.AllDialogs);
                        else FileSystem.CopyFile(clean, dest, UIOption.AllDialogs);
                    }

                    created.Add(dest);
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(source)}: {ex.Message}");
                }
            }
        });

        if (cut) { try { Clipboard.Clear(); } catch { } }

        ShowErrors(errors);
        await LoadAsync(target, null, created);
    }

    private async void DeleteSelected()
    {
        var sel = Selected;
        if (sel.Count == 0 || _currentPath == null) return;

        var message = sel.Count == 1
            ? $"Mover \"{sel[0].Name}\" a la papelera?"
            : $"Mover {sel.Count} elementos a la papelera?";

        var answer = MessageBox.Show(Window.GetWindow(this), message, "Eliminar",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        var errors = new List<string>();
        await Task.Run(() =>
        {
            foreach (var entry in sel)
            {
                try
                {
                    if (entry.IsDirectory)
                        FileSystem.DeleteDirectory(entry.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    else
                        FileSystem.DeleteFile(entry.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                catch (Exception ex)
                {
                    errors.Add($"{entry.Name}: {ex.Message}");
                }
            }
        });

        ShowErrors(errors);
        await LoadAsync(_currentPath);
    }

    private async void RenameSelected()
    {
        var sel = Selected;
        if (sel.Count != 1 || _currentPath == null) return;

        var entry = sel[0];
        var stem = entry.IsDirectory ? entry.Name : Path.GetFileNameWithoutExtension(entry.Name);
        var newName = Prompt("Cambiar nombre", entry.Name, stem.Length);
        if (string.IsNullOrWhiteSpace(newName) || newName == entry.Name) return;

        try
        {
            if (entry.IsDirectory) FileSystem.RenameDirectory(entry.FullPath, newName);
            else FileSystem.RenameFile(entry.FullPath, newName);
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
            return;
        }

        await LoadAsync(_currentPath, Path.Combine(_currentPath, newName));
    }

    private async void NewFolder()
    {
        if (_currentPath == null) return;

        var initial = Path.GetFileName(UniquePath(_currentPath, "Nueva carpeta", " "));
        var name = Prompt("Nueva carpeta", initial);
        if (string.IsNullOrWhiteSpace(name)) return;

        var path = Path.Combine(_currentPath, name);
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
            return;
        }

        await LoadAsync(_currentPath, path);
    }

    private void CopyPath()
    {
        var paths = Selected.Select(s => s.FullPath).ToList();
        if (paths.Count == 0) return;

        try { Clipboard.SetText(string.Join(Environment.NewLine, paths)); }
        catch (Exception ex) { LogService.Error("No se pudo copiar la ruta", ex); }
    }

    /// <summary>Si "name" ya existe en dir, devuelve "name - copia", "name - copia (2)"...</summary>
    private static string UniquePath(string dir, string name, string separator = " - copia")
    {
        var candidate = Path.Combine(dir, name);
        if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;

        var isFile = File.Exists(candidate);
        var stem = isFile ? Path.GetFileNameWithoutExtension(name) : name;
        var ext = isFile ? Path.GetExtension(name) : "";

        for (var i = 1; ; i++)
        {
            var suffix = separator == " - copia"
                ? (i == 1 ? " - copia" : $" - copia ({i})")
                : $" ({i + 1})";

            candidate = Path.Combine(dir, stem + suffix + ext);
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }

    private void Warn(string message) =>
        MessageBox.Show(Window.GetWindow(this), message, "Carpetas", MessageBoxButton.OK, MessageBoxImage.Warning);

    private void ShowErrors(List<string> errors)
    {
        if (errors.Count == 0) return;
        Warn("No se pudo completar:\n" + string.Join("\n", errors.Take(6)));
    }

    /// <summary>Cuadro de texto modal con el estilo del Launcher. Devuelve null si se cancela.</summary>
    private string? Prompt(string title, string initial, int selectLength = -1)
    {
        var box = new TextBox
        {
            Text = initial,
            Style = (Style)FindResource("TextBoxStyle"),
            Margin = new Thickness(0, 12, 0, 16)
        };

        var ok = new Button
        {
            Content = "Aceptar",
            Style = (Style)FindResource("PrimaryButtonStyle"),
            IsDefault = true,
            Margin = new Thickness(8, 0, 0, 0)
        };
        var cancel = new Button
        {
            Content = "Cancelar",
            Style = (Style)FindResource("SecondaryButtonStyle"),
            IsCancel = true
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);

        var content = new StackPanel { Margin = new Thickness(18) };
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = (FontFamily)FindResource("Font.Mono"),
            FontSize = 14,
            Foreground = (Brush)FindResource("Brush.TextPrimary")
        });
        content.Children.Add(box);
        content.Children.Add(buttons);

        var window = new Window
        {
            Content = new Border
            {
                Background = (Brush)FindResource("Brush.Background"),
                BorderBrush = (Brush)FindResource("Brush.BorderStrong"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Child = content
            },
            Width = 380,
            SizeToContent = SizeToContent.Height,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Owner = Window.GetWindow(this),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        string? result = null;
        ok.Click += (_, _) => { result = box.Text.Trim(); window.DialogResult = true; };
        window.Loaded += (_, _) =>
        {
            box.Focus();
            if (selectLength >= 0) box.Select(0, selectLength);
            else box.SelectAll();
        };

        return window.ShowDialog() == true ? result : null;
    }

    // ===================== Cambios en disco =====================

    private void SetWatcher(string path)
    {
        _watcher?.Dispose();
        _watcher = null;

        try
        {
            var watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            watcher.Created += (_, _) => KickRefresh();
            watcher.Deleted += (_, _) => KickRefresh();
            watcher.Changed += (_, _) => KickRefresh();
            watcher.Renamed += (_, _) => KickRefresh();
            _watcher = watcher;
        }
        catch
        {
            // sin permiso o ruta de red sin soporte: el usuario puede actualizar con F5
        }
    }

    /// <summary>Espera 400 ms de calma antes de recargar: una descarga o copia genera cientos de eventos.</summary>
    private void KickRefresh() =>
        Dispatcher.BeginInvoke(() => { _refreshTimer.Stop(); _refreshTimer.Start(); });

    // ===================== Visor de imagenes y videos =====================

    private void OpenViewer(FileSystemEntry entry)
    {
        _media = _visible.Where(e => e.IsMedia).ToList();
        _mediaIndex = Math.Max(0, _media.IndexOf(entry));

        ViewerOverlay.Visibility = Visibility.Visible;
        ShowMedia();
        ViewerOverlay.Focus();
    }

    private void ShowMedia()
    {
        if (_media.Count == 0) return;

        var entry = _media[_mediaIndex];
        ViewerTitle.Text = entry.Name;
        ViewerCounter.Text = $"{_mediaIndex + 1} / {_media.Count}";
        ViewerMessage.Visibility = Visibility.Collapsed;
        StopVideo();

        if (entry.Kind == EntryKind.Image)
        {
            ViewerVideo.Visibility = Visibility.Collapsed;
            VideoBar.Visibility = Visibility.Collapsed;
            ViewerImage.Visibility = Visibility.Visible;

            var image = LoadFullImage(entry.FullPath);
            ViewerImage.Source = image;
            if (image == null) ShowViewerError("No se pudo cargar esta imagen.");
        }
        else
        {
            ViewerImage.Source = null;
            ViewerImage.Visibility = Visibility.Collapsed;
            ViewerVideo.Visibility = Visibility.Visible;
            VideoBar.Visibility = Visibility.Visible;

            ViewerVideo.Source = new Uri(entry.FullPath, UriKind.Absolute);
            ViewerVideo.Play();
            SetPlaying(true);
        }
    }

    private static BitmapImage? LoadFullImage(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void ShowViewerError(string message)
    {
        ViewerMessageText.Text = message;
        ViewerMessage.Visibility = Visibility.Visible;
    }

    private void StepMedia(int delta)
    {
        if (_media.Count == 0) return;
        _mediaIndex = (_mediaIndex + delta + _media.Count) % _media.Count;
        ShowMedia();
    }

    private void CloseViewer()
    {
        if (ViewerOverlay.Visibility != Visibility.Visible) return;

        var current = _media.Count > 0 ? _media[_mediaIndex] : null;
        StopVideo();
        ViewerImage.Source = null;
        ViewerOverlay.Visibility = Visibility.Collapsed;

        // Deja seleccionado en la lista el ultimo archivo visto.
        var match = current == null ? null
            : _visible.FirstOrDefault(e => string.Equals(e.FullPath, current.FullPath, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            EntriesList.SelectedItems.Clear();
            EntriesList.SelectedItem = match;
            EntriesList.ScrollIntoView(match);
        }

        EntriesList.Focus();
    }

    private void ViewerClose_Click(object sender, RoutedEventArgs e) => CloseViewer();
    private void ViewerPrev_Click(object sender, RoutedEventArgs e) => StepMedia(-1);
    private void ViewerNext_Click(object sender, RoutedEventArgs e) => StepMedia(1);

    private void Viewer_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        StepMedia(e.Delta > 0 ? -1 : 1);
        e.Handled = true;
    }

    private void OpenExternal_Click(object sender, RoutedEventArgs e)
    {
        if (_media.Count == 0) return;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = _media[_mediaIndex].FullPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo abrir el archivo multimedia", ex);
        }
    }

    // ----- Video -----

    private void StopVideo()
    {
        _videoTimer.Stop();
        ViewerVideo.Stop();
        ViewerVideo.Source = null;
        SeekSlider.Value = 0;
        TimeText.Text = "";
        SetPlaying(false);
    }

    private void SetPlaying(bool playing)
    {
        _videoPlaying = playing;
        PlayPauseButton.Content = playing ? "\uE769" : "\uE768";
    }

    private void TogglePlay()
    {
        if (ViewerVideo.Visibility != Visibility.Visible || ViewerVideo.Source == null) return;

        if (_videoPlaying) ViewerVideo.Pause();
        else ViewerVideo.Play();
        SetPlaying(!_videoPlaying);
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e) => TogglePlay();

    private void Video_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (ViewerVideo.NaturalDuration.HasTimeSpan)
            SeekSlider.Maximum = Math.Max(1, ViewerVideo.NaturalDuration.TimeSpan.TotalSeconds);
        _videoTimer.Start();
    }

    private void Video_MediaEnded(object sender, RoutedEventArgs e)
    {
        ViewerVideo.Position = TimeSpan.Zero;
        ViewerVideo.Pause();
        SetPlaying(false);
    }

    private void Video_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _videoTimer.Stop();
        SetPlaying(false);
        ShowViewerError("No se pudo reproducir este video (formato o codec no compatible).");
    }

    private void UpdateVideoProgress()
    {
        if (_draggingSeek || ViewerVideo.Source == null || !ViewerVideo.NaturalDuration.HasTimeSpan) return;

        var position = ViewerVideo.Position;
        SeekSlider.Value = Math.Min(SeekSlider.Maximum, position.TotalSeconds);
        TimeText.Text = $"{FormatTime(position)} / {FormatTime(ViewerVideo.NaturalDuration.TimeSpan)}";
    }

    private void Seek_Down(object sender, MouseButtonEventArgs e) => _draggingSeek = true;

    private void Seek_Up(object sender, MouseButtonEventArgs e)
    {
        _draggingSeek = false;
        if (ViewerVideo.Source != null) ViewerVideo.Position = TimeSpan.FromSeconds(SeekSlider.Value);
    }

    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
}
