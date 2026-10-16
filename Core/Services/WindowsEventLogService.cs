using System.Diagnostics.Eventing.Reader;
using Launcher.Core.Models;

namespace Launcher.Core.Services;

public sealed record WindowsEventItem(DateTime Time, StatusLevel Level, string Source, string Message);

/// <summary>
/// Lee errores recientes del Visor de eventos (registros System y Application, que se
/// pueden leer sin ser administrador; Security no se toca). Solo mira Critico y Error,
/// descarta origenes ruidosos conocidos, agrupa repeticiones y devuelve como maximo unas
/// pocas entradas por lectura para no inundar el registro del dashboard.
///
/// ReadNew() hace E/S: llamarlo SIEMPRE desde un hilo de fondo (lo hace SystemStatusHost).
/// </summary>
public sealed class WindowsEventLogService
{
    private static readonly string[] Logs = { "System", "Application" };

    /// <summary>Un Error de estos origenes cuenta como grave (DEFCON): disco, GPU, energia, hardware.</summary>
    private static readonly HashSet<string> SevereProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "disk", "Ntfs", "volmgr", "nvlddmkm", "Display", "storahci", "stornvme",
        "Microsoft-Windows-Kernel-Power", "Microsoft-Windows-WHEA-Logger"
    };

    /// <summary>Origenes que generan errores "normales" a todas horas en Windows.</summary>
    private static readonly HashSet<string> IgnoredProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft-Windows-DistributedCOM", "DCOM", "Microsoft-Windows-WMI",
        "Microsoft-Windows-Perflib", "Microsoft-Windows-Time-Service", "Microsoft-Windows-GroupPolicy"
    };

    private const int MaxPerRun = 5;

    private readonly HashSet<string> _seen = new();
    private bool _firstRun = true;

    public List<WindowsEventItem> ReadNew()
    {
        // La primera lectura solo mira los ultimos 10 min para no volcar el historial al arrancar.
        var lookbackMinutes = _firstRun ? 10 : 15;
        _firstRun = false;

        var found = new List<(DateTime Time, int Level, string Provider, int Id, string Message)>();

        foreach (var log in Logs)
        {
            try
            {
                var xpath = $"*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) <= {lookbackMinutes * 60_000}]]]";
                var query = new EventLogQuery(log, PathType.LogName, xpath) { ReverseDirection = true };

                using var reader = new EventLogReader(query);

                var read = 0;
                EventRecord? record;
                while (read < 200 && (record = reader.ReadEvent()) != null)
                {
                    read++;
                    using (record)
                    {
                        var provider = record.ProviderName ?? "?";
                        if (IgnoredProviders.Contains(provider)) continue;

                        // Cada registro se reporta una sola vez, aunque las ventanas de lectura se solapen.
                        if (!_seen.Add($"{log}:{record.RecordId}")) continue;

                        found.Add((record.TimeCreated ?? DateTime.Now, record.Level ?? 2, provider, record.Id, Describe(record)));
                    }
                }
            }
            catch (Exception ex) when (ex is EventLogException or UnauthorizedAccessException)
            {
                LogService.Error($"No se pudo leer el registro de Windows '{log}'", ex);
            }
        }

        if (_seen.Count > 2000) _seen.Clear();

        return found
            .GroupBy(f => (f.Provider, f.Id))
            .Select(group =>
            {
                var newest = group.OrderByDescending(x => x.Time).First();
                var count = group.Count();
                var severe = newest.Level == 1 || SevereProviders.Contains(newest.Provider);
                var repeats = count > 1 ? $" x{count}" : "";

                return new WindowsEventItem(
                    newest.Time,
                    severe ? StatusLevel.Defcon : StatusLevel.Warning,
                    newest.Provider,
                    $"{newest.Provider} ({newest.Id}){repeats}: {newest.Message}");
            })
            .OrderByDescending(i => i.Time)
            .Take(MaxPerRun)
            .OrderBy(i => i.Time)
            .ToList();
    }

    private static string Describe(EventRecord record)
    {
        try
        {
            var text = record.FormatDescription();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var line = text.Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                return line.Length > 90 ? line[..90] + "..." : line;
            }
        }
        catch
        {
            // sin plantilla de mensaje para este origen: nos quedamos con el texto generico
        }

        return "sin descripcion";
    }
}
