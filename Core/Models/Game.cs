using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Launcher.Core.Models;

/// <summary>
/// Entidad de un juego en la biblioteca. Implementa INotifyPropertyChanged para
/// que la UI (tarjetas) reaccione al instante a cambios como favorito/oculto,
/// y se serializa tal cual a games.json.
/// </summary>
public class Game : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _name = string.Empty;
    public string Name { get => _name; set { _name = value; OnChanged(); } }

    private string _executablePath = string.Empty;
    public string ExecutablePath { get => _executablePath; set { _executablePath = value; OnChanged(); } }

    private string _arguments = string.Empty;
    public string Arguments { get => _arguments; set { _arguments = value; OnChanged(); } }

    private string? _coverImagePath;
    public string? CoverImagePath
    {
        get => _coverImagePath;
        set { _coverImagePath = value; OnChanged(); OnChanged(nameof(HasCover)); }
    }

    private bool _favorite;
    public bool Favorite { get => _favorite; set { _favorite = value; OnChanged(); } }

    private bool _hidden;
    public bool Hidden { get => _hidden; set { _hidden = value; OnChanged(); } }

    public GameSource Source { get; set; } = GameSource.Manual;

    /// <summary>Id del juego segun el proveedor externo (p.ej. Steam AppId). Permite
    /// hacer match al re-sincronizar sin duplicar entradas. Null en juegos manuales.</summary>
    public string? ExternalId { get; set; }

    public DateTime? LastPlayed { get; set; }

    private int _playTimeMinutes;
    public int PlayTimeMinutes
    {
        get => _playTimeMinutes;
        set { _playTimeMinutes = value; OnChanged(); OnChanged(nameof(Subtitle)); }
    }

    /// <summary>Texto bajo el nombre en la tarjeta: origen y, si lo hay, tiempo jugado.</summary>
    [JsonIgnore]
    public string Subtitle => PlayTimeMinutes <= 0
        ? Source.ToString()
        : $"{Source}  \u00B7  {FormatPlayTime(PlayTimeMinutes)}";

    private static string FormatPlayTime(int minutes) =>
        minutes < 60 ? $"{minutes} min" : $"{minutes / 60.0:0.#} h";

    public bool HasCover => !string.IsNullOrWhiteSpace(CoverImagePath) && File.Exists(CoverImagePath);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}