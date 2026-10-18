using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Modules.Commands;
using Launcher.Modules.Games;
using Launcher.Modules.Processes;

// System.Drawing entra por los usings implicitos de WinForms: se fijan los tipos de WPF.
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;

namespace Launcher.Modules.Dashboard;

// ===================== Filas de las listas =====================

public sealed record DashGameTile(Game Game)
{
    public string Name => Game.Name;
    public string Initial => string.IsNullOrWhiteSpace(Game.Name) ? "?" : Game.Name[..1].ToUpperInvariant();
    public string? CoverPath => Game.CoverImagePath;
    public bool HasCover => Game.HasCover;
}

public sealed class LogRow
{
    public string Time { get; init; } = "";
    public string Tag { get; init; } = "";
    public string Message { get; init; } = "";
    public Brush TagBg { get; init; } = Brushes.Transparent;
    public Brush TagFg { get; init; } = Brushes.White;
}

public sealed class UplinkRow
{
    public string Icon { get; init; } = "";
    public string Name { get; init; } = "";
    public string Detail { get; init; } = "";
    public Brush IconBrush { get; init; } = Brushes.White;
    public Brush DotBrush { get; init; } = Brushes.Transparent;
}

public sealed class ProcCell
{
    public string Name { get; init; } = "";
    public double Cpu { get; init; }
    public string Info { get; init; } = "";
    public Brush Bar { get; init; } = Brushes.White;
    public Brush InfoBrush { get; init; } = Brushes.White;
}

/// <summary>
/// Pantalla de inicio. Un unico DispatcherTimer (1 s) alimenta todo: metricas, estado
/// (StatusEvaluator), uplink, procesos y reproductor; se para cuando la vista no esta visible.
/// </summary>
public partial class DashboardView : UserControl
{
    private const int MaxGames = 8;
    private const int MaxShortcuts = 12;
    private const int LogCollapsed = 6;
    private const int LogExpanded = 25;

    private readonly SystemMonitorService _monitor = new();
    private readonly ProcessMonitorService _procs = new();
    private readonly StatusEvaluator _evaluator = new();
    private readonly UplinkService _uplink;
    private readonly NowPlayingService _nowPlaying = new();
    private readonly NowPlayingInfo _info;
    private readonly DispatcherTimer _timer;

    private CommandsService _commands = new();
    private FolderShortcutsService _folders = new();

    private List<ProcessGroup> _groups = new();
    private int _tick;
    private int _logVersion;
    private int _shownLogVersion = -1;
    private bool _logExpanded;
    private bool _initialized;
    private bool _ticking;
    private bool _draggingSlider;

    public DashboardView()
    {
        InitializeComponent();

        // Tamano de las partes fijas (alto del monitor, ancho del reproductor): se recuerda al arrastrar sus divisores.
        var defaultColumns = RootGrid.ColumnDefinitions.Select(col => col.Width).ToArray();
        var defaultRows = RootGrid.RowDefinitions.Select(row => row.Height).ToArray();
        LayoutPersistence.Attach(RootGrid, "dash.cols", columns: true);
        LayoutPersistence.Attach(RootGrid, "dash.rows");

        // "Restablecer disposicion" (clic derecho en el tablero) tambien devuelve esas partes a su tamano original.
        CenterBoard.LayoutReset += (_, _) =>
        {
            for (var i = 0; i < defaultColumns.Length; i++) RootGrid.ColumnDefinitions[i].Width = defaultColumns[i];
            for (var i = 0; i < defaultRows.Length; i++) RootGrid.RowDefinitions[i].Height = defaultRows[i];
            LayoutPersistence.Reset("dash.cols");
            LayoutPersistence.Reset("dash.rows");
        };

        _info = _nowPlaying.Info;
        PlayerPanel.DataContext = _info;

        _uplink = new UplinkService(_evaluator);
        _uplink.Changed += (_, _) => UpdateUplink();
        _evaluator.EventRaised += _ => _logVersion++;
        _evaluator.Report(StatusLevel.Online, "Launcher", "sistema iniciado");

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await TickAsync();

        Loaded += DashboardView_Loaded;
        Unloaded += (_, _) => _timer.Stop();
    }

    private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_initialized)
        {
            _initialized = true;
            await _nowPlaying.InitializeAsync();
        }

        LoadAccess();
        _timer.Start();
        await TickAsync();
    }

    // ===================== Tick =====================

    private async Task TickAsync()
    {
        if (_ticking) return; // un tick lento no debe solaparse con el siguiente
        _ticking = true;

        try
        {
            var snap = _monitor.Sample();

            // Los procesos son lo mas pesado de leer: cada 2 s basta.
            if (_tick % 2 == 0)
            {
                _procs.Refresh();
                _groups = _procs.BuildGroups();
            }

            var frozen = _procs.Processes.Where(p => p.IsFrozen).Select(p => p.Name).ToList();
            var report = _evaluator.Evaluate(snap, frozen);

            _uplink.Refresh();

            UpdateHeader(report.Global);
            UpdateMetrics(snap, report);
            UpdateProcesses();
            UpdateLog();

            if (!_draggingSlider)
            {
                await _nowPlaying.RefreshAsync();
                ProgressSlider.Maximum = Math.Max(_info.DurationSeconds, 0.01);
                ProgressSlider.Value = _info.PositionSeconds;
            }

            _tick++;
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo el refresco del dashboard", ex);
        }
        finally
        {
            _ticking = false;
        }
    }

    // ===================== Brushes =====================

    private Brush B(string key) => (Brush)FindResource(key);

    private Brush LevelBrush(StatusLevel level) => level switch
    {
        StatusLevel.Defcon => B("Brush.Danger"),
        StatusLevel.Warning => B("Brush.Warning"),
        _ => B("Brush.Accent")
    };

    private Brush LevelDimBrush(StatusLevel level) => level switch
    {
        StatusLevel.Defcon => B("Brush.DangerDim"),
        StatusLevel.Warning => B("Brush.WarningDim"),
        _ => B("Brush.AccentDim")
    };

    // ===================== Cabecera y metricas =====================

    private void UpdateHeader(StatusLevel global)
    {
        GlobalStatusText.Text = global switch
        {
            StatusLevel.Defcon => "DEFCON",
            StatusLevel.Warning => "WARNING",
            _ => "ONLINE"
        };
        GlobalStatusText.Foreground = LevelBrush(global);
        GlobalStatusBorder.Background = LevelDimBrush(global);
        GlobalStatusBorder.BorderBrush = LevelBrush(global);

        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm:ss");
        DateText.Text = now.ToString("ddd dd MMM").ToUpperInvariant();
    }

    private void UpdateMetrics(SystemSnapshot s, StatusReport report)
    {
        StatusLevel Lvl(string key) => report.Metrics.TryGetValue(key, out var l) ? l : StatusLevel.Online;

        SetMetric(CpuText, Lvl("CPU"), $"{s.CpuPercent:0}%");
        CpuDetail.Text = $"{Environment.ProcessorCount} hilos";

        var ramUsed = s.RamUsedBytes / 1024d / 1024 / 1024;
        var ramTotal = s.RamTotalBytes / 1024d / 1024 / 1024;
        SetMetric(RamText, Lvl("RAM"), $"{s.RamPercent:0}%");
        RamDetail.Text = $"{ramUsed:0.#} / {ramTotal:0.#} GB";

        SetMetric(GpuText, Lvl("GPU"), $"{s.GpuPercent:0}%");
        GpuDetail.Text = s.GpuName;

        var vramUsed = s.VramUsedBytes / 1024d / 1024 / 1024;
        var vramTotal = s.VramTotalBytes / 1024d / 1024 / 1024;
        SetMetric(VramText, Lvl("VRAM"), $"{s.VramPercent:0}%");
        VramDetail.Text = vramTotal > 0 ? $"{vramUsed:0.#} / {vramTotal:0.#} GB" : $"{vramUsed:0.#} GB";

        NetDownText.Text = $"\u2193 {FormatRate(s.NetDownBytesPerSec)}";
        NetUpText.Text = $"\u2191 {FormatRate(s.NetUpBytesPerSec)}";
    }

    private void SetMetric(TextBlock number, StatusLevel level, string text)
    {
        number.Text = text;
        number.Foreground = LevelBrush(level);
    }

    private static string FormatRate(double bytesPerSec)
    {
        var kb = bytesPerSec / 1024d;
        return kb >= 1024 ? $"{kb / 1024:0.#} MB/s" : $"{kb:0.#} KB/s";
    }

    // ===================== Procesos =====================

    private void UpdateProcesses()
    {
        var top = _groups
            .Where(g => !string.Equals(g.Root.Name, "Idle", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(g => g.AnyFrozen)   // los congelados van fijos delante
            .ThenByDescending(g => g.TotalCpu)
            .Take(5)
            .Select(g =>
            {
                var cpu = g.TotalCpu;
                var level = g.AnyFrozen ? StatusLevel.Warning
                    : cpu >= 95 ? StatusLevel.Defcon
                    : cpu >= 80 ? StatusLevel.Warning
                    : StatusLevel.Online;

                var name = g.MemberCount > 1 ? $"{g.Root.Name} \u00D7{g.MemberCount}" : g.Root.Name;

                return new ProcCell
                {
                    Name = name,
                    Cpu = cpu,
                    Info = g.AnyFrozen ? "congelado" : $"{cpu:0}% \u00B7 {g.TotalRamMB:0} MB",
                    Bar = LevelBrush(level),
                    InfoBrush = level == StatusLevel.Online ? B("Brush.TextDim") : LevelBrush(level)
                };
            })
            .ToList();

        ProcItems.ItemsSource = top;
    }

    // ===================== Registro y uplink =====================

    private void UpdateLog()
    {
        if (_shownLogVersion == _logVersion) return;
        _shownLogVersion = _logVersion;

        var rows = _evaluator.History
            .Take(_logExpanded ? LogExpanded : LogCollapsed)
            .Select(ev =>
            {
                var (fg, bg) = ev.Tag switch
                {
                    "uplink" => (B("Brush.Uplink"), B("Brush.UplinkDim")),
                    "warning" => (B("Brush.Warning"), B("Brush.WarningDim")),
                    "defcon" => (B("Brush.Danger"), B("Brush.DangerDim")),
                    _ => (B("Brush.Accent"), B("Brush.AccentDim"))
                };

                return new LogRow
                {
                    Time = ev.Time.ToString("HH:mm:ss"),
                    Tag = ev.Tag.ToUpperInvariant(),
                    Message = ev.Message,
                    TagFg = fg,
                    TagBg = bg
                };
            })
            .ToList();

        LogItems.ItemsSource = rows;
        LogEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LogToggle_Click(object sender, RoutedEventArgs e)
    {
        _logExpanded = !_logExpanded;
        LogToggleButton.Content = _logExpanded ? "menos" : "ver todo";
        _shownLogVersion = -1; // fuerza reconstruir
        UpdateLog();
    }

    private void UpdateUplink()
    {
        var rows = _uplink.Devices.Select(d =>
        {
            var problem = d.State == UplinkState.Problem;
            var live = d.State is UplinkState.Active or UplinkState.Transferring;

            var parts = new List<string>();
            if (d.ConnectionLabel.Length > 0) parts.Add(d.ConnectionLabel);
            parts.Add(string.IsNullOrWhiteSpace(d.Detail) ? d.StateLabel : d.Detail);

            return new UplinkRow
            {
                Icon = d.Icon,
                Name = d.Name,
                Detail = string.Join(" \u00B7 ", parts),
                IconBrush = problem ? B("Brush.Warning") : B("Brush.Uplink"),
                DotBrush = problem ? B("Brush.Warning") : live ? B("Brush.Uplink") : B("Brush.TextDim")
            };
        }).ToList();

        UplinkItems.ItemsSource = rows;
        UplinkEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===================== Accesos (juegos, comandos, carpetas) =====================

    /// <summary>Recarga los accesos cada vez que se entra en la vista: cambios hechos en
    /// Juegos, Comandos o Carpetas aparecen aqui sin reiniciar. Los servicios de comandos y
    /// carpetas se recrean porque leen su archivo una sola vez al construirse.</summary>
    private void LoadAccess()
    {
        var games = GameLibraryService.Instance.Games
            .Where(g => !g.Hidden)
            .OrderByDescending(g => g.LastPlayed ?? DateTime.MinValue)
            .ThenByDescending(g => g.Favorite)
            .Take(MaxGames)
            .Select(g => new DashGameTile(g))
            .ToList();

        GamesItems.ItemsSource = games;
        GamesEmpty.Visibility = games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _commands = new CommandsService();
        CommandsItems.ItemsSource = _commands.Commands
            .OrderByDescending(c => c.Favorite)
            .ThenByDescending(c => c.LastUsed ?? DateTime.MinValue)
            .Take(MaxShortcuts)
            .ToList();

        _folders = new FolderShortcutsService();
        FoldersItems.ItemsSource = _folders.Folders
            .Where(f => !f.Hidden)
            .Take(MaxShortcuts)
            .ToList();
    }

    private void GameTile_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (((FrameworkElement)sender).DataContext is not DashGameTile tile) return;

        if (!GameLibraryService.Instance.Launch(tile.Game))
        {
            _evaluator.Report(StatusLevel.Warning, tile.Name, $"no se pudo lanzar {tile.Name}");
        }
    }

    private void IconTile_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;

        switch (((FrameworkElement)sender).DataContext)
        {
            case CommandShortcut command:
                _commands.Launch(command);
                break;
            case FolderShortcut folder:
                if (!folder.Exists)
                    _evaluator.Report(StatusLevel.Warning, folder.Name, $"carpeta no encontrada: {folder.Path}");
                _folders.Open(folder);
                break;
        }
    }

    // ===================== Reproductor =====================

    private async void PlayPause_Click(object sender, RoutedEventArgs e) => await _nowPlaying.TogglePlayPauseAsync();
    private async void Next_Click(object sender, RoutedEventArgs e) => await _nowPlaying.NextAsync();
    private async void Previous_Click(object sender, RoutedEventArgs e) => await _nowPlaying.PreviousAsync();

    private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _draggingSlider = true;

    private async void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_draggingSlider) return;
        _draggingSlider = false;
        await _nowPlaying.SeekAsync(ProgressSlider.Value);
    }
}
