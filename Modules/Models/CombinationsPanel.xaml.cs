using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Launcher.Core.Mvvm;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

public enum CharState { Installed, Missing, Ambiguous, Malformed, NoTag, Unknown }

/// <summary>Un personaje candidato: su nombre, su linea de prompt (editable) y si su modelo esta instalado.</summary>
public class CharacterItem : ViewModelBase
{
    private bool _selected;
    private string _line = "";

    public string Name { get; set; } = "";
    public CharState State { get; set; }

    public bool Selected { get => _selected; set => SetField(ref _selected, value); }
    public string Line { get => _line; set => SetField(ref _line, value); }

    public bool IsProblem => State is CharState.Missing or CharState.Ambiguous or CharState.Malformed;

    public string StateLabel => State switch
    {
        CharState.Installed => "INSTALADO",
        CharState.Missing => "SIN LORA",
        CharState.Ambiguous => "AMBIGUO",
        CharState.Malformed => "ERROR",
        CharState.NoTag => "SIN ETIQUETA",
        _ => "?"
    };

    public int StateLevel => State switch
    {
        CharState.Installed => 1,
        CharState.Missing or CharState.Ambiguous => 2,
        CharState.Malformed => 3,
        _ => 0
    };
}

/// <summary>Una pareja o un trio generado. Las lineas de los personajes se unen con ", " (sin barras verticales).</summary>
public class ComboItem : ViewModelBase
{
    private bool _include;
    private bool _exists;

    public string[] Parts { get; }
    public string Text { get; }

    public ComboItem(string[] parts, bool include)
    {
        Parts = parts;
        Text = string.Join(", ", parts);
        _include = include;
    }

    public string KindLabel => Parts.Length == 2 ? "PAREJA" : "TRIO";
    public bool Include { get => _include; set => SetField(ref _include, value); }
    public bool CanInclude => !_exists;

    /// <summary>Ya hay una linea equivalente (incluso con los personajes en otro orden) en el wildcard de destino.</summary>
    public bool ExistsInDestination
    {
        get => _exists;
        set
        {
            if (!SetField(ref _exists, value)) return;
            OnPropertyChanged(nameof(CanInclude));
            if (value) Include = false;
        }
    }

    private static string Norm(string text) => Regex.Replace(text.Trim(), @"\s+", " ").ToLowerInvariant();

    public static string Key(string line) => Norm(line);

    /// <summary>Todas las ordenaciones posibles de los personajes (2 o 6), para detectar duplicados invertidos.</summary>
    public IEnumerable<string> Permutations()
    {
        if (Parts.Length == 2)
        {
            yield return Norm($"{Parts[0]}, {Parts[1]}");
            yield return Norm($"{Parts[1]}, {Parts[0]}");
            yield break;
        }

        int[][] orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
        foreach (var order in orders) yield return Norm(string.Join(", ", order.Select(i => Parts[i])));
    }

    public bool IsIn(HashSet<string> existing) => Permutations().Any(existing.Contains);
}

public record OriginOption(string Label, string? Folder, WildcardFile? File);

/// <summary>
/// Generador de parejas y trios. Parte de un origen (carpeta de la biblioteca o un wildcard de personajes), genera
/// combinaciones sin duplicados por orden (i&lt;j&lt;k) y las guarda en UN wildcard (existente o nuevo) solo tras la
/// vista previa y la confirmacion. Nunca se escribe sin copia de seguridad y nunca se repiten lineas existentes.
/// </summary>
public partial class CombinationsPanel : UserControl
{
    private const int MaxCombos = 20000;
    private const string DefaultWeight = "0.8";

    private readonly ModelCatalogService _catalog = ModelCatalogService.Instance;
    private readonly ModelsSettings _settings = ModelsSettings.Instance;

    private readonly ObservableCollection<CharacterItem> _chars = new();
    private readonly ICollectionView _charsView;
    private ObservableCollection<ComboItem> _combos = new();
    private HashSet<string> _destSet = new();
    private List<WildcardFile> _files = new();

    private CharacterItem? _current;
    private string _source = "library";
    private string _search = "";
    private int _loadVersion;
    private bool _ready;
    private bool _loaded;
    private bool _updatingLine;
    private bool _rebuildingOrigins;

    /// <summary>Se han escrito wildcards: el explorador de Wildcards debe refrescarse.</summary>
    public event Action? WildcardsChanged;

    public CombinationsPanel()
    {
        InitializeComponent();

        _charsView = CollectionViewSource.GetDefaultView(_chars);
        _charsView.Filter = o => _search.Length == 0 ||
                                 (o is CharacterItem c && c.Name.Contains(_search, StringComparison.OrdinalIgnoreCase));
        CharList.ItemsSource = _charsView;
        ComboList.ItemsSource = _combos;

        _catalog.Changed += (_, _) =>
        {
            if (_loaded && _source == "library") RebuildOrigins();
        };

        IsVisibleChanged += async (_, e) =>
        {
            if (e.NewValue is true && !_loaded)
            {
                _loaded = true;

                try
                {
                    await RefreshFilesAsync();
                    RebuildOrigins();
                }
                catch (Exception ex)
                {
                    LogService.Error("No se pudo iniciar el generador de combinaciones", ex);
                }
            }
        };

        _ready = true;
        UpdateCounts();
        UpdateComboInfo();
        UpdateDestUi();
    }

    private Window? Owner => Window.GetWindow(this);

    private static string Clean(string line) => line.Trim().TrimEnd(',', ' ');

    // ===================== Origen y personajes =====================

    private async Task RefreshFilesAsync()
    {
        var selectedPath = (DestCombo.SelectedItem as WildcardFile)?.FullPath;

        _files = await WildcardService.Instance.ListFilesAsync();
        DestCombo.ItemsSource = _files;
        DestCombo.SelectedItem = _files.FirstOrDefault(f => string.Equals(f.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase));
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RefreshFilesAsync();
            RebuildOrigins();

            // Recarga los personajes del origen elegido (por si cambiaron la biblioteca o el wildcard).
            if (OriginCombo.SelectedItem is OriginOption selected) await LoadCharactersAsync(selected);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo actualizar el generador", ex);
        }
    }

    private void Source_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        _source = (string)((RadioButton)sender).Tag;
        RebuildOrigins();
    }

    private void RebuildOrigins()
    {
        var previous = (OriginCombo.SelectedItem as OriginOption);
        var previousKey = previous?.Folder ?? previous?.File?.FullPath;

        List<OriginOption> options;

        if (_source == "library")
        {
            OriginLabel.Text = "ORIGEN (serie / franquicia = carpeta de la biblioteca)";

            options = _catalog.Models
                .Where(m => m.Type != ModelType.Checkpoint && m.RelativeFolder.Length > 0)
                .GroupBy(m => m.RelativeFolder, StringComparer.OrdinalIgnoreCase)
                .Select(g => new OriginOption($"{g.Key}  ({g.Count()})", g.Key, null))
                .OrderByDescending(o => o.Folder!.StartsWith(DownloadPlanner.CharactersCategory + "\\", StringComparison.OrdinalIgnoreCase))
                .ThenBy(o => o.Folder, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        else
        {
            OriginLabel.Text = "WILDCARD DE PERSONAJES (cada linea = un personaje)";
            options = _files.Select(f => new OriginOption($"{f.Name}  ({f.LineCount})", null, f)).ToList();
        }

        _rebuildingOrigins = true;
        OriginCombo.ItemsSource = options;
        OriginCombo.SelectedItem = options.FirstOrDefault(o => (o.Folder ?? o.File?.FullPath) == previousKey);
        _rebuildingOrigins = false;

        if (OriginCombo.SelectedItem == null) _ = LoadCharactersAsync(null);
    }

    private async void OriginCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rebuildingOrigins) return;

        try
        {
            await LoadCharactersAsync(OriginCombo.SelectedItem as OriginOption);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudieron cargar los personajes", ex);
        }
    }

    private async Task LoadCharactersAsync(OriginOption? origin)
    {
        var version = ++_loadVersion;

        _chars.Clear();
        _combos = new ObservableCollection<ComboItem>();
        ComboList.ItemsSource = _combos;
        _current = null;
        SetLineBox(null);

        if (origin == null)
        {
            EmptyChars.Text = _source == "library"
                ? "> elige un origen (o escanea la biblioteca)_"
                : "> elige un wildcard de personajes_";
            EmptyChars.Visibility = Visibility.Visible;
            UpdateCounts();
            UpdateComboInfo();
            return;
        }

        var items = new List<CharacterItem>();

        if (origin.File == null)
        {
            // Solo los modelos de ESA carpeta: nunca se mezclan origenes.
            var models = _catalog.Models
                .Where(m => m.Type != ModelType.Checkpoint &&
                            string.Equals(m.RelativeFolder, origin.Folder, StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var lines = await LineInfoService.BuildLinesAsync(models.Select(m => m.Name), DefaultWeight);
            var duplicated = _catalog.Models.Where(m => m.Type != ModelType.Checkpoint)
                .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            items = models.Select(m => new CharacterItem
            {
                Name = m.Name,
                Line = lines.GetValueOrDefault(m.Name) ?? $"<lora:{m.Name}:{DefaultWeight}>",
                State = duplicated.Contains(m.Name) ? CharState.Ambiguous : CharState.Installed
            }).ToList();
        }
        else
        {
            var raw = await Task.Run(() => WildcardService.ReadLines(origin.File.FullPath));
            var entries = raw.Where(WildcardService.IsEntryLine).Select(l => l.Trim()).ToList();
            var checks = WildcardAnalyzer.Analyze(entries, new ModelLookup(_catalog.Models));

            var tag = new Regex(@"<(?:lora|lyco):(?<n>[^:>]+)", RegexOptions.IgnoreCase);

            for (var i = 0; i < entries.Count; i++)
            {
                var match = tag.Match(entries[i]);
                var name = match.Success
                    ? match.Groups["n"].Value.Trim()
                    : entries[i].Split(',')[0].Trim();

                items.Add(new CharacterItem
                {
                    Name = name.Length > 60 ? name[..60] : name,
                    Line = entries[i],
                    State = checks[i].Status switch
                    {
                        LineStatus.Ok => CharState.Installed,
                        LineStatus.Missing => CharState.Missing,
                        LineStatus.Ambiguous => CharState.Ambiguous,
                        LineStatus.Malformed => CharState.Malformed,
                        LineStatus.Unknown => CharState.Unknown,
                        _ => CharState.NoTag
                    }
                });
            }
        }

        if (version != _loadVersion) return; // el usuario ya eligio otro origen mientras se leia

        foreach (var item in items)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CharacterItem.Selected)) UpdateCounts();
            };
            _chars.Add(item);
        }

        EmptyChars.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyChars.Text = "> este origen no tiene personajes_";

        UpdateCounts();
        UpdateComboInfo();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text.Trim();
        if (_ready) _charsView.Refresh();
    }

    private void MarkAllChars_Click(object sender, RoutedEventArgs e)
    {
        foreach (var c in _charsView.Cast<CharacterItem>().ToList()) c.Selected = true;
    }

    private void MarkNoChars_Click(object sender, RoutedEventArgs e)
    {
        foreach (var c in _charsView.Cast<CharacterItem>().ToList()) c.Selected = false;
    }

    private void CharList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _current = CharList.SelectedItem as CharacterItem;
        SetLineBox(_current);
    }

    private void SetLineBox(CharacterItem? item)
    {
        _updatingLine = true;
        LineBox.Text = item?.Line ?? "";
        LineBox.IsEnabled = item != null;
        _updatingLine = false;
    }

    private void LineBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingLine || _current == null) return;
        _current.Line = LineBox.Text; // las combinaciones ya generadas no cambian: hay que volver a generar
    }

    // ===================== Conteo y opciones =====================

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready) UpdateCounts();
    }

    private void UpdateCounts()
    {
        if (!_ready) return;

        var selected = _chars.Where(c => c.Selected).ToList();
        long n = selected.Count;

        var pairs = n * (n - 1) / 2;
        var trios = n * (n - 1) * (n - 2) / 6;
        var total = (PairsCheck.IsChecked == true ? pairs : 0) + (TriosCheck.IsChecked == true ? trios : 0);

        CountsText.Text = $"{n} personajes   ->   {pairs} parejas  +  {trios} trios   =   {total} combinaciones";
        SelectedInfo.Text = $"{n} de {_chars.Count} personajes marcados";

        var problems = selected.Count(c => c.IsProblem);
        var warn = problems > 0
            ? $"{problems} personaje(s) sin LoRA instalado o ambiguos: puedes usarlos, pero sus lineas no funcionaran bien al generar. "
            : "";
        if (total > MaxCombos) warn += $"Demasiadas combinaciones (maximo {MaxCombos}): reduce la seleccion.";
        WarnText.Text = warn.Trim();

        var ok = n >= 2 && total > 0 && total <= MaxCombos;
        GenerateButton.IsEnabled = ok;
        GenerateButton.Opacity = ok ? 1.0 : 0.45;
    }

    // ===================== Generar =====================

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        var chars = _chars.Where(c => c.Selected && !string.IsNullOrWhiteSpace(c.Line)).ToList();
        var parts = chars.Select(c => Clean(c.Line)).ToList();
        var n = parts.Count;

        var wantPairs = PairsCheck.IsChecked == true;
        var wantTrios = TriosCheck.IsChecked == true;

        long total = (wantPairs ? (long)n * (n - 1) / 2 : 0) + (wantTrios ? (long)n * (n - 1) * (n - 2) / 6 : 0);
        if (n < 2 || total == 0 || total > MaxCombos) return;

        var include = AutoRadio.IsChecked == true; // automatico: todas marcadas; manual: se marcan a mano
        var list = new List<ComboItem>((int)total);

        if (wantPairs)
        {
            for (var i = 0; i < n; i++)
                for (var j = i + 1; j < n; j++)
                    list.Add(new ComboItem(new[] { parts[i], parts[j] }, include));
        }

        if (wantTrios)
        {
            for (var i = 0; i < n; i++)
                for (var j = i + 1; j < n; j++)
                    for (var k = j + 1; k < n; k++)
                        list.Add(new ComboItem(new[] { parts[i], parts[j], parts[k] }, include));
        }

        SetCombos(list);
        _ = RefreshDestinationAsync();
    }

    private void SetCombos(List<ComboItem> list)
    {
        foreach (var combo in list)
        {
            combo.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(ComboItem.Include) or nameof(ComboItem.ExistsInDestination)) UpdateComboInfo();
            };
        }

        _combos = new ObservableCollection<ComboItem>(list);
        ComboList.ItemsSource = _combos;
        UpdateComboInfo();
    }

    private void UpdateComboInfo()
    {
        if (!_ready) return;

        var included = _combos.Count(c => c.Include);
        var exists = _combos.Count(c => c.ExistsInDestination);

        EmptyCombos.Visibility = _combos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ComboInfo.Text = _combos.Count == 0
            ? ""
            : $"{_combos.Count} combinaciones  \u00b7  {included} marcadas  \u00b7  {exists} ya existen en el destino";

        var can = included > 0;
        SaveButton.IsEnabled = can;
        SaveButton.Opacity = can ? 1.0 : 0.45;
    }

    private void MarkAllCombos_Click(object sender, RoutedEventArgs e)
    {
        foreach (var c in _combos) if (c.CanInclude) c.Include = true;
    }

    private void MarkNoCombos_Click(object sender, RoutedEventArgs e)
    {
        foreach (var c in _combos) c.Include = false;
    }

    private void RemoveCombo_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ComboItem combo)
        {
            _combos.Remove(combo);
            UpdateComboInfo();
        }
    }

    // ===================== Destino =====================

    private bool DestIsNew => DestNew.IsChecked == true;

    private void Dest_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        UpdateDestUi();
        _ = RefreshDestinationAsync();
    }

    private void UpdateDestUi()
    {
        DestCombo.Visibility = DestIsNew ? Visibility.Collapsed : Visibility.Visible;
        NewNameBox.Visibility = DestIsNew ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void DestCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) await RefreshDestinationAsync();
    }

    private static HashSet<string> BuildSet(IEnumerable<string> lines) =>
        lines.Where(WildcardService.IsEntryLine).Select(ComboItem.Key).ToHashSet();

    /// <summary>Lee el wildcard de destino y marca las combinaciones que ya contiene (en cualquier orden).</summary>
    private async Task RefreshDestinationAsync()
    {
        _destSet = new HashSet<string>();

        if (!DestIsNew && DestCombo.SelectedItem is WildcardFile file)
        {
            try
            {
                var lines = await Task.Run(() => WildcardService.ReadLines(file.FullPath));
                _destSet = BuildSet(lines);
            }
            catch (Exception ex)
            {
                LogService.Error("No se pudo leer el wildcard de destino", ex);
            }
        }

        foreach (var combo in _combos) combo.ExistsInDestination = combo.IsIn(_destSet);
        UpdateComboInfo();
    }

    // ===================== Guardar =====================

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var chosen = _combos.Where(c => c.Include && !c.ExistsInDestination).ToList();
        if (chosen.Count == 0) return;

        SaveButton.IsEnabled = false;

        try
        {
            var saved = DestIsNew ? await SaveToNewAsync(chosen) : await SaveToExistingAsync(chosen);
            if (saved != null)
            {
                _destSet.Clear();
                WildcardService.Instance.InvalidateReferenceIndex();
                WildcardsChanged?.Invoke();

                await RefreshFilesAsync();

                // Se queda seleccionado el wildcard guardado: las combinaciones recien escritas pasan a "YA EXISTE".
                DestExisting.IsChecked = true;
                DestCombo.SelectedItem = _files.FirstOrDefault(f => string.Equals(f.FullPath, saved, StringComparison.OrdinalIgnoreCase));
                await RefreshDestinationAsync();
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudieron guardar las combinaciones", ex);
            MessageBox.Show(Owner, $"No se han podido guardar las combinaciones:\n{ex.Message}", "Guardar combinaciones",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            UpdateComboInfo();
        }
    }

    /// <summary>Devuelve la ruta guardada, o null si se cancelo o no habia nada que escribir.</summary>
    private async Task<string?> SaveToExistingAsync(List<ComboItem> chosen)
    {
        if (DestCombo.SelectedItem is not WildcardFile file)
        {
            MessageBox.Show(Owner, "Elige el wildcard de destino.", "Guardar combinaciones",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        var doc = await Task.Run(() => WildcardDocument.Load(file.FullPath));

        // Se vuelve a comprobar contra el contenido actual del archivo.
        var existing = BuildSet(doc.Lines);
        var texts = chosen.Where(c => !c.IsIn(existing)).Select(c => c.Text).ToList();

        if (texts.Count == 0)
        {
            StatusText.Text = "todas las combinaciones marcadas ya existen en ese wildcard";
            return null;
        }

        var changes = texts.Select((t, i) => new ChangeLine("+", $"L{doc.Lines.Count + i + 1}   {t}")).ToList();
        var dialog = new ConfirmChangesWindow(file.Name, changes) { Owner = Owner };
        if (dialog.ShowDialog() != true) return null;

        if (doc.ChangedOnDisk())
        {
            var answer = MessageBox.Show(Owner,
                "El archivo ha cambiado en disco mientras revisabas las combinaciones. Se anadiran al final igualmente " +
                "(quedara una copia del estado actual). Continuar?",
                "Wildcard modificado", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return null;

            doc = await Task.Run(() => WildcardDocument.Load(file.FullPath));
        }

        await Task.Run(() =>
        {
            WildcardBackup.Create(doc.Path, file.Name); // si falla, no se escribe
            doc.Save(doc.Lines.Concat(texts).ToList());
        });

        LogService.Info($"Combinaciones guardadas en {file.Name}: {texts.Count} lineas");
        StatusText.Text = $"{texts.Count} lineas anadidas a __{file.Name}__";
        return file.FullPath;
    }

    private async Task<string?> SaveToNewAsync(List<ComboItem> chosen)
    {
        var name = NewNameBox.Text.Trim().Replace('\\', '/').Trim('/');
        var invalid = Path.GetInvalidFileNameChars();

        if (name.Length == 0 || name.Split('/').Any(s => s.Length == 0 || s == "." || s == ".." || s.IndexOfAny(invalid) >= 0))
        {
            MessageBox.Show(Owner, "Escribe un nombre valido para el wildcard nuevo (puedes usar carpetas con '/').",
                "Nuevo wildcard", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var root = _settings.WildcardFoldersEffective().FirstOrDefault(Directory.Exists);
        if (root == null)
        {
            MessageBox.Show(Owner, "No hay una carpeta de wildcards valida. Configurala en Configuracion.",
                "Nuevo wildcard", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var fullRoot = Path.GetFullPath(root).TrimEnd('\\');
        var path = Path.GetFullPath(Path.Combine(fullRoot, name.Replace('/', '\\') + ".txt"));

        if (!path.StartsWith(fullRoot + "\\", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(Owner, "El nombre apunta fuera de la carpeta de wildcards.", "Nuevo wildcard",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        if (File.Exists(path))
        {
            MessageBox.Show(Owner, $"Ya existe el wildcard \"{name}\". Elige \"Wildcard existente\" para anadir lineas sin pisarlo.",
                "Nuevo wildcard", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var texts = chosen.Select(c => c.Text).ToList();
        var changes = texts.Select((t, i) => new ChangeLine("+", $"L{i + 1}   {t}")).ToList();

        var dialog = new ConfirmChangesWindow(name, changes, $"nuevo wildcard __{name}__") { Owner = Owner };
        if (dialog.ShowDialog() != true) return null;

        await Task.Run(() => WildcardDocument.CreateNew(path, texts));

        LogService.Info($"Wildcard nuevo creado con combinaciones: {path} ({texts.Count} lineas)");
        StatusText.Text = $"creado __{name}__ con {texts.Count} lineas";
        return path;
    }
}
