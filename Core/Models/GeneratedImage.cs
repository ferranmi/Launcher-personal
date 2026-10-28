using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;

namespace Launcher.Core.Models;

/// <summary>Una imagen generada y guardada en disco, con los parametros que la
/// produjeron (y la seed real usada, aunque se pidiera -1).</summary>
public class GeneratedImage : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public long Seed { get; set; }
    public ImageGenParams Params { get; set; } = new();

    private bool _favorite;
    public bool Favorite
    {
        get => _favorite;
        set
        {
            if (_favorite == value) return;
            _favorite = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Favorite)));
        }
    }

    [JsonIgnore]
    public bool Exists => !string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath);

    /// <summary>Texto del tooltip de la miniatura.</summary>
    [JsonIgnore]
    public string Summary =>
        $"{Params.ModelName}\n{Params.Sampler} · {Params.Steps} steps · CFG {Params.CfgScale:0.#} · {Params.Width}x{Params.Height}\nseed {Seed}";

    public event PropertyChangedEventHandler? PropertyChanged;
}
