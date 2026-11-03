using System.IO;
using System.Text.Json.Serialization;

namespace Launcher.Modules.Models;

public enum ModelType { Lora, Checkpoint, LyCoris }

/// <summary>
/// Un modelo encontrado en disco. Es una "foto" del archivo: el disco es la fuente de verdad y el
/// indice (models-index.json) solo una cache que se puede borrar y reconstruir en cualquier momento.
/// El tipo sale de la carpeta configurada donde se encontro, no se mueve ningun archivo.
/// </summary>
public class ModelEntry
{
    /// <summary>Nombre del archivo sin extension: es lo que aparece en &lt;lora:NOMBRE:peso&gt;.</summary>
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";

    /// <summary>Carpeta configurada (raiz) en la que se encontro.</summary>
    public string RootFolder { get; set; } = "";

    /// <summary>Subcarpeta dentro de la raiz ("" si esta en la propia raiz).</summary>
    public string RelativeFolder { get; set; } = "";

    public ModelType Type { get; set; }
    public long SizeBytes { get; set; }
    public DateTime Modified { get; set; }

    /// <summary>Imagen de vista previa junto al modelo (nombre.preview.png, nombre.png...), si existe.</summary>
    public string? PreviewPath { get; set; }

    /// <summary>True si hay otro archivo con el mismo nombre (y el mismo "tipo de etiqueta"): una
    /// etiqueta &lt;lora:nombre&gt; seria ambigua. Lo calcula el catalogo, no se guarda en el indice.</summary>
    [JsonIgnore]
    public bool IsDuplicateName { get; set; }

    [JsonIgnore]
    public bool HasPreview => !string.IsNullOrEmpty(PreviewPath);

    [JsonIgnore]
    public string Initial => Name.Length == 0 ? "?" : Name[..1].ToUpperInvariant();

    [JsonIgnore]
    public string DisplayFolder => RelativeFolder.Length == 0 ? "(raiz)" : RelativeFolder;

    /// <summary>Primer nivel de subcarpeta ("Personajes", "Poses"...). Es solo una pista visual
    /// para filtrar: no obliga a mover nada.</summary>
    [JsonIgnore]
    public string Category
    {
        get
        {
            if (RelativeFolder.Length == 0) return "(raiz)";
            var i = RelativeFolder.IndexOfAny(new[] { '\\', '/' });
            return i < 0 ? RelativeFolder : RelativeFolder[..i];
        }
    }

    [JsonIgnore]
    public string TypeLabel => Type switch
    {
        ModelType.Lora => "LoRA",
        ModelType.Checkpoint => "Checkpoint",
        _ => "LyCORIS"
    };

    [JsonIgnore]
    public string SizeText => FormatSize(SizeBytes);

    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        var i = 0;
        while (size >= 1024 && i < units.Length - 1)
        {
            size /= 1024;
            i++;
        }
        return $"{size:0.#} {units[i]}";
    }

    public static string? FindPreview(string modelPath)
    {
        var basePath = Path.Combine(Path.GetDirectoryName(modelPath) ?? "", Path.GetFileNameWithoutExtension(modelPath));
        foreach (var suffix in new[] { ".preview.png", ".png", ".preview.jpg", ".jpg", ".jpeg" })
        {
            var candidate = basePath + suffix;
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
