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
/// wildcards, 2) los datos de Civitai guardados al descargar, 3) solo la etiqueta (se completa a mano).
/// Una linea existente solo se reutiliza si contiene UNA unica etiqueta de modelo: asi nunca se toma una linea
/// de pareja o trio (varias etiquetas) como si fuera la del personaje.</summary>
public static class LineInfoService
{
    private static readonly System.Text.RegularExpressions.Regex AnyTag = new(
        @"<(?:lora|lyco):[^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static bool IsSingleModelLine(string line) =>
        ModelLine.Parse(line) != null && AnyTag.Matches(line).Count == 1;

    private static string Fallback(string modelName, string weight)
    {
        var info = ModelInfoStore.Instance.Get(modelName);
        var tag = $"<lora:{modelName}:{weight}>";

        return info is { TrainedWords.Count: > 0 }
            ? tag + ", " + string.Join(", ", info.TrainedWords)
            : tag;
    }

    public static async Task<string> BuildLineAsync(string modelName, string weight)
    {
        var all = await BuildLinesAsync(new[] { modelName }, weight);
        return all.TryGetValue(modelName, out var line) ? line : Fallback(modelName, weight);
    }

    /// <summary>Lineas de varios modelos leyendo cada archivo de wildcards una sola vez.</summary>
    public static async Task<Dictionary<string, string>> BuildLinesAsync(IEnumerable<string> names, string weight)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, List<WildcardRefLocation>> index;
        try
        {
            index = await WildcardService.Instance.GetReferenceIndexAsync();
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer el indice de referencias", ex);
            index = new Dictionary<string, List<WildcardRefLocation>>(StringComparer.OrdinalIgnoreCase);
        }

        var cache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string? line = null;

            if (index.TryGetValue(name, out var locations))
            {
                foreach (var location in locations)
                {
                    if (!cache.TryGetValue(location.FullPath, out var lines))
                    {
                        try { lines = await Task.Run(() => WildcardService.ReadLines(location.FullPath)); }
                        catch { lines = Array.Empty<string>(); }
                        cache[location.FullPath] = lines;
                    }

                    if (location.LineNumber >= 1 && location.LineNumber <= lines.Length &&
                        IsSingleModelLine(lines[location.LineNumber - 1]))
                    {
                        line = lines[location.LineNumber - 1].Trim();
                        break;
                    }
                }
            }

            result[name] = line ?? Fallback(name, weight);
        }

        return result;
    }
}
