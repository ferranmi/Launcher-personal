using System.IO;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

public record ModelsSnapshot(
    int Total, int Lora, int Checkpoints, int LyCoris, int Duplicates,
    int DownloadsActive, int DownloadsQueued, int DownloadsPaused, int DownloadsCompleted, int DownloadsFailed,
    double DownloadSpeed, int PendingCorrections, DateTime? LastScan);

public record ReferenceSummary(int Missing, int Ambiguous, int Total, DateTime At);

public record EnvItem(string Label, bool? Ok, string Detail);

/// <summary>Cifras compartidas por el Dashboard del modulo y los widgets del dashboard del Launcher, para que
/// ambos muestren siempre lo mismo y la logica no se repita.</summary>
public static class ModelsStats
{
    /// <summary>Ultimo analisis de referencias (null si aun no se ha hecho: no se inventan cifras).</summary>
    public static ReferenceSummary? References { get; private set; }

    /// <summary>Ultima prueba de conexion con Civitai.</summary>
    public static (bool Ok, string Message, DateTime At)? Connection { get; private set; }

    public static void SetReferences(int missing, int ambiguous, int total) =>
        References = new ReferenceSummary(missing, ambiguous, total, DateTime.Now);

    public static void SetConnection(bool ok, string message) => Connection = (ok, message, DateTime.Now);

    public static ModelsSnapshot Take()
    {
        var catalog = ModelCatalogService.Instance;
        var models = catalog.Models;
        var queue = DownloadQueueService.Instance.Items;

        return new ModelsSnapshot(
            models.Count,
            models.Count(m => m.Type == ModelType.Lora),
            models.Count(m => m.Type == ModelType.Checkpoint),
            models.Count(m => m.Type == ModelType.LyCoris),
            models.Count(m => m.IsDuplicateName),
            queue.Count(i => i.State == DownloadState.Downloading),
            queue.Count(i => i.State == DownloadState.Queued),
            queue.Count(i => i.State == DownloadState.Paused),
            queue.Count(i => i.State == DownloadState.Completed),
            queue.Count(i => i.State == DownloadState.Failed),
            queue.Where(i => i.State == DownloadState.Downloading).Sum(i => i.SpeedBps),
            ModelsSettings.Instance.PendingCorrections.Count,
            catalog.LastScan);
    }

    /// <summary>Estado de las rutas y de Civitai. Ok = null significa "sin datos / no probado".</summary>
    public static List<EnvItem> Environment()
    {
        var settings = ModelsSettings.Instance;
        var list = new List<EnvItem>();

        var install = settings.InstallPath;
        list.Add(string.IsNullOrWhiteSpace(install)
            ? new EnvItem("A1111", false, "sin configurar")
            : new EnvItem("A1111", Directory.Exists(install), Directory.Exists(install) ? install : "la carpeta no existe"));

        EnvItem Folders(string label, IReadOnlyList<string> folders)
        {
            if (folders.Count == 0) return new EnvItem(label, false, "sin configurar");

            var missing = folders.Count(f => !Directory.Exists(f));
            return missing == 0
                ? new EnvItem(label, true, folders.Count == 1 ? folders[0] : $"{folders.Count} carpetas")
                : new EnvItem(label, false, $"{missing} de {folders.Count} carpeta(s) no existen");
        }

        list.Add(Folders("LoRA", settings.FoldersFor(ModelType.Lora)));
        list.Add(Folders("Checkpoints", settings.FoldersFor(ModelType.Checkpoint)));
        list.Add(Folders("LyCORIS", settings.FoldersFor(ModelType.LyCoris)));
        list.Add(Folders("Wildcards", settings.WildcardFoldersEffective()));

        var hasKey = CivitaiService.Instance.HasApiKey;
        list.Add(new EnvItem("API Key", hasKey, hasKey ? "guardada" : "sin clave (las descargas daran 401)"));

        list.Add(Connection is { } c
            ? new EnvItem("Civitai", c.Ok, $"{c.Message} ({c.At:HH:mm})")
            : new EnvItem("Civitai", null, "sin probar"));

        return list;
    }

    /// <summary>Cruza las referencias de los wildcards con la biblioteca y guarda el resumen (solo lectura).</summary>
    public static async Task<bool> AnalyzeReferencesAsync()
    {
        var models = ModelCatalogService.Instance.Models;
        if (models.Count == 0) return false;

        var index = await WildcardService.Instance.GetReferenceIndexAsync(force: true);

        var counts = models
            .Where(m => m.Type != ModelType.Checkpoint)
            .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var ignored = new HashSet<string>(ModelsSettings.Instance.IgnoredReferences, StringComparer.OrdinalIgnoreCase);

        int missing = 0, ambiguous = 0;
        foreach (var name in index.Keys.Where(n => !ignored.Contains(n)))
        {
            var count = counts.GetValueOrDefault(name);
            if (count == 0) missing++;
            else if (count > 1) ambiguous++;
        }

        SetReferences(missing, ambiguous, index.Count);
        return true;
    }
}

/// <summary>Abre una pestana concreta del modulo Modelos desde fuera (widgets del dashboard, etiquetas...).
/// Si la vista aun no existe, la peticion queda pendiente y se atiende al crearla.</summary>
public static class ModelsNavigation
{
    private static (string Tab, string? Arg)? _pending;

    public static event Action? Requested;

    public static (string Tab, string? Arg)? TakePending()
    {
        var pending = _pending;
        _pending = null;
        return pending;
    }

    public static void Open(string tab, string? arg = null)
    {
        _pending = (tab, arg);
        Requested?.Invoke();
        Navigator.GoTo("models");
    }
}
