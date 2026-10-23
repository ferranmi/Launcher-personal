namespace Launcher.Core.Models;

/// <summary>
/// Datos "correctos" de una cancion tal y como los resuelve MusicBrainz (o, si no hay
/// coincidencia fiable, el titulo limpio como mejor suposicion).
/// </summary>
public class MusicMetadata
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public uint Year { get; set; }

    /// <summary>Duracion de la grabacion oficial segun MusicBrainz (null si no consta).
    /// Sirve para elegir la version correcta entre los resultados de YouTube.</summary>
    public double? DurationSeconds { get; set; }

    /// <summary>Caratula oficial (Cover Art Archive), o null si no se encontro.</summary>
    public byte[]? CoverBytes { get; set; }

    /// <summary>True si viene de una coincidencia fiable en MusicBrainz; false si es
    /// solo el titulo limpiado (conviene revisarlo con Editar).</summary>
    public bool Verified { get; set; }

    public string Summary
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Artist) ? Title : $"{Artist} - {Title}";
            var extra = string.Join(", ", new[] { Album, Year > 0 ? Year.ToString() : "" }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            return extra.Length > 0 ? $"{name} ({extra})" : name;
        }
    }
}
