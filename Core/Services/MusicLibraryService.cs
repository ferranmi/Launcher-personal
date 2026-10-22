using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using Launcher.Core.Models;
using Microsoft.VisualBasic.FileIO;

namespace Launcher.Core.Services;

/// <summary>
/// Biblioteca musical basada en una carpeta: cada archivo de audio es una cancion y
/// sus datos (titulo, artista, album, ano, caratula) viven en las etiquetas del propio
/// archivo, leidas y escritas con TagLib#. No hay base de datos ni JSON: si copias la
/// carpeta a otro PC, la biblioteca va contigo.
///
/// Es un singleton: asi la vista se puede recrear sin releer toda la carpeta.
/// </summary>
public class MusicLibraryService
{
    public static MusicLibraryService Instance { get; } = new();

    public static readonly string[] Extensions =
        { ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wav", ".wma" };

    public ObservableCollection<MusicTrack> Tracks { get; } = new();

    public string Folder => AppSettingsService.Instance.MusicFolder;

    private MusicLibraryService() { }

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    // ===================== Carga =====================

    /// <summary>Vuelve a leer toda la carpeta. Las etiquetas se leen en segundo plano y
    /// las canciones van apareciendo en la lista segun se leen.</summary>
    public async Task LoadAsync()
    {
        Tracks.Clear();

        List<string> files;
        try
        {
            Directory.CreateDirectory(Folder);
            files = await Task.Run(() => Directory
                .EnumerateFiles(Folder, "*.*", System.IO.SearchOption.AllDirectories)
                .Where(IsSupported)
                .ToList());
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer la carpeta de musica", ex);
            return;
        }

        foreach (var file in files)
        {
            var track = await Task.Run(() => ReadTrack(file));
            if (track != null) Tracks.Add(track);
        }
    }

    private static MusicTrack? ReadTrack(string path)
    {
        try
        {
            using var f = TagLib.File.Create(path);
            var tag = f.Tag;

            return new MusicTrack
            {
                FilePath = path,
                Title = string.IsNullOrWhiteSpace(tag.Title) ? Path.GetFileNameWithoutExtension(path) : tag.Title,
                Artist = tag.FirstPerformer ?? "",
                Album = tag.Album ?? "",
                Year = tag.Year,
                Duration = f.Properties.Duration,
                Cover = tag.Pictures.Length > 0 ? DecodeCover(tag.Pictures[0].Data.Data) : null
            };
        }
        catch (Exception ex)
        {
            // Archivo corrupto o formato raro: se omite en vez de tumbar la carga entera.
            LogService.Error($"No se pudo leer la cancion '{path}'", ex);
            return null;
        }
    }

    /// <summary>Decodifica la caratula a un tamano pequeno (suficiente para las tarjetas)
    /// y la congela para poder crearla en un hilo de fondo.</summary>
    private static BitmapImage? DecodeCover(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 320;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    // ===================== Importar =====================

    /// <summary>Copia los archivos a la carpeta de la biblioteca (sin tocar los originales)
    /// y los anade a la lista. Devuelve cuantas canciones se han importado.</summary>
    public async Task<int> ImportAsync(IEnumerable<string> files)
    {
        var imported = 0;

        try { Directory.CreateDirectory(Folder); }
        catch (Exception ex)
        {
            LogService.Error("No se pudo crear la carpeta de musica", ex);
            return 0;
        }

        foreach (var source in files.Where(IsSupported))
        {
            try
            {
                var alreadyInLibrary = Tracks.Any(t =>
                    string.Equals(t.FilePath, source, StringComparison.OrdinalIgnoreCase));
                if (alreadyInLibrary) continue;

                var inFolder = Path.GetFullPath(source)
                    .StartsWith(Path.GetFullPath(Folder), StringComparison.OrdinalIgnoreCase);

                var destination = source;
                if (!inFolder)
                {
                    destination = UniquePath(Path.Combine(Folder, Path.GetFileName(source)));
                    await Task.Run(() => File.Copy(source, destination));
                }

                var track = await Task.Run(() => ReadTrack(destination));
                if (track == null) continue;

                Tracks.Add(track);
                imported++;
            }
            catch (Exception ex)
            {
                LogService.Error($"No se pudo importar '{source}'", ex);
            }
        }

        return imported;
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    // ===================== Editar =====================

    /// <summary>Escribe los datos en las etiquetas del archivo y actualiza la cancion.
    /// newCoverPath = imagen nueva; removeCover = quitar la caratula actual.
    /// Devuelve false si no se pudo escribir (archivo en uso, solo lectura...).</summary>
    public bool ApplyEdits(MusicTrack track, string title, string artist, string album, uint year,
                           string? newCoverPath, bool removeCover)
    {
        try
        {
            byte[]? coverBytes = newCoverPath != null ? File.ReadAllBytes(newCoverPath) : null;

            using (var f = TagLib.File.Create(track.FilePath))
            {
                f.Tag.Title = title;
                f.Tag.Performers = string.IsNullOrWhiteSpace(artist) ? Array.Empty<string>() : new[] { artist };
                f.Tag.Album = album;
                f.Tag.Year = year;

                if (coverBytes != null)
                {
                    var picture = new TagLib.Picture(new TagLib.ByteVector(coverBytes))
                    {
                        Type = TagLib.PictureType.FrontCover
                    };
                    f.Tag.Pictures = new TagLib.IPicture[] { picture };
                }
                else if (removeCover)
                {
                    f.Tag.Pictures = Array.Empty<TagLib.IPicture>();
                }

                f.Save();
            }

            track.Title = title;
            track.Artist = artist;
            track.Album = album;
            track.Year = year;

            if (coverBytes != null) track.Cover = DecodeCover(coverBytes);
            else if (removeCover) track.Cover = null;

            return true;
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudieron guardar los datos de '{track.Title}'", ex);
            return false;
        }
    }

    // ===================== Acciones =====================

    /// <summary>Abre la cancion con el reproductor predeterminado de Windows.</summary>
    public void Play(MusicTrack track)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = track.FilePath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo reproducir '{track.Title}'", ex);
        }
    }

    public void ShowInExplorer(MusicTrack track)
    {
        try
        {
            Process.Start("explorer.exe", $"/select,\"{track.FilePath}\"");
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo abrir la carpeta de la cancion", ex);
        }
    }

    /// <summary>Envia el archivo a la papelera (recuperable) y lo quita de la lista.</summary>
    public bool Delete(MusicTrack track)
    {
        try
        {
            FileSystem.DeleteFile(track.FilePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            Tracks.Remove(track);
            return true;
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo eliminar '{track.Title}'", ex);
            return false;
        }
    }
}
