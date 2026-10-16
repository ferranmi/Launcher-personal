using System.Windows;
using System.Windows.Threading;
using Launcher.Core.Models;
using Launcher.Modules.Processes;

namespace Launcher.Core.Services;

/// <summary>
/// Unico dueno de la vigilancia del PC: monitor del sistema, procesos, StatusEvaluator,
/// UplinkService y lector del Visor de eventos. Arranca con la app (MainWindow) y sigue
/// corriendo aunque el dashboard no se vea, asi el registro y las alertas no se pierden.
///
/// Para gastar poco, el intervalo se adapta:
///   dashboard visible      -> StatusIntervalSeconds (1 por defecto)
///   otro modulo visible    -> 3 s
///   ventana oculta/minimiz -> 10 s
/// Las vistas (Dashboard) solo se suscriben a Updated mientras estan en pantalla.
/// Nota: las reglas "sostenido" cuentan muestras, no segundos, asi que en segundo plano
/// reaccionan mas despacio.
/// </summary>
public sealed class SystemStatusHost : IDisposable
{
    public static SystemStatusHost Instance { get; } = new();

    private static readonly TimeSpan EventLogInterval = TimeSpan.FromSeconds(60);

    // El monitor no necesita su propio top de procesos: ya lo da ProcessMonitorService.
    private readonly SystemMonitorService _monitor = new() { IncludeTopProcesses = false };
    private readonly ProcessMonitorService _procs = new();
    private readonly WindowsEventLogService _eventLog = new();
    private readonly AppSettingsService _settings = AppSettingsService.Instance;

    private DispatcherTimer? _timer;
    private Window? _window;
    private bool _dashboardVisible;
    private bool _ticking;
    private bool _started;
    private int _cycle;
    private int _eventLogRunning;
    private DateTime _nextEventLog = DateTime.MinValue;

    public StatusEvaluator Evaluator { get; }
    public UplinkService Uplink { get; }

    public SystemSnapshot? Snapshot { get; private set; }
    public StatusReport? Report { get; private set; }
    public IReadOnlyList<ProcessGroup> Groups { get; private set; } = Array.Empty<ProcessGroup>();

    /// <summary>Se dispara en el hilo de la UI tras cada lectura.</summary>
    public event Action? Updated;

    private SystemStatusHost()
    {
        Evaluator = new StatusEvaluator(StatusEvaluator.CreateThresholds(_settings.ThresholdProfile));
        Uplink = new UplinkService(Evaluator);
    }

    /// <summary>Llamar una vez, desde el constructor de MainWindow.</summary>
    public void Start(Window window)
    {
        if (_started) return;
        _started = true;

        _window = window;
        window.StateChanged += (_, _) => UpdateInterval();
        window.IsVisibleChanged += (_, _) => UpdateInterval();
        _settings.Changed += (_, _) => ApplySettings();

        Evaluator.Report(StatusLevel.Online, "Launcher", "sistema iniciado");

        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => Tick();
        UpdateInterval();
        _timer.Start();

        // La primera lectura espera a que la ventana termine de arrancar.
        window.Dispatcher.BeginInvoke(new Action(Tick), DispatcherPriority.ContextIdle);
    }

    /// <summary>El dashboard avisa al entrar y salir de pantalla para subir o bajar el ritmo.</summary>
    public void SetDashboardVisible(bool visible)
    {
        _dashboardVisible = visible;
        UpdateInterval();
    }

    /// <summary>Reaplica perfil de umbrales e intervalo (se llama solo al guardar Configuracion).</summary>
    public void ApplySettings()
    {
        Evaluator.SetThresholds(StatusEvaluator.CreateThresholds(_settings.ThresholdProfile));
        UpdateInterval();
    }

    private TimeSpan CurrentInterval()
    {
        var hidden = _window == null || !_window.IsVisible || _window.WindowState == WindowState.Minimized;
        if (hidden) return TimeSpan.FromSeconds(10);
        if (!_dashboardVisible) return TimeSpan.FromSeconds(3);
        return TimeSpan.FromSeconds(Math.Clamp(_settings.StatusIntervalSeconds, 1, 5));
    }

    private void UpdateInterval()
    {
        if (_timer == null) return;

        var interval = CurrentInterval();
        if (_timer.Interval != interval) _timer.Interval = interval;
    }

    private void Tick()
    {
        if (_ticking) return; // una lectura lenta no debe solaparse con la siguiente
        _ticking = true;

        try
        {
            var snap = _monitor.Sample();

            // Leer procesos es lo mas caro: con el dashboard a 1 s basta con hacerlo cada 2 ciclos.
            var fast = _dashboardVisible && _timer != null && _timer.Interval <= TimeSpan.FromSeconds(1.5);
            if (!fast || _cycle % 2 == 0)
            {
                _procs.Refresh();
                Groups = _procs.BuildGroups();
            }

            var frozen = _procs.Processes.Where(p => p.IsFrozen).Select(p => p.Name).ToList();

            Snapshot = snap;
            Report = Evaluator.Evaluate(snap, frozen);

            Uplink.Refresh();
            MaybeReadWindowsEvents();

            _cycle++;
            Updated?.Invoke();
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la lectura del estado del sistema", ex);
        }
        finally
        {
            _ticking = false;
        }
    }

    /// <summary>El Visor de eventos se lee en segundo plano cada minuto y las entradas
    /// vuelven al hilo de la UI para entrar en el registro.</summary>
    private void MaybeReadWindowsEvents()
    {
        if (!_settings.WindowsEventLog) return;
        if (DateTime.UtcNow < _nextEventLog) return;
        if (Interlocked.Exchange(ref _eventLogRunning, 1) == 1) return;

        _nextEventLog = DateTime.UtcNow + EventLogInterval;
        var dispatcher = Application.Current.Dispatcher;

        _ = Task.Run(() =>
        {
            List<WindowsEventItem> items;
            try
            {
                items = _eventLog.ReadNew();
            }
            catch (Exception ex)
            {
                LogService.Error("Fallo la lectura del Visor de eventos", ex);
                items = new List<WindowsEventItem>();
            }

            dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var item in items)
                    Evaluator.Report(item.Level, item.Source, item.Message, time: item.Time);

                Interlocked.Exchange(ref _eventLogRunning, 0);
            }));
        });
    }

    public void Dispose()
    {
        _timer?.Stop();
        _monitor.Dispose();
        _procs.Dispose();
        Uplink.Dispose();
    }
}
