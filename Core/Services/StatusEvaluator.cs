using Launcher.Core.Models;

namespace Launcher.Core.Services;

/// <summary>
/// Convierte un SystemSnapshot (y la lista de procesos congelados) en un estado
/// Online / Warning / Defcon por metrica y global, con histeresis para que un pico
/// de un segundo no haga parpadear las etiquetas. Cada cambio de nivel se anade al
/// registro (History), que tambien admite entradas de otras fuentes via Report().
///
/// Pensado para llamarse una vez por tick desde el mismo DispatcherTimer que lee el
/// monitor: no es thread-safe, usalo solo desde el hilo de la UI.
/// </summary>
public sealed class StatusEvaluator
{
    private const int MaxHistory = 200;

    /// <summary>Muestras seguidas con un proceso sin responder antes de avisar
    /// (evita falsos positivos mientras una app arranca o carga).</summary>
    private const int FrozenSamples = 5;

    /// <summary>Claves: CPU, RAM, GPU, VRAM, Disco. GPU y VRAM exigen mas muestras para
    /// DEFCON porque al jugar o usar Ollama es normal ir al 95-100 % un buen rato.</summary>
    public static Dictionary<string, MetricThresholds> CreateDefaultThresholds() => new()
    {
        ["CPU"] = new(80, 95),
        ["RAM"] = new(85, 95),
        ["GPU"] = new(85, 98, DefconSamples: 10),
        ["VRAM"] = new(90, 98, DefconSamples: 10),
        ["Disco"] = new(90, 97, 1, 1, 1, 2)
    };

    /// <summary>Perfiles de sensibilidad: "relaxed", "normal" (por defecto) o "strict".</summary>
    public static Dictionary<string, MetricThresholds> CreateThresholds(string? profile) => profile switch
    {
        "relaxed" => new Dictionary<string, MetricThresholds>
        {
            ["CPU"] = new(90, 98),
            ["RAM"] = new(90, 97),
            ["GPU"] = new(92, 99, DefconSamples: 15),
            ["VRAM"] = new(95, 99, DefconSamples: 15),
            ["Disco"] = new(93, 98, 1, 1, 1, 2)
        },
        "strict" => new Dictionary<string, MetricThresholds>
        {
            ["CPU"] = new(70, 90),
            ["RAM"] = new(75, 90),
            ["GPU"] = new(80, 95, DefconSamples: 6),
            ["VRAM"] = new(85, 95, DefconSamples: 6),
            ["Disco"] = new(85, 95, 1, 1, 1, 2)
        },
        _ => CreateDefaultThresholds()
    };

    private sealed class MetricState
    {
        public StatusLevel Current;
        public int WarnRun, DefconRun, DownRun;
    }

    private Dictionary<string, MetricThresholds> _thresholds;
    private readonly Dictionary<string, MetricState> _states = new();
    private readonly Dictionary<string, int> _frozenRuns = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<StatusEvent> _history = new();

    public StatusEvaluator(Dictionary<string, MetricThresholds>? thresholds = null)
    {
        _thresholds = thresholds ?? CreateDefaultThresholds();
    }

    /// <summary>Cambia los umbrales en caliente (p.ej. al cambiar el perfil en Configuracion).
    /// Los estados actuales se conservan y se reevaluan en la siguiente lectura.</summary>
    public void SetThresholds(Dictionary<string, MetricThresholds> thresholds) => _thresholds = thresholds;

    /// <summary>Registro, el mas reciente primero (maximo 200 entradas).</summary>
    public IReadOnlyList<StatusEvent> History => _history;

    public StatusLevel Global { get; private set; }

    /// <summary>Se dispara por cada entrada nueva del registro.</summary>
    public event Action<StatusEvent>? EventRaised;

    public StatusReport Evaluate(SystemSnapshot snap, IEnumerable<string>? frozenProcesses = null)
    {
        var metrics = new Dictionary<string, StatusLevel>
        {
            ["CPU"] = CheckMetric("CPU", "CPU", snap.CpuPercent),
            ["RAM"] = CheckMetric("RAM", "RAM", snap.RamPercent),
            ["GPU"] = CheckMetric("GPU", "GPU", snap.GpuPercent)
        };

        if (snap.VramTotalBytes > 0)
            metrics["VRAM"] = CheckMetric("VRAM", "VRAM", snap.VramPercent);

        foreach (var drive in snap.Drives)
            metrics[$"Disco {drive.Name}"] = CheckMetric("Disco", $"Disco {drive.Name}", drive.UsedPercent);

        metrics["Procesos"] = CheckFrozen(frozenProcesses);

        Global = metrics.Values.Max();
        return new StatusReport(Global, metrics);
    }

    /// <summary>Anade una entrada al registro desde fuera (Uplink, Visor de eventos...).
    /// Los Warning y Defcon tambien se guardan en el log de la app.</summary>
    public void Report(StatusLevel level, string source, string message, bool isUplink = false, DateTime? time = null)
    {
        var ev = new StatusEvent(time ?? DateTime.Now, level, source, message, isUplink);

        _history.Insert(0, ev);
        if (_history.Count > MaxHistory)
            _history.RemoveRange(MaxHistory, _history.Count - MaxHistory);

        if (level != StatusLevel.Online)
            LogService.Info($"[{ev.Tag}] {source}: {message}");

        EventRaised?.Invoke(ev);
    }

    // ===================== Metricas con histeresis =====================

    private StatusLevel CheckMetric(string thresholdKey, string label, double value)
    {
        var t = _thresholds[thresholdKey];
        if (!_states.TryGetValue(label, out var s))
            _states[label] = s = new MetricState();

        var before = s.Current;

        s.WarnRun = value >= t.Warn ? s.WarnRun + 1 : 0;
        s.DefconRun = value >= t.Defcon ? s.DefconRun + 1 : 0;

        // Subir: DEFCON tiene prioridad y exige su racha; WARNING solo desde Online.
        if (s.DefconRun >= t.DefconSamples)
            s.Current = StatusLevel.Defcon;
        else if (s.WarnRun >= t.WarnSamples && s.Current == StatusLevel.Online)
            s.Current = StatusLevel.Warning;

        // Bajar: el valor debe caer Margin puntos bajo el umbral del nivel actual,
        // durante CalmSamples muestras seguidas.
        if (s.Current != StatusLevel.Online)
        {
            var floor = s.Current == StatusLevel.Defcon ? t.Defcon : t.Warn;
            s.DownRun = value < floor - t.Margin ? s.DownRun + 1 : 0;

            if (s.DownRun >= t.CalmSamples)
            {
                s.Current = s.Current == StatusLevel.Defcon && value >= t.Warn
                    ? StatusLevel.Warning
                    : StatusLevel.Online;
                s.DownRun = 0;
            }
        }
        else
        {
            s.DownRun = 0;
        }

        if (s.Current != before)
        {
            var message = s.Current switch
            {
                StatusLevel.Defcon => $"{label} {value:0}% sostenido",
                StatusLevel.Warning => before == StatusLevel.Defcon
                    ? $"{label} baja a {value:0}%"
                    : $"{label} alto: {value:0}%",
                _ => $"{label} normalizado: {value:0}%"
            };
            Report(s.Current, label, message);
        }

        return s.Current;
    }

    // ===================== Procesos congelados =====================

    private StatusLevel CheckFrozen(IEnumerable<string>? frozenProcesses)
    {
        var frozen = new HashSet<string>(frozenProcesses ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var name in frozen)
        {
            var run = _frozenRuns.GetValueOrDefault(name) + 1;
            _frozenRuns[name] = run;

            if (run == FrozenSamples)
                Report(StatusLevel.Warning, name, $"{name} no responde");
        }

        foreach (var name in _frozenRuns.Keys.Where(k => !frozen.Contains(k)).ToList())
        {
            if (_frozenRuns[name] >= FrozenSamples)
                Report(StatusLevel.Online, name, $"{name} responde de nuevo");

            _frozenRuns.Remove(name);
        }

        return _frozenRuns.Values.Any(r => r >= FrozenSamples) ? StatusLevel.Warning : StatusLevel.Online;
    }
}
