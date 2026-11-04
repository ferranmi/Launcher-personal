using System.IO;
using System.Text.Json.Serialization;

namespace Launcher.Modules.Models;

// Formas de la API publica de Civitai (https://developer.civitai.com/site/reference/models.md). Solo se leen
// los campos que usa el Launcher; los desconocidos se ignoran.

public class CivitaiStats
{
    [JsonPropertyName("downloadCount")] public long DownloadCount { get; set; }
    [JsonPropertyName("thumbsUpCount")] public long ThumbsUpCount { get; set; }
}

public class CivitaiCreator
{
    [JsonPropertyName("username")] public string? Username { get; set; }
}

public class CivitaiHashes
{
    [JsonPropertyName("SHA256")] public string? Sha256 { get; set; }
    [JsonPropertyName("AutoV2")] public string? AutoV2 { get; set; }
}

public class CivitaiFileMeta
{
    [JsonPropertyName("format")] public string? Format { get; set; }
    [JsonPropertyName("size")] public string? Size { get; set; }
    [JsonPropertyName("fp")] public string? Fp { get; set; }
}

public class CivitaiFile
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("sizeKB")] public double SizeKB { get; set; }
    [JsonPropertyName("primary")] public bool Primary { get; set; }
    [JsonPropertyName("downloadUrl")] public string? DownloadUrl { get; set; }
    [JsonPropertyName("hashes")] public CivitaiHashes? Hashes { get; set; }
    [JsonPropertyName("metadata")] public CivitaiFileMeta? Metadata { get; set; }

    [JsonIgnore] public long SizeBytes => (long)(SizeKB * 1024);
    [JsonIgnore] public string SizeText => ModelEntry.FormatSize(SizeBytes);
    [JsonIgnore] public string FormatText => Metadata?.Format ?? Path.GetExtension(Name).TrimStart('.').ToUpperInvariant();
    [JsonIgnore] public string Display => $"{Name}  ({SizeText}, {FormatText})";
}

public class CivitaiImage
{
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
}

public class CivitaiVersion
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("modelId")] public long ModelId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("baseModel")] public string? BaseModel { get; set; }
    [JsonPropertyName("trainedWords")] public List<string> TrainedWords { get; set; } = new();
    [JsonPropertyName("publishedAt")] public DateTime? PublishedAt { get; set; }
    [JsonPropertyName("files")] public List<CivitaiFile> Files { get; set; } = new();
    [JsonPropertyName("images")] public List<CivitaiImage> Images { get; set; } = new();
    [JsonPropertyName("downloadUrl")] public string? DownloadUrl { get; set; }

    /// <summary>Fichero principal de la version (el marcado como primary, o el primero de tipo Model).</summary>
    [JsonIgnore]
    public CivitaiFile? PrimaryFile =>
        Files.FirstOrDefault(f => f.Primary) ??
        Files.FirstOrDefault(f => string.Equals(f.Type, "Model", StringComparison.OrdinalIgnoreCase)) ??
        Files.FirstOrDefault();

    [JsonIgnore] public string Display => string.IsNullOrEmpty(BaseModel) ? Name : $"{Name}  ·  {BaseModel}";
}

public class CivitaiModel
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("nsfw")] public bool Nsfw { get; set; }
    [JsonPropertyName("creator")] public CivitaiCreator? Creator { get; set; }
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = new();
    [JsonPropertyName("stats")] public CivitaiStats? Stats { get; set; }
    [JsonPropertyName("modelVersions")] public List<CivitaiVersion> ModelVersions { get; set; } = new();

    [JsonIgnore] public string CreatorName => Creator?.Username ?? "";
    [JsonIgnore] public string Url => $"https://civitai.com/models/{Id}";

    [JsonIgnore]
    public string TypeLabel => Type switch
    {
        "LORA" => "LoRA",
        "LoCon" => "LyCORIS",
        "DoRA" => "DoRA",
        _ => Type ?? ""
    };

    /// <summary>Primera imagen (no video) de la version mas reciente, para la tarjeta.</summary>
    [JsonIgnore]
    public string? PreviewUrl => ModelVersions
        .SelectMany(v => v.Images)
        .FirstOrDefault(i => !string.IsNullOrEmpty(i.Url) &&
                             !string.Equals(i.Type, "video", StringComparison.OrdinalIgnoreCase) &&
                             !i.Url!.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))?.Url;

    [JsonIgnore] public string StatsText => Stats == null ? "" : $"{Stats.DownloadCount:N0} descargas  \u00b7  {Stats.ThumbsUpCount:N0} likes";
}

public class CivitaiPage<T>
{
    public List<T> Items { get; set; } = new();
    public string? NextCursor { get; set; }
}

public class CivitaiCollection
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("itemCount")] public int ItemCount { get; set; }
    [JsonPropertyName("user")] public CivitaiCreator? User { get; set; }

    [JsonIgnore] public string Url => $"https://civitai.com/collections/{Id}";
    [JsonIgnore] public string Subtitle => $"{Type}  \u00b7  {ItemCount} elementos  \u00b7  {User?.Username}";
}
