using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Core.Services;

namespace Launcher.Modules.Dashboard.Widgets;

public sealed class CalendarEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Dia del evento (solo la fecha).</summary>
    public DateTime Date { get; set; }

    /// <summary>"HH:mm", o vacio si dura todo el dia.</summary>
    public string Time { get; set; } = "";

    public string Title { get; set; } = "";

    /// <summary>Momento exacto del evento (los de todo el dia cuentan desde las 00:00).</summary>
    [JsonIgnore]
    public DateTime When =>
        Date.Date + (TimeSpan.TryParseExact(Time, "hh\\:mm", CultureInfo.InvariantCulture, out var t) ? t : TimeSpan.Zero);
}

/// <summary>Eventos del calendario, compartidos por todos los widgets que los muestren. Se guardan en
/// %AppData%\Launcher\events.json.</summary>
public sealed class CalendarEventsService
{
    // Debe ir antes que Instance: los campos estaticos se inicializan en orden.
    private static readonly string FilePath = Path.Combine(AppSettingsService.AppFolder, "events.json");

    public static CalendarEventsService Instance { get; } = new();

    private readonly List<CalendarEvent> _events = new();

    /// <summary>Se dispara tras anadir o borrar un evento.</summary>
    public event EventHandler? Changed;

    private CalendarEventsService() => Load();

    /// <summary>Eventos de un dia: primero los de todo el dia y luego por hora.</summary>
    public List<CalendarEvent> On(DateTime day) =>
        _events.Where(e => e.Date.Date == day.Date)
            .OrderBy(e => e.Time.Length == 0 ? "" : e.Time)
            .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public bool HasEvents(DateTime day) => _events.Any(e => e.Date.Date == day.Date);

    /// <summary>El primer evento que todavia no ha pasado (los de todo el dia valen durante su dia entero).</summary>
    public CalendarEvent? Next(DateTime from) =>
        _events
            .Where(e => e.Time.Length == 0 ? e.Date.Date >= from.Date : e.When >= from)
            .OrderBy(e => e.When)
            .FirstOrDefault();

    public void Add(DateTime day, string time, string title)
    {
        _events.Add(new CalendarEvent { Date = day.Date, Time = time, Title = title });
        Commit();
    }

    public void Remove(string id)
    {
        if (_events.RemoveAll(e => e.Id == id) > 0) Commit();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var items = JsonSerializer.Deserialize<List<CalendarEvent>>(File.ReadAllText(FilePath));
            if (items != null) _events.AddRange(items);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudieron leer los eventos del calendario", ex);
        }
    }

    private void Commit()
    {
        try
        {
            Directory.CreateDirectory(AppSettingsService.AppFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_events, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudieron guardar los eventos del calendario", ex);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
