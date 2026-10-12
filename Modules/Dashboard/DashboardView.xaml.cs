using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Modules.Commands;
using Launcher.Modules.Games;
using Launcher.Modules.Processes;
using Launcher.UI.Controls;

namespace Launcher.Modules.Dashboard;

public partial class DashboardView : UserControl
{
    private enum ProcMetric { Cpu, Ram, Gpu }

    private const int MaxGames = 6;
    private const int MaxFolders = 8;
    private const int MaxCommands = 8;
    private const int TopProcessCount = 5;

    /// <summary>Por encima de este porcentaje la barra pasa al color de aviso.</summary>
    private const double WarningPercent = 85;

    private static readonly CultureInfo Spanish = new("es-ES");

    private readonly NowPlayingService _nowPlaying = new();
    private readonly SystemMonitorService _system = new();
    private readonly ProcessMonitorService _processes = new();

    /// <summary>Un unico temporizador de 1 s. Cada tarea corre a su ritmo: lo que suena cada
    /// segundo, el sistema cada 2 y los procesos cada 4 (la parte mas cara).</summary>
    private readonly DispatcherTimer _timer;

    private FolderShortcutsService? _folders;
    private CommandsService? _commands;

    private bool _nowPlayingReady;
    private bool _sampling;
    private bool _refreshingProcesses;
    private int _tick;

    private ProcMetric _metric = ProcMetric.Cpu;
    private List<ProcessGroup> _groups = new();

    public DashboardView()
    {
        InitializeComponent();

        NowPlayingCard.DataContext = _nowPlaying.Info;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await OnTickAsync();

        // La vista se cachea en MainWindow: Loaded/Unloaded se repiten al cambiar de modulo.
        // Solo se muestrea mientras el dashboard esta a la vista.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshShortcuts();
        UpdateClock();

        if (!_nowPlayingReady)
        {
            _nowPlayingReady = true;
            await _nowPlaying.InitializeAsync();
        }

        _tick = 0;
        await Task.WhenAll(RefreshNowPlayingAsync(), SampleSystemAsync(), RefreshProcessesAsync());

        // Si el usuario cambio de modulo mientras tanto, no arrancamos el temporizador.
        if (IsLoaded) _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _processes.Dispose(); // descarta las referencias de CPU/GPU: al volver se vuelven a fijar
    }

    private async Task OnTickAsync()
    {
        _tick++;
        UpdateClock();

        var tasks = new List<Task> { RefreshNowPlayingAsync() };
        if (_tick % 2 == 0) tasks.Add(SampleSystemAsync());
        if (_tick % 4 == 0) tasks.Add(RefreshProcessesAsync());

        await Task.WhenAll(tasks);
    }

    private void UpdateClock() =>
        DateText.Text = DateTime.Now.ToString("dddd d 'de' MMMM  \u00b7  HH:mm", Spanish);

    // ===================== Accesos: juegos, carpetas, comandos =====================

    /// <summary>Se reconstruye cada vez que el dashboard se muestra, asi refleja los cambios
    /// hechos en los otros modulos (un juego recien jugado, una carpeta nueva...).</summary>
    private void RefreshShortcuts()
    {
        // Juegos: los jugados mas recientemente; si faltan para llenar la fila, favoritos.
        var visibleGames = GameLibraryService.Instance.Games.Where(g => !g.Hidden).ToList();
        var recent = visibleGames
            .Where(g => g.LastPlayed != null)
            .OrderByDescending(g => g.LastPlayed)
            .ToList();
        var favorites = visibleGames
            .Where(g => g.Favorite && !recent.Contains(g))
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase);

        var games = recent.Concat(favorites).Take(MaxGames).Select(g => new GameTile { Game = g }).ToList();
        GamesItems.ItemsSource = games;
        GamesEmpty.Visibility = games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Carpetas: las que no esten ocultas, en el orden del modulo Carpetas.
        _folders = new FolderShortcutsService();
        var folders = _folders.Folders.Where(f => !f.Hidden).Take(MaxFolders).ToList();
        FoldersItems.ItemsSource = folders;
        FoldersEmpty.Visibility = folders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Comandos: favoritos primero, despues los usados hace menos, despues por nombre.
        _commands = new CommandsService();
        var commands = _commands.Commands
            .OrderByDescending(c => c.Favorite)
            .ThenByDescending(c => c.LastUsed ?? DateTime.MinValue)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxCommands)
            .ToList();
        CommandsItems.ItemsSource = commands;
        CommandsEmpty.Visibility = commands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void GameTile_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not GameTile tile) return;

        if (!GameLibraryService.Instance.Launch(tile.Game))
        {
            MessageBox.Show(Window.GetWindow(this),
                $"No se ha podido lanzar \"{tile.Name}\". Comprueba que la ruta del ejecutable sigue siendo valida (Juegos > Editar).",
                "Lanzar juego", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void FolderTile_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is FolderShortcut folder) _folders?.Open(folder);
    }

    private void CommandTile_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is CommandShortcut command) _commands?.Launch(command);
    }

    // ===================== Navegacion a otros modulos =====================

    private void SectionLink_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string moduleId }) NavigateToModule(moduleId);
    }

    /// <summary>Marca el boton del sidebar del modulo: MainWindow ya navega al marcarse, asi no
    /// hace falta tocar su codigo.</summary>
    private static void NavigateToModule(string moduleId)
    {
        if (Application.Current.MainWindow is not MainWindow main) return;

        var button = main.NavPanel.Children
            .OfType<NavButton>()
            .FirstOrDefault(b => (string?)b.Tag == moduleId);

        if (button != null) button.IsChecked = true;
    }

    // ===================== Reproduciendo =====================

    private async Task RefreshNowPlayingAsync()
    {
        try
        {
            await _nowPlaying.RefreshAsync();
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la lectura de reproduccion en el dashboard", ex);
        }
    }

    private async void PlayPause_Click(object sender, RoutedEventArgs e) => await _nowPlaying.TogglePlayPauseAsync();
    private async void Next_Click(object sender, RoutedEventArgs e) => await _nowPlaying.NextAsync();
    private async void Previous_Click(object sender, RoutedEventArgs e) => await _nowPlaying.PreviousAsync();

    // ===================== Sistema =====================

    /// <summary>El muestreo (contadores de rendimiento, WMI la primera vez) va en un hilo de
    /// fondo y no se solapa: si el anterior no ha terminado, se salta.</summary>
    private async Task SampleSystemAsync()
    {
        if (_sampling) return;
        _sampling = true;

        try
        {
            var snapshot = await Task.Run(() => _system.Sample());
            ApplySnapshot(snapshot);
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo el muestreo del sistema en el dashboard", ex);
        }
        finally
        {
            _sampling = false;
        }
    }

    private void ApplySnapshot(SystemSnapshot snap)
    {
        SetMetric(CpuBar, CpuValue, snap.CpuPercent, $"{snap.CpuPercent:0}%");

        SetMetric(RamBar, RamValue, snap.RamPercent,
            $"{ToGb(snap.RamUsedBytes):0.#} / {ToGb(snap.RamTotalBytes):0.#} GB");

        SetMetric(GpuBar, GpuValue, snap.GpuPercent, $"{snap.GpuPercent:0}%");

        var vramText = snap.VramTotalBytes > 0
            ? $"{ToGb(snap.VramUsedBytes):0.#} / {ToGb(snap.VramTotalBytes):0.#} GB"
            : $"{ToGb(snap.VramUsedBytes):0.#} GB";
        SetMetric(VramBar, VramValue, snap.VramPercent, vramText);

        NetText.Text = $"\u2193 {FormatRate(snap.NetDownBytesPerSec)}   \u2191 {FormatRate(snap.NetUpBytesPerSec)}";

        DrivesText.Text = snap.Drives.Count == 0
            ? ""
            : string.Join("  \u00b7  ", snap.Drives.Take(4).Select(d => $"{d.Name} {d.UsedPercent:0}%"));

        UptimeText.Text = $"Actividad: {FormatUptime(snap.Uptime)}";
    }

    private static void SetMetric(ProgressBar bar, TextBlock valueText, double percent, string text)
    {
        bar.Value = Math.Clamp(percent, 0, 100);
        valueText.Text = text;
        bar.SetResourceReference(Control.ForegroundProperty, percent >= WarningPercent ? "Brush.Warning" : "Brush.Accent");
    }

    private static double ToGb(ulong bytes) => bytes / 1024d / 1024 / 1024;

    private static string FormatRate(double bytesPerSec)
    {
        var kb = bytesPerSec / 1024d;
        return kb >= 1024 ? $"{kb / 1024:0.#} MB/s" : $"{kb:0.#} KB/s";
    }

    private static string FormatUptime(TimeSpan ts)
    {
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{ts.Minutes}m {ts.Seconds}s";
    }

    // ===================== Top procesos =====================

    private async Task RefreshProcessesAsync()
    {
        if (_refreshingProcesses) return;
        _refreshingProcesses = true;

        try
        {
            _groups = await Task.Run(() =>
            {
                _processes.Refresh();
                return _processes.BuildGroups();
            });

            RebuildTopProcesses();
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo el refresco de procesos en el dashboard", ex);
        }
        finally
        {
            _refreshingProcesses = false;
        }
    }

    private void Metric_Checked(object sender, RoutedEventArgs e)
    {
        // Checked tambien se dispara al cargar el XAML (la cabecera CPU viene marcada), cuando
        // los demas controles aun no existen: hasta Loaded no hacemos nada.
        if (!IsLoaded) return;

        if (sender is RadioButton { Tag: string tag } && Enum.TryParse<ProcMetric>(tag, out var metric))
        {
            _metric = metric;
            RebuildTopProcesses();
        }
    }

    private void RebuildTopProcesses()
    {
        // Desempates por RAM para que el orden no baile cuando todo vale 0 (p. ej. GPU).
        IEnumerable<ProcessGroup> ordered = _metric switch
        {
            ProcMetric.Ram => _groups.OrderByDescending(g => g.TotalRamMB),
            ProcMetric.Gpu => _groups.OrderByDescending(g => g.TotalGpu).ThenByDescending(g => g.TotalRamMB),
            _ => _groups.OrderByDescending(g => g.TotalCpu).ThenByDescending(g => g.TotalRamMB)
        };

        var rows = ordered
            .Take(TopProcessCount)
            .Select(g => new TopProcessRow
            {
                Icon = g.Root.Icon,
                Name = g.Root.Name,
                CountLabel = g.MemberCount > 1 ? $"\u00d7{g.MemberCount}" : "",
                CpuText = $"{g.TotalCpu:0}%",
                RamText = FormatMemory(g.TotalRamMB),
                GpuText = $"{g.TotalGpu:0}%",
                IsCpu = _metric == ProcMetric.Cpu,
                IsRam = _metric == ProcMetric.Ram,
                IsGpu = _metric == ProcMetric.Gpu
            })
            .ToList();

        TopProcessesList.ItemsSource = rows;
    }

    private static string FormatMemory(double megabytes) =>
        megabytes >= 1024 ? $"{megabytes / 1024:0.#} GB" : $"{megabytes:0} MB";
}
