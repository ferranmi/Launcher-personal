using System.Net.Http;
using System.Text;
using System.Text.Json;
using Launcher.Core.Models;

namespace Launcher.Modules.ImageGen;

public enum SdServerState { Offline, NoApi, Ready }

public record SdModel(string Title, string Name);

public record SdProgress(
    double Fraction, double EtaSeconds, int Step, int TotalSteps,
    int Job, int JobCount, string? CurrentImageBase64);

/// <summary>Imagen devuelta por A1111 con el prompt realmente enviado (tras wildcards, etc.).</summary>
public record SdImageResult(byte[] Data, long Seed, string Prompt, string NegativePrompt);

/// <summary>Peticion txt2img en construccion: las extensiones la modifican antes de enviarla.
/// Body = campos normales; AlwaysOn = scripts alwayson_scripts ({"nombre": {"args": [...]}}).</summary>
public class SdRequest
{
    public Dictionary<string, object?> Body { get; }
    public Dictionary<string, object?> AlwaysOn { get; } = new();

    public SdRequest(Dictionary<string, object?> body) => Body = body;

    public string Prompt
    {
        get => Body["prompt"] as string ?? "";
        set => Body["prompt"] = value;
    }

    public string NegativePrompt
    {
        get => Body["negative_prompt"] as string ?? "";
        set => Body["negative_prompt"] = value;
    }

    public void AddScript(string name, params object?[] args) =>
        AlwaysOn[name] = new Dictionary<string, object?> { ["args"] = args };
}

/// <summary>
/// Cliente de la API de AUTOMATIC1111 (webui lanzado con --api). Mismo patron que
/// OllamaService: HttpClient estatico y sin estado salvo la URL base.
///
/// LoRA y LyCORIS: desde A1111 1.5 ambos los gestiona la extension Lora integrada
/// y salen en /sdapi/v1/loras; se invocan igual con &lt;lora:nombre:peso&gt;.
/// </summary>
public class StableDiffusionService
{
    // txt2img es una peticion bloqueante que puede tardar mucho (SDXL, batches...).
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };

    public string BaseUrl { get; set; } = "http://127.0.0.1:7860";

    // ===================== Estado =====================

    /// <summary>Offline = no responde nada; NoApi = el servidor esta pero sin --api.</summary>
    public async Task<SdServerState> CheckAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            using var response = await Http.GetAsync($"{BaseUrl}/sdapi/v1/samplers", cts.Token);
            return response.IsSuccessStatusCode ? SdServerState.Ready : SdServerState.NoApi;
        }
        catch
        {
            return SdServerState.Offline;
        }
    }

    // ===================== Listados =====================

    public async Task<List<SdModel>> GetModelsAsync()
    {
        using var doc = await GetDocAsync("/sdapi/v1/sd-models");
        return doc.RootElement.EnumerateArray()
            .Select(e => new SdModel(Str(e, "title"), Str(e, "model_name")))
            .Where(m => m.Title.Length > 0)
            .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Titulo del checkpoint cargado ahora mismo en el servidor.</summary>
    public async Task<string?> GetCurrentModelTitleAsync()
    {
        try
        {
            using var doc = await GetDocAsync("/sdapi/v1/options");
            return Str(doc.RootElement, "sd_model_checkpoint") is { Length: > 0 } t ? t : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<string>> GetSamplersAsync()
    {
        using var doc = await GetDocAsync("/sdapi/v1/samplers");
        return doc.RootElement.EnumerateArray().Select(e => Str(e, "name")).Where(n => n.Length > 0).ToList();
    }

    /// <summary>Solo existe en A1111 1.9+; en versiones anteriores devuelve lista vacia.</summary>
    public async Task<List<string>> GetSchedulersAsync()
    {
        try
        {
            using var doc = await GetDocAsync("/sdapi/v1/schedulers");
            return doc.RootElement.EnumerateArray().Select(e => Str(e, "name")).Where(n => n.Length > 0).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>LoRAs y LyCORIS disponibles (nombre de archivo sin extension).</summary>
    public async Task<List<string>> GetLorasAsync()
    {
        try
        {
            using var doc = await GetDocAsync("/sdapi/v1/loras");
            return doc.RootElement.EnumerateArray()
                .Select(e => Str(e, "name"))
                .Where(n => n.Length > 0)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>Pide al servidor que vuelva a escanear la carpeta de LoRAs.</summary>
    public async Task RefreshLorasAsync()
    {
        try
        {
            using var response = await Http.PostAsync($"{BaseUrl}/sdapi/v1/refresh-loras", new StringContent(""));
        }
        catch
        {
            // Si falla, simplemente se recarga la lista que ya habia.
        }
    }

    // ===================== Instalacion =====================

    /// <summary>Flags con los que se lanzo A1111 (/sdapi/v1/cmd-flags), solo los de tipo texto.
    /// Incluye data_dir (la raiz de la instalacion) y ckpt_dir/lora_dir/vae_dir si se personalizaron.
    /// Devuelve un diccionario vacio si el servidor esta apagado.</summary>
    public async Task<Dictionary<string, string>> GetCmdFlagsAsync()
    {
        var flags = new Dictionary<string, string>();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            using var doc = await GetDocAsync("/sdapi/v1/cmd-flags", cts.Token);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    flags[property.Name] = property.Value.GetString()!;
                }
            }
        }
        catch
        {
            // Servidor apagado o sin API: sin datos.
        }

        return flags;
    }

    // ===================== VAE / extensiones =====================

    public async Task<List<string>> GetVaesAsync()
    {
        try
        {
            using var doc = await GetDocAsync("/sdapi/v1/sd-vae");
            return doc.RootElement.EnumerateArray()
                .Select(e => Str(e, "model_name"))
                .Where(n => n.Length > 0)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>Nombres de los scripts alwayson instalados en A1111. Null si el
    /// servidor no expone /script-info (versiones antiguas): se asume "desconocido".</summary>
    public async Task<HashSet<string>?> GetAlwaysOnScriptsAsync()
    {
        try
        {
            using var doc = await GetDocAsync("/sdapi/v1/script-info");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.TryGetProperty("is_alwayson", out var alwaysOn) && alwaysOn.ValueKind == JsonValueKind.True)
                {
                    names.Add(Str(e, "name"));
                }
            }

            return names;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Lee una lista de strings de cualquier endpoint, ya sea un array directo
    /// o un objeto con la lista dentro de "property" (p.ej. /controlnet/model_list).</summary>
    public async Task<List<string>> GetStringListAsync(string path, string? property = null)
    {
        using var doc = await GetDocAsync(path);
        var root = doc.RootElement;

        if (property != null && root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out var inner))
        {
            root = inner;
        }

        if (root.ValueKind != JsonValueKind.Array) return new List<string>();

        return root.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? "")
            .Where(s => s.Length > 0)
            .ToList();
    }

    // ===================== Generacion =====================

    /// <summary>
    /// Genera con txt2img. Con perImage = false se envia UNA peticion con todo el lote
    /// (tandas x por tanda). Con perImage = true se envia una peticion por imagen, para que
    /// las extensiones (wildcards) den a cada una su propio prompt: A1111 solo acepta un
    /// prompt por peticion. Las seeds fijas se incrementan como hace A1111 dentro de un
    /// lote (seed, seed+1...). Cancelar (ct) evita lanzar las peticiones siguientes; la
    /// que esta en curso se corta con InterruptAsync.
    /// </summary>
    public async Task<List<SdImageResult>> Txt2ImgAsync(
        ImageGenParams p, Action<SdRequest>? customize = null, bool perImage = false,
        Action<int, int>? onImageStarted = null, CancellationToken ct = default)
    {
        if (!perImage) return await PostTxt2ImgAsync(p, customize);

        var total = Math.Max(1, p.BatchCount) * Math.Max(1, p.BatchSize);
        var results = new List<SdImageResult>();

        for (var i = 0; i < total && !ct.IsCancellationRequested; i++)
        {
            onImageStarted?.Invoke(i, total);

            var single = p.Clone();
            single.BatchCount = 1;
            single.BatchSize = 1;
            single.Seed = p.Seed >= 0 ? p.Seed + i : -1;

            // customize se invoca en cada peticion: los wildcards se resuelven de nuevo.
            results.AddRange(await PostTxt2ImgAsync(single, customize));
        }

        return results;
    }

    private async Task<List<SdImageResult>> PostTxt2ImgAsync(
        ImageGenParams p, Action<SdRequest>? customize, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["prompt"] = p.Prompt,
            ["negative_prompt"] = p.NegativePrompt,
            ["steps"] = p.Steps,
            ["sampler_name"] = p.Sampler,
            ["cfg_scale"] = p.CfgScale,
            ["width"] = p.Width,
            ["height"] = p.Height,
            ["seed"] = p.Seed,
            ["n_iter"] = p.BatchCount,
            ["batch_size"] = p.BatchSize,
            ["send_images"] = true,
            ["save_images"] = false // la galeria del Launcher es la fuente de verdad
        };

        if (!string.IsNullOrWhiteSpace(p.Scheduler)) body["scheduler"] = p.Scheduler;

        // Clip skip y VAE son ajustes globales de A1111: se envian siempre como override.
        var overrides = new Dictionary<string, object>
        {
            ["CLIP_stop_at_last_layers"] = Math.Max(1, p.ClipSkip),
            ["sd_vae"] = string.IsNullOrWhiteSpace(p.Vae) ? "Automatic" : p.Vae
        };
        body["override_settings"] = overrides;
        body["override_settings_restore_afterwards"] = false;

        if (!string.IsNullOrWhiteSpace(p.ModelTitle))
        {
            // Cambia de checkpoint solo si hace falta y lo deja cargado despues.
            overrides["sd_model_checkpoint"] = p.ModelTitle;
            body["override_settings_restore_afterwards"] = false;
        }

        var request = new SdRequest(body);
        customize?.Invoke(request);
        if (request.AlwaysOn.Count > 0) body["alwayson_scripts"] = request.AlwaysOn;

        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync($"{BaseUrl}/sdapi/v1/txt2img", content, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var snippet = text.Length > 300 ? text[..300] : text;
            throw new InvalidOperationException($"A1111 respondio {(int)response.StatusCode}: {snippet}");
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        var images = new List<byte[]>();
        if (root.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array)
        {
            foreach (var img in imgs.EnumerateArray())
            {
                var b64 = img.GetString();
                if (!string.IsNullOrEmpty(b64)) images.Add(Convert.FromBase64String(b64));
            }
        }

        var seeds = ParseSeeds(root);

        // Con varias imagenes, A1111 puede anteponer la rejilla (grid) al resto:
        // si sobran imagenes respecto a las seeds, descartamos las primeras.
        if (seeds.Count > 0 && images.Count > seeds.Count)
        {
            images = images.Skip(images.Count - seeds.Count).ToList();
        }

        var results = new List<SdImageResult>();
        for (var i = 0; i < images.Count; i++)
        {
            var seed = i < seeds.Count ? seeds[i] : (p.Seed >= 0 ? p.Seed + i : 0);
            results.Add(new SdImageResult(
                images[i], seed,
                body["prompt"] as string ?? p.Prompt,
                body["negative_prompt"] as string ?? p.NegativePrompt));
        }

        return results;
    }

    /// <summary>"info" llega como un string que contiene otro JSON con all_seeds.</summary>
    private static List<long> ParseSeeds(JsonElement root)
    {
        var seeds = new List<long>();
        try
        {
            if (root.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.String)
            {
                using var infoDoc = JsonDocument.Parse(info.GetString() ?? "{}");
                if (infoDoc.RootElement.TryGetProperty("all_seeds", out var all) && all.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in all.EnumerateArray())
                    {
                        if (s.ValueKind == JsonValueKind.Number) seeds.Add(s.GetInt64());
                    }
                }
            }
        }
        catch
        {
            // Sin seeds fiables: el llamador cae a seed pedida + indice.
        }

        return seeds;
    }

    public async Task<SdProgress?> GetProgressAsync(CancellationToken ct)
    {
        try
        {
            using var doc = await GetDocAsync("/sdapi/v1/progress?skip_current_image=false", ct);
            var root = doc.RootElement;

            var step = 0; var total = 0; var job = 0; var jobCount = 0;
            if (root.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.Object)
            {
                step = (int)Num(state, "sampling_step");
                total = (int)Num(state, "sampling_steps");
                job = (int)Num(state, "job_no");
                jobCount = (int)Num(state, "job_count");
            }

            string? current = null;
            if (root.TryGetProperty("current_image", out var ci) && ci.ValueKind == JsonValueKind.String)
            {
                current = ci.GetString();
            }

            return new SdProgress(Num(root, "progress"), Num(root, "eta_relative"), step, total, job, jobCount, current);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Interrumpe la generacion en curso (txt2img devuelve lo que ya tenga).</summary>
    public async Task InterruptAsync()
    {
        try
        {
            using var response = await Http.PostAsync($"{BaseUrl}/sdapi/v1/interrupt", new StringContent(""));
        }
        catch
        {
            // Si el servidor ya habia terminado, no hay nada que interrumpir.
        }
    }

    // ===================== Utilidades =====================

    private async Task<JsonDocument> GetDocAsync(string path, CancellationToken ct = default)
    {
        using var response = await Http.GetAsync($"{BaseUrl}{path}", ct);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    private static double Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
}
