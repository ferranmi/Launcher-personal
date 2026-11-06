using System.IO;
using System.Text.Json;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

public class ActivityEntry
{
    public DateTime Time { get; set; } = DateTime.Now;

    /// <summary>"descarga", "renombrado", "wildcard", "referencias", "combinaciones", "biblioteca"...</summary>
    public string Kind { get; set; } = "";

    public string Text { get; set; } = "";

    /// <summary>0 = normal, 1 = bien, 2 = aviso, 3 = incidencia.</summary>
    public int Level { get; set; }
}

/// <summary>Registro de las ultimas operaciones del modulo (descargas, renombrados, cambios en wildcards, incidencias...)
/// para el dashboard y los widgets. Se guarda en models-activity.json (ultimas 200).</summary>
public static class ActivityLog
{
    private const int MaxEntries = 200;

    private static readonly string FilePath = Path.Combine(AppSettingsService.AppFolder, "models-activity.json");
    private static readonly object Gate = new();
    private static readonly List<ActivityEntry> Items = new();

    public static event Action? Added;

    static ActivityLog()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var loaded = JsonSerializer.Deserialize<List<ActivityEntry>>(File.ReadAllText(FilePath));
            if (loaded != null) Items.AddRange(loaded.OrderByDescending(e => e.Time).Take(MaxEntries));
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer models-activity.json", ex);
        }
    }

    public static void Add(string kind, string text, int level = 0)
    {
        lock (Gate)
        {
            Items.Insert(0, new ActivityEntry { Kind = kind, Text = text, Level = level });
            if (Items.Count > MaxEntries) Items.RemoveRange(MaxEntries, Items.Count - MaxEntries);

            try
            {
                Directory.CreateDirectory(AppSettingsService.AppFolder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(Items));
            }
            catch (Exception ex)
            {
                LogService.Error("No se pudo guardar models-activity.json", ex);
            }
        }

        Added?.Invoke();
    }

    public static List<ActivityEntry> Recent(int count)
    {
        lock (Gate) return Items.Take(count).ToList();
    }
}
