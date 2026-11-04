using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

/// <summary>
/// Explorador y editor de wildcards. Reglas: nada se escribe sin vista previa y confirmacion; antes de
/// escribir siempre se crea una copia; las lineas que no se tocan se guardan exactamente como estaban;
/// "Comprobar referencias" solo lee. La sincronizacion con la biblioteca es manual (boton).
/// </summary>
public partial class WildcardsPanel : UserControl
{
    private const string DefaultWeight = "0.8";

    private readonly WildcardService _service = WildcardService.Instance;
    private readonly ModelCatalogService _catalog = ModelCatalogService.Instance;

    private readonly ObservableCollection<WildcardFile> _files = new();
    private readonly ICollectionView _filesView;

    private ObservableCollection<WildcardLineVM> _lines = new();
    private ICollectionView? _linesView;

    private WildcardFile? _file;
    private WildcardDocument? _doc;
    private WildcardLineVM? _sel;
    private ParsedModelLine? _parsed;

    private string _search = "";
    private string _fileFilter = "all";
    private bool _onlyAttention;

    private bool _ready;
    private bool _changingFile;
    private bool _updatingEditor;
    private bool _syncingLineSelection;

    public WildcardsPanel()
    {
        InitializeComponent();

        _filesView = CollectionViewSource.GetDefaultView(_files);
        _filesView.Filter = FileFilter;
        FilesList.ItemsSource = _filesView;

        SetEnabled(SaveButton, false);
        SetEnabled(DiscardButton, false);

        _ready = true;
    }

    private Window? Owner => Window.GetWindow(this);

    private static void SetEnabled(Button button, bool enabled)
    {
        button.IsEnabled = enabled;
        button.Opacity = enabled ? 1.0 : 0.45;
    }

    // ===================== Explorador =====================

    public async Task RefreshAsync()
    {
        RefreshButton.IsEnabled = false;

        try
        {
            var files = await _service.ListFilesAsync();

            _changingFile = true;
            _files.Clear();
            foreach (var file in files) _files.Add(file);

            // Si habia un archivo abierto se conserva el editor (y sus cambios) y solo se re-enlaza el elemento.
            if (_file != null)
            {
                var again = files.FirstOrDefault(f =>
                    string.Equals(f.FullPath, _file.FullPath, StringComparison.OrdinalIgnoreCase));
                if (again != null)
                {
                    _file = again;
                    FilesList.SelectedItem = again;
                }
            }
            _changingFile = false;

            _filesView.Refresh();
            UpdateExplorerInfo();
        }
        catch (Exception ex)
        {
            _changingFile = false;
            LogService.Error("No se pudieron listar los wildcards", ex);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private bool FileFilter(object item)
    {
        if (item is not WildcardFile file) return false;

        if (_search.Length > 0 && !file.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)) return false;

        return _fileFilter switch
        {
            "models" => file.ModelRefCount > 0,
            "problems" => file.HasProblems,
            _ => true
        };
    }

    private void UpdateExplorerInfo()
    {
        var shown = _filesView.Cast<object>().Count();
        var refs = _files.Sum(f => f.ModelRefCount);
        var problems = _files.Count(f => f.HasProblems);

        var text = $"{shown} de {_files.Count} archivos  \u00b7  {refs} referencias a modelos";
        if (_files.Any(f => f.WasChecked)) text += $"  \u00b7  {problems} con problemas";

        ExplorerInfo.Text = _files.Count == 0
            ? "sin wildcards: revisa la carpeta en Configuracion"
            : text;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text.Trim();
        if (!_ready) return;

        _filesView.Refresh();
        UpdateExplorerInfo();
    }

    private void FileFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        _fileFilter = (string)((RadioButton)sender).Tag;
        _filesView.Refresh();
        UpdateExplorerInfo();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmLeaveAsync()) return;
        await RefreshAsync();
    }

    /// <summary>Comprueba todos los archivos contra la biblioteca. Solo lee: no modifica nada.</summary>
    private async void CheckAll_Click(object sender, RoutedEventArgs e)
    {
        var models = _catalog.Models;
        if (models.Count == 0)
        {
            MessageBox.Show(Owner,
                "Aun no hay inventario de modelos. Configura la carpeta de A1111 y reescanea la Biblioteca.",
                "Comprobar wildcards", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        CheckAllButton.IsEnabled = false;
        CheckAllButton.Content = "Comprobando...";

        try
        {
            var lookup = new ModelLookup(models);
            var files = _files.ToList();

            var results = await Task.Run(() => files.Select(file =>
            {
                try
                {
                    var checks = WildcardAnalyzer.Analyze(WildcardService.ReadLines(file.FullPath), lookup);
                    return (File: file, Summary: (CheckSummary?)WildcardAnalyzer.Summarize(checks));
                }
                catch
                {
                    return (File: file, Summary: (CheckSummary?)null);
                }
            }).ToList());

            foreach (var (file, summary) in results)
            {
                if (summary is { } s) file.ApplyCheck(s);
            }

            _filesView.Refresh();
            UpdateExplorerInfo();
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la comprobacion de wildcards", ex);
        }
        finally
        {
            CheckAllButton.IsEnabled = true;
            CheckAllButton.Content = "Comprobar todos";
        }
    }

    private async void FilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_changingFile) return;

        if (FilesList.SelectedItem is not WildcardFile target || ReferenceEquals(target, _file)) return;

        if (!await ConfirmLeaveAsync())
        {
            _changingFile = true;
            FilesList.SelectedItem = _file;
            _changingFile = false;
            return;
        }

        await LoadFileAsync(target);
    }

    // ===================== Cargar / construir lineas =====================

    private async Task LoadFileAsync(WildcardFile file)
    {
        try
        {
            var doc = await Task.Run(() => WildcardDocument.Load(file.FullPath));

            _file = file;
            _doc = doc;
            BuildLines(doc.Lines.Select((text, i) => new WildcardLineVM(text, i, text)));

            EditorBody.Visibility = Visibility.Visible;
            EditorEmpty.Visibility = Visibility.Collapsed;
            UpdateEditorInfo(null);
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo abrir el wildcard {file.Name}", ex);
            MessageBox.Show(Owner, $"No se ha podido abrir \"{file.Name}\":\n{ex.Message}", "Wildcards",
                MessageBoxButton.OK, MessageBoxImage.Warning);

            _changingFile = true;
            FilesList.SelectedItem = _file;
            _changingFile = false;
        }
    }

    private void BuildLines(IEnumerable<WildcardLineVM> lines)
    {
        _lines = new ObservableCollection<WildcardLineVM>(lines);
        _linesView = CollectionViewSource.GetDefaultView(_lines);
        _linesView.Filter = LineFilter;
        LinesList.ItemsSource = _linesView;

        Renumber();
        SetSelectedLine(null);
        RefreshDirtyUi();
    }

    private bool LineFilter(object item) =>
        !_onlyAttention || (item is WildcardLineVM line && (line.IsModified || line.NeedsAttention));

    private void LineFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        _onlyAttention = (string)((RadioButton)sender).Tag == "attention";
        _linesView?.Refresh();
    }

    private void Renumber()
    {
        for (var i = 0; i < _lines.Count; i++) _lines[i].Number = i + 1;
    }

    private bool IsDirty()
    {
        if (_doc == null) return false;

        var existing = 0;
        foreach (var line in _lines)
        {
            if (line.IsNew || line.IsModified) return true;
            existing++;
        }

        return existing != _doc.Lines.Count; // se ha eliminado alguna
    }

    private void RefreshDirtyUi()
    {
        var dirty = IsDirty();
        EditorTitle.Text = _file == null ? "" : $"__{_file.Name}__" + (dirty ? "   * sin guardar" : "");
        SetEnabled(SaveButton, dirty);
        SetEnabled(DiscardButton, dirty);
    }

    private void UpdateEditorInfo(CheckSummary? summary)
    {
        var entries = _lines.Count(l => WildcardService.IsEntryLine(l.Text));
        var text = $"{_lines.Count} lineas  \u00b7  {entries} entradas";

        if (summary is { } s)
        {
            var parts = new List<string>();
            if (s.Missing > 0) parts.Add($"{s.Missing} ausentes");
            if (s.Ambiguous > 0) parts.Add($"{s.Ambiguous} ambiguos");
            if (s.Malformed > 0) parts.Add($"{s.Malformed} errores");
            if (s.Duplicates > 0) parts.Add($"{s.Duplicates} duplicadas");
            text += "  \u00b7  " + (parts.Count == 0 ? "sin problemas" : string.Join("  \u00b7  ", parts));
        }

        EditorInfo.Text = text;
    }

    // ===================== Edicion de la linea seleccionada =====================

    private void LinesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingLineSelection) return;
        SetSelectedLine(LinesList.SelectedItem as WildcardLineVM);
    }

    private void SelectLine(WildcardLineVM? line)
    {
        _syncingLineSelection = true;
        LinesList.SelectedItem = line;
        if (line != null) LinesList.ScrollIntoView(line);
        _syncingLineSelection = false;

        SetSelectedLine(line);
    }

    private void SetSelectedLine(WildcardLineVM? line)
    {
        _sel = line;

        LineEditorBody.Visibility = line == null ? Visibility.Collapsed : Visibility.Visible;
        LineEditorEmpty.Visibility = line == null ? Visibility.Visible : Visibility.Collapsed;

        if (line == null)
        {
            _parsed = null;
            return;
        }

        _updatingEditor = true;
        LineTitle.Text = $"LINEA {line.Number}";
        RawBox.Text = line.Text;
        LoadStructured(line.Text);
        _updatingEditor = false;
    }

    private void LoadStructured(string text)
    {
        _parsed = ModelLine.Parse(text);
        StructuredPanel.Visibility = _parsed == null ? Visibility.Collapsed : Visibility.Visible;

        if (_parsed != null)
        {
            ModelBox.Text = _parsed.Name;
            WeightBox.Text = _parsed.Weight;
            RestBox.Text = _parsed.Rest;
        }

        UpdateModelState();
    }

    private void RawBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingEditor || _sel == null) return;

        _sel.Text = RawBox.Text;

        _updatingEditor = true;
        LoadStructured(_sel.Text);
        _updatingEditor = false;

        RefreshDirtyUi();
    }

    private void Structured_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingEditor || _sel == null || _parsed == null) return;

        _parsed.Name = ModelBox.Text.Trim();
        _parsed.Weight = WeightBox.Text.Trim();
        _parsed.Rest = RestBox.Text.TrimStart();

        var text = ModelLine.Build(_parsed);

        _updatingEditor = true;
        RawBox.Text = text;
        _updatingEditor = false;

        _sel.Text = text;
        UpdateModelState();
        RefreshDirtyUi();
    }

    private static double? ParseWeight(string text) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private void StepWeight(double delta)
    {
        if (_sel == null || _parsed == null) return;

        var current = ParseWeight(WeightBox.Text) ?? ParseWeight(DefaultWeight)!.Value;
        var next = Math.Round(Math.Clamp(current + delta, -10, 10), 2);
        WeightBox.Text = next.ToString("0.##", CultureInfo.InvariantCulture); // dispara Structured_TextChanged
    }

    private void WeightUp_Click(object sender, RoutedEventArgs e) => StepWeight(0.05);
    private void WeightDown_Click(object sender, RoutedEventArgs e) => StepWeight(-0.05);

    private void UpdateModelState()
    {
        if (_parsed == null)
        {
            ModelStateText.Text = "";
            return;
        }

        var name = ModelBox.Text.Trim();
        var sb = new StringBuilder();

        if (name.Length == 0)
        {
            sb.Append("sin nombre de modelo");
        }
        else if (_catalog.Models.Count == 0)
        {
            sb.Append("sin inventario de modelos");
        }
        else
        {
            var matches = _catalog.FindAllByName(name).Where(m => m.Type != ModelType.Checkpoint).ToList();

            if (matches.Count == 0) sb.Append("ausente en la biblioteca");
            else if (matches.Count == 1) sb.Append($"instalado: {matches[0].TypeLabel} \u00b7 {matches[0].DisplayFolder}");
            else sb.Append($"ambiguo: {matches.Count} archivos con este nombre");
        }

        var weight = WeightBox.Text.Trim();
        if (weight.Length > 0 && ParseWeight(weight) == null) sb.Append("  \u00b7  peso no valido");

        ModelStateText.Text = sb.ToString();
    }

    // ===================== Anadir / eliminar lineas =====================

    private void InsertLine(string text)
    {
        var line = new WildcardLineVM(text, null, null);
        var index = _sel == null ? _lines.Count : _lines.IndexOf(_sel) + 1;

        _lines.Insert(index, line);
        Renumber();
        SelectLine(line);
        RefreshDirtyUi();

        RawBox.Focus();
        RawBox.CaretIndex = RawBox.Text.Length;
    }

    private void AddLine_Click(object sender, RoutedEventArgs e) => InsertLine("");

    private void DeleteLine_Click(object sender, RoutedEventArgs e)
    {
        if (_sel == null) return;

        var index = _lines.IndexOf(_sel);
        _lines.Remove(_sel);
        Renumber();

        SelectLine(_lines.Count == 0 ? null : _lines[Math.Min(index, _lines.Count - 1)]);
        RefreshDirtyUi();
    }

    // ===================== Anadir modelo de la biblioteca =====================

    private void AddModel_Click(object sender, RoutedEventArgs e)
    {
        if (_catalog.Models.Count == 0)
        {
            MessageBox.Show(Owner, "Aun no hay inventario de modelos. Reescanea la Biblioteca primero.",
                "Anadir modelo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ModelPickBox.Text = "";
        FillPickList();
        PickPopup.IsOpen = true;
        Dispatcher.BeginInvoke(() => ModelPickBox.Focus(), DispatcherPriority.Input);
    }

    private void FillPickList()
    {
        var filter = ModelPickBox.Text.Trim();

        ModelPickList.ItemsSource = _catalog.Models
            .Where(m => m.Type != ModelType.Checkpoint &&
                        (filter.Length == 0 || m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(80)
            .ToList();
    }

    private void ModelPickBox_TextChanged(object sender, TextChangedEventArgs e) => FillPickList();

    private void ModelPickBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && ModelPickList.Items.Count > 0)
        {
            ModelPickList.SelectedIndex = 0;
            (ModelPickList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && ModelPickList.Items.Count > 0)
        {
            PickModel(ModelPickList.Items[0] as ModelEntry);
            e.Handled = true;
        }
    }

    private void ModelPickList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PickModel(ModelPickList.SelectedItem as ModelEntry);
            e.Handled = true;
        }
    }

    private void ModelPickList_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        PickModel(ModelPickList.SelectedItem as ModelEntry);

    private async void PickModel(ModelEntry? model)
    {
        if (model == null) return;

        PickPopup.IsOpen = false;

        // Prioridad: linea ya existente en los wildcards > datos de Civitai guardados al descargar > solo la etiqueta.
        InsertLine(await LineInfoService.BuildLineAsync(model.Name, DefaultWeight));
    }

    // ===================== Comprobar referencias (manual, solo lectura) =====================

    private void CheckRefs_Click(object sender, RoutedEventArgs e)
    {
        if (_file == null) return;

        if (_catalog.Models.Count == 0)
        {
            MessageBox.Show(Owner,
                "Aun no hay inventario de modelos. Configura la carpeta de A1111 y reescanea la Biblioteca.",
                "Comprobar referencias", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var checks = WildcardAnalyzer.Analyze(_lines.Select(l => l.Text).ToList(), new ModelLookup(_catalog.Models));
        for (var i = 0; i < _lines.Count; i++) _lines[i].Check = checks[i];

        UpdateEditorInfo(WildcardAnalyzer.Summarize(checks));
        _linesView?.Refresh();
        UpdateModelState();
    }

    // ===================== Copias de seguridad =====================

    private void Backups_Click(object sender, RoutedEventArgs e)
    {
        if (_file == null) return;

        var backups = WildcardBackup.List(_file.Name);
        var menu = new ContextMenu();

        if (backups.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "(todavia no hay copias)", IsEnabled = false });
        }

        foreach (var backup in backups.Take(15))
        {
            var item = new MenuItem { Header = $"{backup.Created:dd/MM/yyyy HH:mm:ss}   ({ModelEntry.FormatSize(backup.Size)})" };
            var captured = backup;
            item.Click += async (_, _) => await LoadBackupAsync(captured);
            menu.Items.Add(item);
        }

        menu.PlacementTarget = (Button)sender;
        menu.IsOpen = true;
    }

    /// <summary>Carga una copia en el editor como cambios pendientes: no se escribe nada hasta guardar
    /// (con su vista previa y su propia copia del estado actual).</summary>
    private async Task LoadBackupAsync(BackupInfo backup)
    {
        if (_doc == null) return;

        if (IsDirty())
        {
            var answer = MessageBox.Show(Owner,
                "Cargar la copia reemplazara los cambios sin guardar del editor. Continuar?",
                "Cargar copia", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
        }

        string[] lines;
        try
        {
            lines = await Task.Run(() => WildcardService.ReadLines(backup.Path));
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer la copia", ex);
            MessageBox.Show(Owner, "No se ha podido leer la copia.", "Cargar copia",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var original = _doc.Lines;
        var vms = new List<WildcardLineVM>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            int? index = i < original.Count ? i : null;
            vms.Add(new WildcardLineVM(lines[i], index, index.HasValue ? original[i] : null));
        }

        BuildLines(vms);
        UpdateEditorInfo(null);
        EditorInfo.Text += "  \u00b7  copia cargada: revisa los cambios y pulsa Guardar";
    }

    // ===================== Descartar / guardar =====================

    private async void Discard_Click(object sender, RoutedEventArgs e)
    {
        if (_file == null || !IsDirty()) return;

        var answer = MessageBox.Show(Owner, "Se perderan los cambios sin guardar de este archivo. Continuar?",
            "Descartar cambios", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        await LoadFileAsync(_file);
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAsync();

    /// <summary>Si hay cambios sin guardar pregunta que hacer. Devuelve false si el usuario cancela.</summary>
    private async Task<bool> ConfirmLeaveAsync()
    {
        if (!IsDirty() || _file == null) return true;

        var answer = MessageBox.Show(Owner,
            $"\"{_file.Name}\" tiene cambios sin guardar.\n\nSi = guardar   No = descartar   Cancelar = seguir editando",
            "Cambios sin guardar", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);

        return answer switch
        {
            MessageBoxResult.Yes => await SaveAsync(),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    private List<ChangeLine> BuildChanges()
    {
        var changes = new List<ChangeLine>();
        if (_doc == null) return changes;

        var original = _doc.Lines;
        var next = 0;

        static string Show(string text) => text.Length == 0 ? "(linea vacia)" : text;

        foreach (var line in _lines)
        {
            if (line.OriginalIndex is { } k)
            {
                // Las originales que se saltan entre una y otra se han eliminado.
                for (; next < k; next++) changes.Add(new ChangeLine("-", $"L{next + 1}   {Show(original[next])}"));

                if (line.IsModified)
                {
                    changes.Add(new ChangeLine("-", $"L{k + 1}   {Show(original[k])}"));
                    changes.Add(new ChangeLine("+", $"L{line.Number}   {Show(line.Text)}"));
                }

                next = k + 1;
            }
            else
            {
                changes.Add(new ChangeLine("+", $"L{line.Number}   {Show(line.Text)}"));
            }
        }

        for (; next < original.Count; next++) changes.Add(new ChangeLine("-", $"L{next + 1}   {Show(original[next])}"));

        return changes;
    }

    private async Task<bool> SaveAsync()
    {
        if (_doc == null || _file == null) return true;

        var changes = BuildChanges();
        if (changes.Count == 0) return true;

        if (_doc.ChangedOnDisk())
        {
            var answer = MessageBox.Show(Owner,
                "El archivo ha cambiado en disco desde que lo abriste (A1111 u otro programa pudo modificarlo).\n\n" +
                "Si guardas ahora, esos cambios se sobrescribiran (quedara una copia del estado actual). Continuar?",
                "Wildcard modificado fuera del Launcher", MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return false;
        }

        var dialog = new ConfirmChangesWindow(_file.Name, changes) { Owner = Owner };
        if (dialog.ShowDialog() != true) return false;

        var newLines = _lines.Select(l => l.Text).ToList();
        var doc = _doc;
        var file = _file;
        var keepLine = _sel?.Number;

        try
        {
            // Copia primero; si la copia falla no se llega a escribir nada.
            var backupPath = await Task.Run(() =>
            {
                var backup = WildcardBackup.Create(doc.Path, file.Name);
                doc.Save(newLines);
                return backup;
            });

            LogService.Info($"Wildcard guardado: {file.Name} ({changes.Count} cambios). Copia: {backupPath}");
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo guardar el wildcard {file.Name}", ex);
            MessageBox.Show(Owner,
                $"No se ha podido guardar \"{file.Name}\":\n{ex.Message}\n\nLos cambios siguen en el editor.",
                "Guardar wildcard", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        _service.InvalidateReferenceIndex();

        var (entries, refs) = WildcardService.ComputeStats(newLines);
        file.LineCount = entries;
        file.ModelRefCount = refs;
        file.Modified = DateTime.Now;
        file.ClearCheck();

        try
        {
            _doc = await Task.Run(() => WildcardDocument.Load(file.FullPath));
            BuildLines(_doc.Lines.Select((text, i) => new WildcardLineVM(text, i, text)));
            UpdateEditorInfo(null);

            if (keepLine is { } n && n >= 1 && n <= _lines.Count) SelectLine(_lines[n - 1]);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo recargar el wildcard tras guardar", ex);
        }

        _filesView.Refresh();
        UpdateExplorerInfo();
        return true;
    }
}
