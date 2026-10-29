using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Launcher.Core.Services;
using Launcher.Modules.ImageGen.Autocomplete;
using Microsoft.Win32;

namespace Launcher.Modules.ImageGen.Extensions;

/// <summary>Archivo CSV de tags y de donde viene (para mostrarlo en el desplegable).</summary>
public sealed record TagFile(string Path, string Source)
{
    public override string ToString() => $"{System.IO.Path.GetFileName(Path)}  ·  {Source}";
}

/// <summary>
/// Base de tags en memoria, cargada de un CSV con el formato de TagComplete:
/// nombre,categoria,recuento,"alias1,alias2" (sin cabecera, ordenado por popularidad).
/// </summary>
public sealed class TagDatabase
{
    public sealed record Entry(string Name, string Lower, int Category, long Count, string[] Aliases);

    private Entry[] _entries = Array.Empty<Entry>();
    private string? _pending;

    public bool IsLoaded => _entries.Length > 0;
    public bool IsLoading { get; private set; }
    public bool IsE621 { get; private set; }
    public int Count => _entries.Length;
    public string? LoadedPath { get; private set; }

    /// <summary>Se dispara (en el hilo de la UI) al empezar y al terminar de cargar.</summary>
    public event Action? StateChanged;

    public async Task LoadAsync(string path)
    {
        // Si ya se esta cargando otro archivo, se recuerda el ultimo pedido y se carga despues.
        _pending = path;
        if (IsLoading) return;

        IsLoading = true;
        StateChanged?.Invoke();

        try
        {
            while (_pending != null)
            {
                var target = _pending;
                _pending = null;

                if (IsLoaded && string.Equals(LoadedPath, target, StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    var entries = await Task.Run(() => Parse(target));
                    _entries = entries;
                    LoadedPath = target;
                    IsE621 = System.IO.Path.GetFileName(target).Contains("e621", StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    LogService.Error($"No se pudo cargar el archivo de tags {target}", ex);
                }
            }
        }
        finally
        {
            IsLoading = false;
            StateChanged?.Invoke();
        }
    }

    private static Entry[] Parse(string path)
    {
        var list = new List<Entry>(150_000);

        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#') continue;

            var c1 = line.IndexOf(',');
            if (c1 <= 0) continue;

            var name = line[..c1].Trim();
            var category = 0;
            long count = 0;
            var aliases = Array.Empty<string>();

            var c2 = line.IndexOf(',', c1 + 1);
            if (c2 < 0)
            {
                int.TryParse(line.AsSpan(c1 + 1), out category);
            }
            else
            {
                int.TryParse(line.AsSpan(c1 + 1, c2 - c1 - 1), out category);

                var c3 = line.IndexOf(',', c2 + 1);
                if (c3 < 0)
                {
                    long.TryParse(line.AsSpan(c2 + 1), out count);
                }
                else
                {
                    long.TryParse(line.AsSpan(c2 + 1, c3 - c2 - 1), out count);

                    var rest = line[(c3 + 1)..].Trim().Trim('"');
                    if (rest.Length > 0)
                    {
                        aliases = rest.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(a => a.ToLowerInvariant())
                            .ToArray();
                    }
                }
            }

            list.Add(new Entry(name, name.ToLowerInvariant(), category, count, aliases));
        }

        return list.ToArray();
    }

    /// <summary>Busca por prefijo (lo mas popular primero), luego por contenido y por alias.
    /// El archivo ya viene ordenado por popularidad, asi que el orden de lectura es el ranking.</summary>
    public List<(Entry Entry, string? Alias)> Search(string query, int limit)
    {
        var prefix = new List<(Entry, string?)>();
        var contains = new List<(Entry, string?)>();
        var byAlias = new List<(Entry, string?)>();

        foreach (var entry in _entries)
        {
            if (entry.Lower.StartsWith(query, StringComparison.Ordinal))
            {
                prefix.Add((entry, null));
                if (prefix.Count >= limit) break; // los de prefijo mandan: no hace falta seguir
                continue;
            }

            if (contains.Count < limit && entry.Lower.Contains(query, StringComparison.Ordinal))
            {
                contains.Add((entry, null));
                continue;
            }

            if (byAlias.Count < limit)
            {
                foreach (var alias in entry.Aliases)
                {
                    if (alias.StartsWith(query, StringComparison.Ordinal))
                    {
                        byAlias.Add((entry, alias));
                        break;
                    }
                }
            }
        }

        return prefix.Concat(contains).Concat(byAlias).Take(limit).ToList();
    }
}

/// <summary>
/// Extension PROPIA que reproduce TagComplete del A1111 original: al escribir en el prompt
/// sugiere tags de Danbooru (o e621) con su categoria en color y su popularidad. Usa los CSV de
/// la extension TagComplete si esta instalada en A1111, o cualquier CSV que dejes en la carpeta
/// de tags del Launcher. No modifica la peticion: solo anade sugerencias al escribir.
/// </summary>
public class DanbooruTagsExtension : SdExtension
{
    public override string Id => "danbooru-tags";
    public override string DisplayName => "Tags de Danbooru (autocompletar)";
    public override string Description =>
        "Sugiere tags mientras escribes el prompt, como TagComplete. Tab o Enter para aceptar.";

    public override bool AffectsRequest => false;
    public override IAutocompleteProvider? AutocompleteProvider => _provider;

    public static readonly string UserTagsFolder =
        System.IO.Path.Combine(AppSettingsService.AppFolder, "imagegen", "tags");

    private readonly TagDatabase _db = new();
    private readonly Provider _provider;

    private readonly ComboBox _files = new();
    private readonly TextBlock _status;
    private readonly TextBox _minChars = MakeBox("2");
    private readonly TextBox _maxResults = MakeBox("30");
    private readonly CheckBox _underscores = MakeCheck("Reemplazar _ por espacio", true);
    private readonly CheckBox _escape = MakeCheck("Escapar parentesis: ( ) pasa a \\( \\)", true);
    private readonly CheckBox _comma = MakeCheck("Anadir coma tras el tag", true);

    private string? _wantedFile;
    private bool _updating;

    public DanbooruTagsExtension()
    {
        _provider = new Provider(this);

        _status = new TextBlock
        {
            FontFamily = Res<System.Windows.Media.FontFamily>("Font.Mono"),
            FontSize = 10,
            Foreground = Res<System.Windows.Media.Brush>("Brush.TextDim"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 10)
        };

        _files.SelectionChanged += async (_, _) =>
        {
            if (_updating || _files.SelectedItem is not TagFile file) return;
            _wantedFile = file.Path;
            if (IsEnabled) await LoadSelectedAsync();
        };

        _db.StateChanged += UpdateStatus;

        Changed += async () =>
        {
            UpdateStatus();
            if (IsEnabled && !_db.IsLoaded && !_db.IsLoading) await LoadSelectedAsync();
        };

        RefreshFiles();
    }

    // ===================== Archivos de tags =====================

    private static bool SamePath(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static List<TagFile> Discover()
    {
        var files = new List<TagFile>();

        void AddFolder(string folder, string source)
        {
            if (!Directory.Exists(folder)) return;

            foreach (var csv in Directory.EnumerateFiles(folder, "*.csv").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                if (!files.Any(f => SamePath(f.Path, csv))) files.Add(new TagFile(csv, source));
            }
        }

        try
        {
            // TagComplete instalado en A1111: reutilizamos sus CSV (carpeta de extensiones configurable).
            var extensions = ImageGenSettings.Instance.FolderFor(SdFolder.Extensions);
            if (extensions != null && Directory.Exists(extensions))
            {
                foreach (var dir in Directory.EnumerateDirectories(extensions, "*tagcomplete*"))
                {
                    AddFolder(System.IO.Path.Combine(dir, "tags"), "TagComplete");
                }
            }

            Directory.CreateDirectory(UserTagsFolder);
            AddFolder(UserTagsFolder, "Launcher");
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudieron buscar archivos de tags", ex);
        }

        return files;
    }

    private void RefreshFiles()
    {
        var files = Discover();

        // Un archivo elegido a mano fuera de las carpetas conocidas tambien se conserva.
        if (_wantedFile != null && File.Exists(_wantedFile) && !files.Any(f => SamePath(f.Path, _wantedFile)))
        {
            files.Add(new TagFile(_wantedFile, "personalizado"));
        }

        _updating = true;
        try
        {
            _files.ItemsSource = files;
            _files.SelectedItem =
                files.FirstOrDefault(f => _wantedFile != null && SamePath(f.Path, _wantedFile))
                ?? files.FirstOrDefault(f => System.IO.Path.GetFileName(f.Path).Equals("danbooru.csv", StringComparison.OrdinalIgnoreCase))
                ?? files.FirstOrDefault();
        }
        finally
        {
            _updating = false;
        }

        if (_files.SelectedItem is TagFile selected) _wantedFile = selected.Path;
        UpdateStatus();
    }

    protected override Task LoadServerDataAsync(StableDiffusionService sd)
    {
        // Tras conectar ya puede conocerse la carpeta de instalacion: buscamos de nuevo.
        RefreshFiles();
        if (IsEnabled) return LoadSelectedAsync();
        return Task.CompletedTask;
    }

    private Task LoadSelectedAsync() =>
        _files.SelectedItem is TagFile file ? _db.LoadAsync(file.Path) : Task.CompletedTask;

    private void UpdateStatus()
    {
        _status.Text =
            _db.IsLoading ? "cargando tags..." :
            _db.IsLoaded ? $"{_db.Count:N0} tags cargados" :
            _files.Items.Count == 0
                ? "no hay archivos de tags: instala TagComplete en A1111 o copia un danbooru.csv a la carpeta de tags"
                : IsEnabled ? "sin cargar" : "se carga al activar la extension";
    }

    // ===================== Panel =====================

    public override FrameworkElement BuildPanel()
    {
        var browse = new Button
        {
            Content = "Archivo...",
            Style = Res<Style>("SecondaryButtonStyle"),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        browse.Click += async (_, _) =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "Archivo CSV de tags",
                Filter = "CSV de tags (*.csv)|*.csv|Todos los archivos (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            _wantedFile = dialog.FileName;
            RefreshFiles();
            if (IsEnabled) await LoadSelectedAsync();
        };

        var rescan = new Button
        {
            Content = "Buscar",
            Style = Res<Style>("SecondaryButtonStyle"),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        rescan.Click += (_, _) => RefreshFiles();

        var openFolder = new Button
        {
            Content = "Carpeta de tags",
            Style = Res<Style>("SecondaryButtonStyle"),
            Padding = new Thickness(10, 6, 10, 6)
        };
        openFolder.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(UserTagsFolder);
                Process.Start(new ProcessStartInfo { FileName = UserTagsFolder, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LogService.Error("No se pudo abrir la carpeta de tags", ex);
            }
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(browse);
        buttons.Children.Add(rescan);
        buttons.Children.Add(openFolder);

        var panel = new StackPanel();
        panel.Children.Add(MakeField("ARCHIVO DE TAGS", _files, 0));
        panel.Children.Add(buttons);
        panel.Children.Add(_status);
        panel.Children.Add(MakePair(MakeField("MIN. CARACTERES", _minChars, 0), MakeField("MAX. RESULTADOS", _maxResults, 0), 10));
        panel.Children.Add(_underscores);
        panel.Children.Add(_escape);
        panel.Children.Add(_comma);
        return panel;
    }

    // No modifica la peticion: solo aporta sugerencias.
    public override void Apply(SdRequest request) { }

    protected override JsonObject SaveCore() => new()
    {
        ["file"] = _wantedFile,
        ["min"] = _minChars.Text,
        ["max"] = _maxResults.Text,
        ["underscores"] = _underscores.IsChecked == true,
        ["escape"] = _escape.IsChecked == true,
        ["comma"] = _comma.IsChecked == true
    };

    protected override void LoadCore(JsonObject state)
    {
        _minChars.Text = Str(state, "min", "2");
        _maxResults.Text = Str(state, "max", "30");
        _underscores.IsChecked = (bool?)state["underscores"] ?? true;
        _escape.IsChecked = (bool?)state["escape"] ?? true;
        _comma.IsChecked = (bool?)state["comma"] ?? true;

        _wantedFile = (string?)state["file"] ?? _wantedFile;
        RefreshFiles();
    }

    // ===================== Ajustes leidos por el proveedor =====================

    private int MinChars => int.TryParse(_minChars.Text.Trim(), out var v) ? Math.Clamp(v, 1, 10) : 2;
    private int MaxResults => int.TryParse(_maxResults.Text.Trim(), out var v) ? Math.Clamp(v, 5, 100) : 30;

    private static readonly string[] CategoryNames = { "general", "artista", "", "copyright", "personaje", "meta" };

    /// <summary>Colores por categoria de Danbooru: 0 general, 1 artista, 3 copyright, 4 personaje, 5 meta.
    /// e621 usa otro reparto (5 = especie, 7 = meta, 8 = lore).</summary>
    private static string CategoryColor(int category, bool e621) => category switch
    {
        0 => "#FF6CB6FF",
        1 => e621 ? "#FFFFD24A" : "#FFFF6B6B",
        3 => "#FFD97BFF",
        4 => "#FF4CD97B",
        5 => e621 ? "#FFFF6B6B" : "#FFFFB347",
        7 => "#FFFFB347",
        8 => "#FF20C9B0",
        _ => "#FFB0B8B3"
    };

    private static string FormatCount(long n) => n switch
    {
        >= 1_000_000 => (n / 1_000_000d).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (n / 1_000d).ToString("0.#", CultureInfo.InvariantCulture) + "k",
        _ => n.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>Texto que se inserta: con espacios en vez de _ y parentesis escapados (segun ajustes).
    /// Los kaomojis (^_^, o_o, :3...) conservan sus guiones bajos.</summary>
    private string FormatTag(string tag)
    {
        var text = tag;

        var kaomoji = tag.Length <= 3 || tag.IndexOfAny(new[] { '^', '<', '>', ';', '|', ':', '=', '@' }) >= 0;
        if (_underscores.IsChecked == true && !kaomoji) text = text.Replace('_', ' ');
        if (_escape.IsChecked == true) text = text.Replace("(", "\\(").Replace(")", "\\)");

        return text;
    }

    // ===================== Proveedor =====================

    private sealed class Provider : IAutocompleteProvider
    {
        // Separan tags en un prompt (como en TagComplete); el espacio NO separa porque los tags llevan espacios.
        private static readonly char[] Separators = { ',', '\n', '\r', '(', ')', '[', ']', '{', '}', '|', ':', '<', '>' };
        private static readonly Regex PureNumber = new(@"^[0-9.]+$", RegexOptions.Compiled);

        private readonly DanbooruTagsExtension _owner;

        public Provider(DanbooruTagsExtension owner) => _owner = owner;

        public AutocompleteResult? GetSuggestions(string textBeforeCaret)
        {
            var db = _owner._db;
            if (!db.IsLoaded) return null;

            // Dentro de un <lora:...> sin cerrar no se sugieren tags.
            if (textBeforeCaret.LastIndexOf('<') > textBeforeCaret.LastIndexOf('>')) return null;

            var separator = textBeforeCaret.LastIndexOfAny(Separators);
            var token = textBeforeCaret[(separator + 1)..].TrimStart();

            if (token.Length < _owner.MinChars || token.StartsWith("__", StringComparison.Ordinal) ||
                PureNumber.IsMatch(token))
            {
                return null;
            }

            var query = token.ToLowerInvariant().Replace(' ', '_');
            var hits = db.Search(query, _owner.MaxResults);
            if (hits.Count == 0) return null;

            var suffix = _owner._comma.IsChecked == true ? ", " : "";

            var items = hits.Select(hit =>
            {
                var entry = hit.Entry;
                var inserted = _owner.FormatTag(entry.Name);
                var categoryName = entry.Category >= 0 && entry.Category < CategoryNames.Length
                    ? CategoryNames[entry.Category]
                    : "";

                var note = entry.Aliases.Length > 0
                    ? $"{categoryName}\nalias: {string.Join(", ", entry.Aliases.Take(8))}"
                    : categoryName;

                return new Suggestion
                {
                    Label = hit.Alias == null ? inserted : $"{hit.Alias.Replace('_', ' ')} → {inserted}",
                    Insert = inserted,
                    Suffix = suffix,
                    Detail = FormatCount(entry.Count),
                    Note = note.Length > 0 ? note : null,
                    Tint = SuggestionBrushes.Get(CategoryColor(entry.Category, db.IsE621))
                };
            }).ToList();

            return new AutocompleteResult(token.Length, items);
        }
    }
}
