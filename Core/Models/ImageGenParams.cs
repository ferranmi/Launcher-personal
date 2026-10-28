namespace Launcher.Core.Models;

/// <summary>Parametros de una generacion txt2img. Se serializa tal cual a la
/// galeria (imagegen/gallery.json) para poder reutilizarlos despues.</summary>
public class ImageGenParams
{
    public string Prompt { get; set; } = "";
    public string NegativePrompt { get; set; } = "";

    /// <summary>Titulo completo del checkpoint tal y como lo conoce A1111
    /// ("modelo.safetensors [hash]"). Es lo que acepta override_settings.</summary>
    public string? ModelTitle { get; set; }
    public string? ModelName { get; set; }

    public string Sampler { get; set; } = "Euler a";

    /// <summary>Null = "Automatic" (no se envia el campo).</summary>
    public string? Scheduler { get; set; }

    public int Steps { get; set; } = 20;
    public double CfgScale { get; set; } = 7;
    public int Width { get; set; } = 512;
    public int Height { get; set; } = 512;

    /// <summary>-1 = aleatoria.</summary>
    public long Seed { get; set; } = -1;

    /// <summary>n_iter: cuantas tandas se generan una detras de otra.</summary>
    public int BatchCount { get; set; } = 1;

    /// <summary>batch_size: imagenes por tanda (en paralelo, mas VRAM).</summary>
    public int BatchSize { get; set; } = 1;

    /// <summary>Salto de capas de CLIP (A1111: CLIP_stop_at_last_layers). 1 = sin salto.</summary>
    public int ClipSkip { get; set; } = 1;

    /// <summary>Null = "Automatic". Tambien admite "None" o el nombre de un VAE.</summary>
    public string? Vae { get; set; }

    /// <summary>Estado de cada extension (id -> JSON), para poder reproducir una generacion.</summary>
    public Dictionary<string, string> ExtensionStates { get; set; } = new();

    public ImageGenParams Clone()
    {
        var copy = (ImageGenParams)MemberwiseClone();
        copy.ExtensionStates = new Dictionary<string, string>(ExtensionStates);
        return copy;
    }
}
