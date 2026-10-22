using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Launcher.Core.Models;

/// <summary>
/// Una cancion de la biblioteca. No se persiste en JSON: la fuente de verdad son
/// los propios archivos de audio (sus etiquetas ID3/Vorbis/etc), que se leen con
/// TagLib# al abrir el modulo.
/// </summary>
public class MusicTrack : INotifyPropertyChanged
{
    public string FilePath { get; set; } = string.Empty;

    private string _title = string.Empty;
    public string Title { get => _title; set { _title = value; OnChanged(); } }

    private string _artist = string.Empty;
    public string Artist
    {
        get => _artist;
        set { _artist = value; OnChanged(); OnChanged(nameof(Subtitle)); }
    }

    private string _album = string.Empty;
    public string Album { get => _album; set { _album = value; OnChanged(); } }

    private uint _year;
    public uint Year { get => _year; set { _year = value; OnChanged(); } }

    public TimeSpan Duration { get; set; }

    private ImageSource? _cover;
    public ImageSource? Cover
    {
        get => _cover;
        set { _cover = value; OnChanged(); OnChanged(nameof(HasCover)); }
    }

    public bool HasCover => Cover != null;

    public string DurationText => Duration.TotalHours >= 1
        ? Duration.ToString(@"h\:mm\:ss")
        : Duration.ToString(@"m\:ss");

    /// <summary>Texto bajo el titulo en la tarjeta: artista y duracion.</summary>
    public string Subtitle =>
        $"{(string.IsNullOrWhiteSpace(Artist) ? "Artista desconocido" : Artist)}  \u00B7  {DurationText}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
