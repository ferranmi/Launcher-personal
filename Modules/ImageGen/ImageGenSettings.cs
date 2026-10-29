using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.ImageGen;

/// <summary>Carpetas configurables. Gallery es la de las imagenes generadas por el Launcher;
/// las demas son carpetas de una instalacion de A1111.</summary>
public enum SdFolder { Models, Loras, Vae, Embeddings, Extensions, Outputs, ControlNet, Gallery }

/// <summary>
/// Ajustes propios del modulo, en %AppData%\Launcher\imagegen.json. Se guardan
/// aparte de AppSettingsService para no tocar ese archivo.
/// </summary>
public class ImageGenSettings
{
    private static readonly string SettingsPath = Path.Combine(AppSettingsService.AppFolder, "imagegen.json");

    /// <summary>Carpeta por defecto de las imagenes generadas por el Launcher.</summary>
    public static readonly string DefaultGalleryFolder =
        Path.Combine(AppSettingsService.AppFolder, "imagegen", "outputs");

    private static ImageGenSettings? _instance;
    public static ImageGenSettings Instance => _instance ??= Load();

    /// <summary>Direccion del servidor A1111 (por defecto el puerto estandar).</summary>
    public string Url { get; set; } = "http://127.0.0.1:7860";

    /// <summary>Carpeta raiz de la instalacion de A1111 (donde esta webui-user.bat).</summary>
    public string? InstallPath { get; set; }

    /// <summary>Ruta suelta al .bat; solo se usa si InstallPath no lo contiene.</summary>
    public string? ServerBatPath { get; set; }

    /// <summary>Arrancar A1111 solo al abrir el modulo si esta apagado.</summary>
    public bool AutoStartServer { get; set; }

    /// <summary>Carpeta de wildcards (null = la del Launcher). Puede apuntar a la de Dynamic Prompts.</summary>
    public string? WildcardsFolder { get; set; }

    /// <summary>Carpetas elegidas a mano por el usuario (clave = nombre de SdFolder).
    /// Tienen prioridad sobre lo que informa el servidor y sobre las rutas estandar.</summary>
    public Dictionary<string, string> CustomFolders { get; set; } = new();

    // Carpetas reales que A1111 informa en /sdapi/v1/cmd-flags (si se lanzo con --ckpt-dir, etc.).
    // Se guardan para que "abrir carpeta" funcione tambien con el servidor apagado.
    public string? CkptDir { get; set; }
    public string? LoraDir { get; set; }
    public string? VaeDir { get; set; }
    public string? EmbeddingsDir { get; set; }
    public string? ControlNetDir { get; set; }

    /// <summary>Parametros de la ultima generacion: se restauran al abrir el modulo.</summary>
    public ImageGenParams? LastParams { get; set; }

    /// <summary>El webui-user.bat a lanzar: el de la instalacion si existe, o la ruta suelta.</summary>
    [JsonIgnore]
    public string? EffectiveBatPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(InstallPath))
            {
                var candidate = Path.Combine(InstallPath, "webui-user.bat");
                if (File.Exists(candidate)) return candidate;
            }

            return ServerBatPath;
        }
    }

    /// <summary>Ruta de una carpeta. Prioridad: la elegida a mano, la que informo el servidor
    /// (--ckpt-dir...) y la estandar bajo la carpeta de instalacion. installOverride permite
    /// consultar una instalacion aun sin guardar (ventana de ajustes); ignoreCustom devuelve
    /// lo que se usaria SIN la ruta manual (para mostrarlo como sugerencia).</summary>
    public string? FolderFor(SdFolder kind, string? installOverride = null, bool ignoreCustom = false)
    {
        if (!ignoreCustom &&
            CustomFolders.TryGetValue(kind.ToString(), out var custom) &&
            !string.IsNullOrWhiteSpace(custom))
        {
            return custom;
        }

        if (kind == SdFolder.Gallery) return DefaultGalleryFolder;

        var install = string.IsNullOrWhiteSpace(installOverride) ? InstallPath : installOverride;
        var useServerDirs = installOverride == null ||
                            string.Equals(installOverride.TrimEnd('\\'), InstallPath?.TrimEnd('\\'),
                                StringComparison.OrdinalIgnoreCase);

        if (useServerDirs)
        {
            var explicitDir = kind switch
            {
                SdFolder.Models => CkptDir,
                SdFolder.Loras => LoraDir,
                SdFolder.Vae => VaeDir,
                SdFolder.Embeddings => EmbeddingsDir,
                SdFolder.ControlNet => ControlNetDir,
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(explicitDir)) return explicitDir;
        }

        if (string.IsNullOrWhiteSpace(install)) return null;

        var relative = kind switch
        {
            SdFolder.Models => @"models\Stable-diffusion",
            SdFolder.Loras => @"models\Lora",
            SdFolder.Vae => @"models\VAE",
            SdFolder.Embeddings => "embeddings",
            SdFolder.Extensions => "extensions",
            SdFolder.Outputs => "outputs",
            _ => @"models\ControlNet"
        };

        return Path.Combine(install, relative);
    }

    /// <summary>Aprende de A1111 en marcha: si aun no hay carpeta de instalacion la toma de
    /// data_dir (por defecto es la raiz de la instalacion) y guarda las carpetas personalizadas.</summary>
    public void ApplyServerFlags(Dictionary<string, string> flags)
    {
        if (flags.Count == 0) return;

        var changed = false;

        if (string.IsNullOrWhiteSpace(InstallPath) &&
            flags.TryGetValue("data_dir", out var dataDir) && Directory.Exists(dataDir))
        {
            InstallPath = dataDir;
            changed = true;
        }

        changed |= Update(CkptDir, flags.GetValueOrDefault("ckpt_dir"), v => CkptDir = v);
        changed |= Update(LoraDir, flags.GetValueOrDefault("lora_dir"), v => LoraDir = v);
        changed |= Update(VaeDir, flags.GetValueOrDefault("vae_dir"), v => VaeDir = v);
        changed |= Update(EmbeddingsDir, flags.GetValueOrDefault("embeddings_dir"), v => EmbeddingsDir = v);
        changed |= Update(ControlNetDir, flags.GetValueOrDefault("controlnet_dir"), v => ControlNetDir = v);

        if (changed) Save();
    }

    private static bool Update(string? current, string? value, Action<string?> set)
    {
        if (string.Equals(current, value, StringComparison.Ordinal)) return false;
        set(value);
        return true;
    }

    private static ImageGenSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<ImageGenSettings>(File.ReadAllText(SettingsPath));
                if (loaded != null) return loaded;
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer imagegen.json", ex);
        }

        return new ImageGenSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppSettingsService.AppFolder);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo guardar imagegen.json", ex);
        }
    }
}
