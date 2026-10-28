using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.ImageGen;

/// <summary>
/// Galeria de imagenes generadas. Los PNG se guardan en
/// %AppData%\Launcher\imagegen\outputs y el indice (con los parametros de cada
/// una) en %AppData%\Launcher\imagegen\gallery.json. Mismo patron que
/// GameLibraryService: ObservableCollection + JSON.
/// </summary>
public class GalleryService
{
    private static readonly string BaseFolder = Path.Combine(AppSettingsService.AppFolder, "imagegen");
    public static readonly string ImagesFolder = Path.Combine(BaseFolder, "outputs");
    private static readonly string IndexPath = Path.Combine(BaseFolder, "gallery.json");

    public static GalleryService Instance { get; } = new();

    /// <summary>Mas recientes primero.</summary>
    public ObservableCollection<GeneratedImage> Images { get; } = new();

    private GalleryService()
    {
        Directory.CreateDirectory(ImagesFolder);
        Load();
    }

    private void Load()
    {
        if (!File.Exists(IndexPath)) return;

        try
        {
            var items = JsonSerializer.Deserialize<List<GeneratedImage>>(File.ReadAllText(IndexPath))
                        ?? new List<GeneratedImage>();

            // Descartamos entradas cuyo archivo ya no existe (borrado a mano).
            foreach (var img in items.Where(i => i.Exists).OrderByDescending(i => i.CreatedAt))
            {
                Images.Add(img);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer gallery.json", ex);
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(IndexPath,
                JsonSerializer.Serialize(Images.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo guardar gallery.json", ex);
        }
    }

    public async Task<GeneratedImage> AddAsync(byte[] pngBytes, ImageGenParams p, long seed)
    {
        Directory.CreateDirectory(ImagesFolder);

        var id = Guid.NewGuid().ToString("N");
        var path = Path.Combine(ImagesFolder, $"{id}.png");
        await File.WriteAllBytesAsync(path, pngBytes);

        var image = new GeneratedImage
        {
            Id = id,
            FilePath = path,
            Seed = seed,
            CreatedAt = DateTime.Now,
            Params = p.Clone()
        };

        Images.Insert(0, image);
        Save();
        return image;
    }

    /// <summary>Quita la imagen de la galeria y borra su PNG (son archivos generados
    /// por el propio modulo, no del usuario).</summary>
    public void Remove(GeneratedImage image)
    {
        Images.Remove(image);

        try
        {
            if (File.Exists(image.FilePath)) File.Delete(image.FilePath);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo borrar la imagen generada", ex);
        }

        Save();
    }
}
