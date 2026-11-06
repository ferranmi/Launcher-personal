using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Launcher.Core.Services;
using Launcher.Modules.Dashboard.Widgets;
using Launcher.Modules.Models.Widgets;

namespace Launcher.Modules.Models;

/// <summary>Pestana Dashboard del modulo: todas las estadisticas del README, actividad reciente y estado del entorno.
/// Cada tarjeta lleva a la seccion correspondiente. Se construye en codigo con las piezas del tema.</summary>
public sealed class ModelsDashboardPanel : UserControl
{
    private readonly WrapPanel _tiles = new();
    private readonly StackPanel _env = new();
    private readonly StackPanel _activity = new();
    private readonly TextBlock _status = ModelsUi.Small("");
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };

    public ModelsDashboardPanel()
    {
        var left = new StackPanel();

        left.Children.Add(Kit.Label("RESUMEN", 11, "Brush.Accent", mono: true));
        left.Children.Add(ModelsUi.Gap(8));
        left.Children.Add(_tiles);

        left.Children.Add(ModelsUi.Gap(6));
        var actions = new WrapPanel();
        actions.Children.Add(Spaced(Kit.ActionButton("Reescanear biblioteca", async (sender, _) =>
        {
            ((Button)sender).IsEnabled = false;
            try { await ModelCatalogService.Instance.ScanAsync(); }
            finally { ((Button)sender).IsEnabled = true; Refresh(); }
        }, primary: true)));
        actions.Children.Add(Spaced(Kit.ActionButton("Analizar referencias", async (sender, _) =>
        {
            ((Button)sender).IsEnabled = false;
            try
            {
                _status.Text = await ModelsStats.AnalyzeReferencesAsync()
                    ? "referencias analizadas"
                    : "primero escanea la biblioteca";
            }
            catch (Exception ex)
            {
                LogService.Error("Fallo el analisis desde el dashboard", ex);
                _status.Text = "no se pudieron leer los wildcards";
            }
            finally { ((Button)sender).IsEnabled = true; Refresh(); }
        })));
        left.Children.Add(actions);
        left.Children.Add(_status);

        left.Children.Add(ModelsUi.Gap(18));
        left.Children.Add(Kit.Label("ENTORNO", 11, "Brush.Accent", mono: true));
        left.Children.Add(ModelsUi.Gap(8));
        left.Children.Add(_env);

        var right = new StackPanel { Margin = new Thickness(14) };
        right.Children.Add(Kit.Label("ACTIVIDAD RECIENTE", 11, "Brush.Accent", mono: true));
        right.Children.Add(ModelsUi.Gap(8));
        right.Children.Add(_activity);

        var activityBox = new Border
        {
            Style = (Style)Application.Current.FindResource("CardStyle"),
            Margin = new Thickness(14, 0, 0, 0),
            Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = right }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });

        var leftScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = left };
        grid.Children.Add(leftScroll);
        Grid.SetColumn(activityBox, 1);
        grid.Children.Add(activityBox);

        Content = grid;

        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) =>
        {
            Refresh();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) Refresh();
        };
    }

    private static FrameworkElement Spaced(Button button)
    {
        button.Margin = new Thickness(0, 0, 8, 8);
        return button;
    }

    private Border Tile(string value, string label, string tab, string tooltip, string brush = "Brush.TextPrimary")
    {
        var inner = new StackPanel();
        inner.Children.Add(Kit.Label(value, 24, brush, mono: true));
        inner.Children.Add(ModelsUi.Gap(2));
        var caption = Kit.Label(label, 10, "Brush.TextDim", mono: true);
        caption.TextWrapping = TextWrapping.Wrap;
        caption.TextTrimming = TextTrimming.None;
        inner.Children.Add(caption);

        var tile = new Border
        {
            Style = (Style)Application.Current.FindResource("CardStyle"),
            Width = 158,
            MinHeight = 86,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(12),
            Cursor = Cursors.Hand,
            ToolTip = tooltip,
            Child = inner
        };

        tile.MouseLeftButtonUp += (_, _) => ModelsNavigation.Open(tab);
        return tile;
    }

    private void Refresh()
    {
        try
        {
            var s = ModelsStats.Take();

            _tiles.Children.Clear();
            _tiles.Children.Add(Tile(s.Total.ToString("N0"), "total de modelos", "library", "Abrir la biblioteca"));
            _tiles.Children.Add(Tile(s.Lora.ToString("N0"), "LoRA instalados", "library", "Abrir la biblioteca"));
            _tiles.Children.Add(Tile(s.Checkpoints.ToString("N0"), "checkpoints instalados", "library", "Abrir la biblioteca"));
            _tiles.Children.Add(Tile(s.LyCoris.ToString("N0"), "LyCORIS instalados", "library", "Abrir la biblioteca"));
            _tiles.Children.Add(Tile(s.Duplicates.ToString("N0"), "pendientes de revisar",
                "library", "Modelos con nombre duplicado: una etiqueta seria ambigua",
                s.Duplicates > 0 ? "Brush.Warning" : "Brush.TextPrimary"));
            _tiles.Children.Add(Tile(s.DownloadsActive.ToString("N0"), $"descargas activas ({s.DownloadsQueued} en cola)",
                "downloads", "Abrir las descargas", s.DownloadsActive > 0 ? "Brush.Accent" : "Brush.TextPrimary"));
            _tiles.Children.Add(Tile(s.DownloadsCompleted.ToString("N0"), "descargas completadas", "downloads", "Abrir las descargas"));
            _tiles.Children.Add(Tile(s.DownloadsFailed.ToString("N0"), "descargas con error", "downloads",
                "Descargas que han fallado y se pueden reintentar", s.DownloadsFailed > 0 ? "Brush.Danger" : "Brush.TextPrimary"));

            var refs = ModelsStats.References;
            var pendingRefs = refs == null ? "\u2014" : (refs.Missing + refs.Ambiguous).ToString("N0");
            _tiles.Children.Add(Tile(pendingRefs, refs == null ? "referencias (sin analizar)" : "referencias pendientes",
                "references", "Ausentes y ambiguas segun el ultimo analisis",
                refs is { } r && r.Missing + r.Ambiguous > 0 ? "Brush.Warning" : "Brush.TextPrimary"));

            _env.Children.Clear();
            foreach (var item in ModelsStats.Environment())
            {
                var mark = item.Ok == true ? "OK" : item.Ok == false ? "!!" : "--";
                var row = ModelsUi.Row($"{mark}  {item.Label}", item.Detail, ModelsUi.EnvBrush(item.Ok));
                row.ToolTip = item.Detail;
                _env.Children.Add(row);
            }

            _activity.Children.Clear();
            var entries = ActivityLog.Recent(20);
            if (entries.Count == 0) _activity.Children.Add(ModelsUi.Small("sin actividad todavia"));

            foreach (var entry in entries)
            {
                var item = new StackPanel { Margin = new Thickness(0, 0, 0, 9) };
                item.Children.Add(Kit.Label($"{entry.Time:dd/MM HH:mm}  \u00b7  {entry.Kind}", 9.5, "Brush.TextDim", mono: true));
                var text = Kit.Label(entry.Text, 11.5, ModelsUi.LevelBrush(entry.Level));
                text.TextWrapping = TextWrapping.Wrap;
                text.TextTrimming = TextTrimming.None;
                item.Children.Add(text);
                _activity.Children.Add(item);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo el dashboard de Modelos", ex);
        }
    }
}
