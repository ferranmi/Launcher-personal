using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Launcher.Core.Services;
using Launcher.Modules.Dashboard.Widgets;

namespace Launcher.Modules.Models.Widgets;

/// <summary>Piezas comunes de los widgets de Modelos. Todo sale de Kit y del tema (colores del acento incluidos).</summary>
internal static class ModelsUi
{
    public static TextBlock Big(string text, string brush = "Brush.TextPrimary") => Kit.Label(text, 26, brush, mono: true);

    public static TextBlock Small(string text, string brush = "Brush.TextDim", double size = 10)
    {
        var block = Kit.Label(text, size, brush, mono: true);
        block.TextWrapping = TextWrapping.Wrap;
        block.TextTrimming = TextTrimming.None;
        return block;
    }

    public static Border Gap(double height = 8) => new() { Height = height };

    public static Grid Row(string label, string value, string brush = "Brush.TextPrimary")
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = Kit.Label(label, 11, "Brush.TextSecondary", mono: true);
        var right = Kit.Label(value, 11, brush, mono: true);
        right.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(right, 1);

        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    public static WrapPanel Buttons(params Button[] buttons)
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        foreach (var button in buttons)
        {
            button.Margin = new Thickness(0, 0, 6, 6);
            panel.Children.Add(button);
        }
        return panel;
    }

    public static string LevelBrush(int level) => level switch
    {
        1 => "Brush.Accent",
        2 => "Brush.Warning",
        3 => "Brush.Danger",
        _ => "Brush.TextSecondary"
    };

    public static string EnvBrush(bool? ok) => ok == true ? "Brush.Accent" : ok == false ? "Brush.Warning" : "Brush.TextDim";
}

/// <summary>Base de los widgets: un panel con scroll que se reconstruye con Refresh() al cargarse y cada N segundos
/// (0 = solo al cargarse). Los errores quedan en el registro, nunca tumban el dashboard.</summary>
public abstract class ModelsWidgetBase : UserControl
{
    private readonly DispatcherTimer? _timer;

    protected StackPanel Body { get; } = new();

    protected ModelsWidgetBase(double seconds)
    {
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = Body };

        if (seconds > 0)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            _timer.Tick += (_, _) => RefreshNow();
        }

        Loaded += (_, _) =>
        {
            RefreshNow();
            _timer?.Start();
        };
        Unloaded += (_, _) => _timer?.Stop();
    }

    protected abstract void Refresh();

    protected void RefreshNow()
    {
        try
        {
            Body.Children.Clear();
            Refresh();
        }
        catch (Exception ex)
        {
            LogService.Error($"Fallo el widget {GetType().Name}", ex);
        }
    }
}

// ===================== Biblioteca =====================

/// <summary>Modelos por tipo, duplicados, ultimo escaneo y rutas con problemas.</summary>
public sealed class ModelsLibraryWidget : ModelsWidgetBase
{
    public ModelsLibraryWidget() : base(5) { }

    protected override void Refresh()
    {
        var s = ModelsStats.Take();

        Body.Children.Add(ModelsUi.Big(s.Total.ToString("N0")));
        Body.Children.Add(ModelsUi.Small("modelos en la biblioteca"));
        Body.Children.Add(ModelsUi.Gap());
        Body.Children.Add(ModelsUi.Row("LoRA", s.Lora.ToString("N0")));
        Body.Children.Add(ModelsUi.Row("Checkpoints", s.Checkpoints.ToString("N0")));
        Body.Children.Add(ModelsUi.Row("LyCORIS", s.LyCoris.ToString("N0")));

        Body.Children.Add(ModelsUi.Gap());
        if (s.Duplicates > 0) Body.Children.Add(ModelsUi.Small($"{s.Duplicates} con nombre duplicado", "Brush.Warning"));

        var badPaths = ModelsStats.Environment().Count(e => e.Label is not ("API Key" or "Civitai") && e.Ok == false);
        if (badPaths > 0) Body.Children.Add(ModelsUi.Small($"{badPaths} ruta(s) con problemas", "Brush.Warning"));

        Body.Children.Add(ModelsUi.Small(s.LastScan is { } scan ? $"ultimo escaneo {scan:dd/MM HH:mm}" : "sin escanear todavia"));

        var rescan = Kit.ActionButton("Reescanear", async (sender, _) =>
        {
            ((Button)sender).IsEnabled = false;
            try { await ModelCatalogService.Instance.ScanAsync(); }
            finally { RefreshNow(); }
        });

        Body.Children.Add(ModelsUi.Buttons(
            Kit.ActionButton("Abrir biblioteca", (_, _) => ModelsNavigation.Open("library"), primary: true),
            rescan));
    }
}

// ===================== Descargas =====================

/// <summary>Progreso, velocidad y cola; pausar y reanudar todo.</summary>
public sealed class ModelsDownloadsWidget : ModelsWidgetBase
{
    public ModelsDownloadsWidget() : base(1.5) { }

    protected override void Refresh()
    {
        var s = ModelsStats.Take();
        var queue = DownloadQueueService.Instance;

        var head = $"{s.DownloadsActive} activas  \u00b7  {s.DownloadsQueued} en cola";
        if (s.DownloadSpeed > 0) head += $"  \u00b7  {ModelEntry.FormatSize((long)s.DownloadSpeed)}/s";
        Body.Children.Add(Kit.Label(head, 12, "Brush.TextPrimary", mono: true));
        Body.Children.Add(ModelsUi.Gap(6));

        var shown = queue.Items
            .Where(i => i.State is DownloadState.Downloading or DownloadState.Queued or DownloadState.Paused
                or DownloadState.Verifying or DownloadState.Failed)
            .Take(4)
            .ToList();

        if (shown.Count == 0)
        {
            Body.Children.Add(ModelsUi.Small("sin descargas pendientes"));
        }

        foreach (var item in shown)
        {
            var brush = item.StateLevel switch { 1 => "Brush.Accent", 2 => "Brush.Warning", 3 => "Brush.Danger", _ => "Brush.TextDim" };

            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 9) };
            row.Children.Add(Kit.Label(item.ModelName, 11.5));
            row.Children.Add(Kit.Label($"{item.StateLabel}  {item.ProgressPercent:0}%  {item.SpeedText}", 9.5, brush, mono: true));

            var bar = Kit.Bar(item.ProgressPercent, Kit.Br(brush));
            bar.Margin = new Thickness(0, 4, 0, 0);
            row.Children.Add(bar);
            Body.Children.Add(row);
        }

        Body.Children.Add(ModelsUi.Buttons(
            Kit.ActionButton("Pausar todo", (_, _) => { queue.PauseAll(); RefreshNow(); }),
            Kit.ActionButton("Reanudar todo", (_, _) => { queue.ResumeAll(); RefreshNow(); }),
            Kit.ActionButton("Abrir", (_, _) => ModelsNavigation.Open("downloads"), primary: true)));
    }
}

// ===================== Modelos ausentes =====================

/// <summary>Referencias de wildcards sin modelo, ambiguas y correcciones pendientes. El analisis es manual (boton).</summary>
public sealed class ModelsMissingWidget : ModelsWidgetBase
{
    private string? _note;

    public ModelsMissingWidget() : base(5) { }

    protected override void Refresh()
    {
        var s = ModelsStats.Take();

        if (ModelsStats.References is { } r)
        {
            Body.Children.Add(ModelsUi.Big(r.Missing.ToString("N0"), r.Missing > 0 ? "Brush.Warning" : "Brush.Accent"));
            Body.Children.Add(ModelsUi.Small("referencias ausentes"));
            Body.Children.Add(ModelsUi.Gap());
            Body.Children.Add(ModelsUi.Row("Ambiguas", r.Ambiguous.ToString("N0"), r.Ambiguous > 0 ? "Brush.Warning" : "Brush.TextPrimary"));
            Body.Children.Add(ModelsUi.Row("Referencias unicas", r.Total.ToString("N0")));
            Body.Children.Add(ModelsUi.Small($"analizado a las {r.At:HH:mm}"));
        }
        else
        {
            Body.Children.Add(ModelsUi.Big("\u2014", "Brush.TextDim"));
            Body.Children.Add(ModelsUi.Small("sin analizar: pulsa Analizar para cruzar los wildcards con la biblioteca"));
        }

        Body.Children.Add(ModelsUi.Gap());
        Body.Children.Add(ModelsUi.Row("Correcciones pendientes", s.PendingCorrections.ToString("N0"),
            s.PendingCorrections > 0 ? "Brush.Warning" : "Brush.TextPrimary"));

        if (_note != null) Body.Children.Add(ModelsUi.Small(_note, "Brush.Warning"));

        Body.Children.Add(ModelsUi.Buttons(
            Kit.ActionButton("Analizar", async (sender, _) =>
            {
                ((Button)sender).IsEnabled = false;
                try
                {
                    _note = await ModelsStats.AnalyzeReferencesAsync() ? null : "primero escanea la biblioteca";
                }
                catch (Exception ex)
                {
                    LogService.Error("Fallo el analisis desde el widget", ex);
                    _note = "no se pudieron leer los wildcards";
                }
                finally
                {
                    RefreshNow();
                }
            }),
            Kit.ActionButton("Ver referencias", (_, _) => ModelsNavigation.Open("references"), primary: true)));
    }
}

// ===================== Civitai =====================

/// <summary>Busqueda rapida: escribe, Enter y se abre la pestana Civitai con los resultados.</summary>
public sealed class ModelsCivitaiWidget : ModelsWidgetBase
{
    public ModelsCivitaiWidget() : base(0) { }

    protected override void Refresh()
    {
        Body.Children.Add(Kit.Label("Buscar en Civitai", 12, "Brush.TextPrimary", mono: true));
        Body.Children.Add(ModelsUi.Gap(6));

        var input = Kit.Input("modelo, personaje, estilo...");

        void Go()
        {
            var text = input.Text.Trim();
            if (text.Length > 0) ModelsNavigation.Open("civitai", text);
        }

        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Go();
        };

        Body.Children.Add(input);

        var hasKey = CivitaiService.Instance.HasApiKey;
        Body.Children.Add(ModelsUi.Gap(6));
        Body.Children.Add(ModelsUi.Small(hasKey ? "API Key guardada" : "sin API Key: las descargas daran 401",
            hasKey ? "Brush.Accent" : "Brush.Warning"));

        Body.Children.Add(ModelsUi.Buttons(
            Kit.ActionButton("Buscar", (_, _) => Go(), primary: true),
            Kit.ActionButton("Colecciones", (_, _) => ModelsNavigation.Open("civitai")),
            Kit.ActionButton("Descargas", (_, _) => ModelsNavigation.Open("downloads"))));
    }
}

// ===================== Wildcards =====================

/// <summary>Cuantos wildcards hay, cuantas referencias a modelos contienen y los mas recientes.</summary>
public sealed class ModelsWildcardsWidget : ModelsWidgetBase
{
    private List<WildcardFile> _files = new();
    private DateTime _loadedAt = DateTime.MinValue;
    private bool _loading;

    public ModelsWildcardsWidget() : base(10) { }

    protected override void Refresh()
    {
        if (!_loading && (DateTime.Now - _loadedAt).TotalSeconds > 30) _ = LoadAsync();

        Body.Children.Add(ModelsUi.Big(_files.Count.ToString("N0")));
        Body.Children.Add(ModelsUi.Small("wildcards"));
        Body.Children.Add(ModelsUi.Gap(6));
        Body.Children.Add(ModelsUi.Row("Referencias a modelos", _files.Sum(f => f.ModelRefCount).ToString("N0"), "Brush.Accent"));

        if (_files.Count == 0 && _loadedAt != DateTime.MinValue)
        {
            Body.Children.Add(ModelsUi.Small("sin wildcards: revisa la carpeta en Configuracion", "Brush.Warning"));
        }
        else
        {
            Body.Children.Add(ModelsUi.Gap());
            Body.Children.Add(ModelsUi.Small("MAS RECIENTES"));

            foreach (var file in _files.OrderByDescending(f => f.Modified).Take(5))
            {
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var amount = Kit.Label($"{file.LineCount} lineas", 10, "Brush.TextDim", mono: true);
                amount.Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(amount, 1);

                row.Children.Add(Kit.Label(file.Name, 11.5));
                row.Children.Add(amount);
                Body.Children.Add(row);
            }
        }

        Body.Children.Add(ModelsUi.Buttons(
            Kit.ActionButton("Abrir editor", (_, _) => ModelsNavigation.Open("wildcards"), primary: true)));
    }

    private async Task LoadAsync()
    {
        _loading = true;

        try
        {
            // La variante "quiet" no invalida el indice de referencias en cada refresco del widget.
            _files = await WildcardService.Instance.ListFilesQuietAsync();
            _loadedAt = DateTime.Now;
            RefreshNow();
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudieron listar los wildcards en el widget", ex);
            _loadedAt = DateTime.Now;
        }
        finally
        {
            _loading = false;
        }
    }
}

// ===================== Actividad =====================

/// <summary>Ultimas operaciones: descargas, renombrados, cambios en wildcards, incidencias.</summary>
public sealed class ModelsActivityWidget : ModelsWidgetBase
{
    public ModelsActivityWidget() : base(3) { }

    protected override void Refresh()
    {
        var entries = ActivityLog.Recent(8);

        if (entries.Count == 0)
        {
            Body.Children.Add(ModelsUi.Small("sin actividad todavia"));
            return;
        }

        foreach (var entry in entries)
        {
            var line = Kit.Label($"{entry.Time:HH:mm}  {entry.Kind}: {entry.Text}", 10.5, ModelsUi.LevelBrush(entry.Level), mono: true);
            line.ToolTip = $"{entry.Time:dd/MM/yyyy HH:mm:ss}\n{entry.Text}";
            line.Margin = new Thickness(0, 0, 0, 5);
            Body.Children.Add(line);
        }
    }
}

// ===================== Combinaciones =====================

/// <summary>Acceso rapido al generador de parejas y trios.</summary>
public sealed class ModelsCombosWidget : ModelsWidgetBase
{
    public ModelsCombosWidget() : base(10) { }

    protected override void Refresh()
    {
        var characters = ModelCatalogService.Instance.Models
            .Where(m => m.Type != ModelType.Checkpoint &&
                        m.RelativeFolder.StartsWith(DownloadPlanner.CharactersCategory + "\\", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var origins = characters.Select(m => m.RelativeFolder).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        Body.Children.Add(Kit.Label("Parejas y trios", 12, "Brush.TextPrimary", mono: true));
        Body.Children.Add(ModelsUi.Small("combina personajes de una misma serie y guardalos en un wildcard"));
        Body.Children.Add(ModelsUi.Gap());
        Body.Children.Add(ModelsUi.Row("Origenes (series)", origins.ToString("N0")));
        Body.Children.Add(ModelsUi.Row("Personajes", characters.Count.ToString("N0")));

        Body.Children.Add(ModelsUi.Buttons(
            Kit.ActionButton("Abrir generador", (_, _) => ModelsNavigation.Open("combos"), primary: true)));
    }
}

// ===================== Estado del entorno =====================

/// <summary>Rutas configuradas, API Key y conexion con Civitai.</summary>
public sealed class ModelsEnvWidget : ModelsWidgetBase
{
    public ModelsEnvWidget() : base(5) { }

    protected override void Refresh()
    {
        foreach (var item in ModelsStats.Environment())
        {
            var mark = item.Ok == true ? "OK" : item.Ok == false ? "!!" : "--";
            var row = ModelsUi.Row($"{mark}  {item.Label}", Shorten(item.Detail), ModelsUi.EnvBrush(item.Ok));
            row.ToolTip = item.Detail;
            Body.Children.Add(row);
        }

        Body.Children.Add(ModelsUi.Buttons(
            Kit.ActionButton("Probar conexion", async (sender, _) =>
            {
                ((Button)sender).IsEnabled = false;
                try
                {
                    var (ok, message) = await CivitaiService.Instance.TestConnectionAsync();
                    ModelsStats.SetConnection(ok, message);
                }
                catch (Exception ex)
                {
                    LogService.Error("Fallo la prueba de conexion desde el widget", ex);
                }
                finally
                {
                    RefreshNow();
                }
            }, primary: true),
            Kit.ActionButton("Configuracion", (_, _) => ModelsNavigation.Open("settings"))));
    }

    private static string Shorten(string text) => text.Length <= 28 ? text : "..." + text[^25..];
}
