using System.IO;
using System.Text.Json;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

/// <summary>
/// Ajustes del modulo Modelos, en %AppData%\Launcher\models.json (aparte de AppSettingsService,
/// igual que hace Imagenes IA). Si una lista de carpetas esta vacia se usa la ruta estandar
/// bajo la carpeta de instalacion de A1111; si tiene entradas, mandan esas.
/// </summary>
public class ModelsSettings
{
    private static readonly string SettingsPath = Path.Combine(AppSettingsService.AppFolder, "models.json");

    private static ModelsSettings? _instance;
    public static ModelsSettings Instance => _instance ??= Load();

    /// <summary>Carpeta raiz de la instalacion de AUTOMATIC1111.</summary>
    public string? InstallPath { get; set; }

    public List<string> LoraFolders { get; set; } = new();
    public List<string> CheckpointFolders { get; set; } = new();
    public List<string> LyCorisFolders { get; set; } = new();
    public List<string> WildcardFolders { get; set; } = new();

    // ----- Descargas -----
    public int MaxConcurrentDownloads { get; set; } = 2;

    /// <summary>Sube o baja el numero de descargas simultaneas segun la velocidad total conseguida (hasta el maximo).</summary>
    public bool AutoConcurrency { get; set; } = true;

    /// <summary>Limite global de velocidad en KB/s (0 = sin limite).</summary>
    public int SpeedLimitKBps { get; set; }

    public int DownloadRetries { get; set; } = 3;

    /// <summary>Categorias (subcarpetas) para organizar las descargas. Editables.</summary>
    public List<string> Categories { get; set; } = new() { "Personajes", "Poses", "Acciones", "Ropa", "Estilos", "Otros" };

    /// <summary>Nombres de referencia que el usuario ha decidido ignorar (no se avisa de que faltan).</summary>
    public List<string> IgnoredReferences { get; set; } = new();

    /// <summary>Correcciones de referencias preparadas y pendientes de revisar/aplicar.</summary>
    public List<PendingCorrection> PendingCorrections { get; set; } = new();

    /// <summary>Ultima vista de la Biblioteca: "cards" o "list".</summary>
    public string LibraryView { get; set; } = "cards";

    /// <summary>Indice del criterio de orden de la Biblioteca.</summary>
    public int LibrarySort { get; set; }

    public IReadOnlyList<string> FoldersFor(ModelType type)
    {
        var custom = type switch
        {
            ModelType.Lora => LoraFolders,
            ModelType.Checkpoint => CheckpointFolders,
            _ => LyCorisFolders
        };

        var list = custom.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (list.Count > 0) return list;

        if (string.IsNullOrWhiteSpace(InstallPath)) return Array.Empty<string>();

        var relative = type switch
        {
            ModelType.Lora => @"models\Lora",
            ModelType.Checkpoint => @"models\Stable-diffusion",
            _ => @"models\LyCORIS"
        };

        return new[] { Path.Combine(InstallPath, relative) };
    }

    public IReadOnlyList<string> WildcardFoldersEffective()
    {
        var list = WildcardFolders.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (list.Count > 0) return list;

        if (string.IsNullOrWhiteSpace(InstallPath)) return Array.Empty<string>();

        return new[] { Path.Combine(InstallPath, @"extensions\sd-dynamic-prompts\wildcards") };
    }

    public void AddPending(string oldName, string newName, string source)
    {
        PendingCorrections.RemoveAll(p => string.Equals(p.OldName, oldName, StringComparison.OrdinalIgnoreCase));
        PendingCorrections.Add(new PendingCorrection { OldName = oldName, NewName = newName, Source = source });
        Save();
        ModelsEvents.RaisePendingChanged();
    }

    public void RemovePending(IEnumerable<PendingCorrection> corrections)
    {
        var keys = corrections.Select(c => (c.OldName.ToLowerInvariant(), c.NewName)).ToHashSet();
        PendingCorrections.RemoveAll(p => keys.Contains((p.OldName.ToLowerInvariant(), p.NewName)));
        Save();
        ModelsEvents.RaisePendingChanged();
    }

    private static ModelsSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<ModelsSettings>(File.ReadAllText(SettingsPath));
                if (loaded != null)
                {
                    loaded.LoraFolders ??= new List<string>();
                    loaded.CheckpointFolders ??= new List<string>();
                    loaded.LyCorisFolders ??= new List<string>();
                    loaded.WildcardFolders ??= new List<string>();
                    loaded.Categories ??= new List<string> { "Personajes", "Poses", "Acciones", "Ropa", "Estilos", "Otros" };
                    loaded.IgnoredReferences ??= new List<string>();
                    loaded.PendingCorrections ??= new List<PendingCorrection>();
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer models.json", ex);
        }

        return new ModelsSettings();
    }

    public void Save() => TrySave();

    public bool TrySave()
    {
        var tempPath = SettingsPath + ".tmp";
        try
        {
            Directory.CreateDirectory(AppSettingsService.AppFolder);
            File.WriteAllText(tempPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tempPath, SettingsPath, true);
            return true;
        }
        catch (Exception ex)
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            LogService.Error("No se pudo guardar models.json", ex);
            return false;
        }
    }
}
