using System.IO;
using System.Text.Json;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

public class ModelInfo
{
    public string Name { get; set; } = "";
    public long CivitaiModelId { get; set; }
    public long CivitaiVersionId { get; set; }
    public List<string> TrainedWords { get; set; } = new();
}

/// <summary>Datos de Civitai de los modelos descargados desde el Launcher (palabras de activacion, ids), en
/// models-info.json. Es solo una ayuda para construir lineas; si se borra no se pierde ningun modelo.</summary>
public sealed class ModelInfoStore
{
    public static ModelInfoStore Instance { get; } = new();

    private static readonly string Path_ = System.IO.Path.Combine(AppSettingsService.AppFolder, "models-info.json");
    private readonly Dictionary<string, ModelInfo> _byName = new(StringComparer.OrdinalIgnoreCase);

    private ModelInfoStore()
    {
        try
        {
            if (!File.Exists(Path_)) return;
            var items = JsonSerializer.Deserialize<List<ModelInfo>>(File.ReadAllText(Path_)) ?? new List<ModelInfo>();
            foreach (var item in items) _byName[item.Name] = item;
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer models-info.json", ex);
        }
    }

    public ModelInfo? Get(string name) => _byName.GetValueOrDefault(name);

    public void Set(ModelInfo info)
    {
        _byName[info.Name] = info;

        try
        {
            Directory.CreateDirectory(AppSettingsService.AppFolder);
            File.WriteAllText(Path_, JsonSerializer.Serialize(_byName.Values.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo guardar models-info.json", ex);
        }
    }
}

/// <summary>Construye la linea de uso de un modelo con la prioridad acordada: 1) la linea que ya exista en los
/// wildcards, 2) los datos de Civitai guardados al descargar, 3) solo la etiqueta (se completa a mano).</summary>
public static class LineInfoService
{
    public static async Task<string> BuildLineAsync(string modelName, string weight)
    {
        try
        {
            var index = await WildcardService.Instance.GetReferenceIndexAsync();
            if (index.TryGetValue(modelName, out var locations))
            {
                foreach (var location in locations)
                {
                    var lines = await Task.Run(() => WildcardService.ReadLines(location.FullPath));
                    if (location.LineNumber >= 1 && location.LineNumber <= lines.Length &&
                        ModelLine.Parse(lines[location.LineNumber - 1]) != null)
                    {
                        return lines[location.LineNumber - 1].Trim();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo recuperar la linea existente", ex);
        }

        var info = ModelInfoStore.Instance.Get(modelName);
        var tag = $"<lora:{modelName}:{weight}>";

        return info is { TrainedWords.Count: > 0 }
            ? tag + ", " + string.Join(", ", info.TrainedWords)
            : tag;
    }
}
