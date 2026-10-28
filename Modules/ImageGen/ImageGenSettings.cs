using System.IO;
using System.Text.Json;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.ImageGen;

/// <summary>
/// Ajustes propios del modulo, en %AppData%\Launcher\imagegen.json. Se guardan
/// aparte de AppSettingsService para no tocar ese archivo.
/// </summary>
public class ImageGenSettings
{
    private static readonly string SettingsPath = Path.Combine(AppSettingsService.AppFolder, "imagegen.json");

    private static ImageGenSettings? _instance;
    public static ImageGenSettings Instance => _instance ??= Load();

    /// <summary>Direccion del servidor A1111 (por defecto el puerto estandar).</summary>
    public string Url { get; set; } = "http://127.0.0.1:7860";

    /// <summary>Ruta al webui-user.bat, para el boton "Iniciar servidor".</summary>
    public string? ServerBatPath { get; set; }

    /// <summary>Parametros de la ultima generacion: se restauran al abrir el modulo.</summary>
    public ImageGenParams? LastParams { get; set; }

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
