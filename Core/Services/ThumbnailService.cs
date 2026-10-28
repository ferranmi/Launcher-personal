using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Launcher.Core.Services;

/// <summary>
/// Miniaturas para el explorador de Carpetas (y la vista previa). Las imagenes se
/// decodifican directamente a su tamano; el resto (videos, documentos...) usa la cache
/// de miniaturas de Windows via ShellThumbnailService, que devuelve el icono si no
/// hay miniatura. Reconstruido el 2026-10-27 tras sobrescribir el original por error:
/// mantiene la firma que usan FileSystemEntry y FolderExplorerPanel.
/// </summary>
public static class ThumbnailService
{
    // Una carpeta con cientos de ficheros pediria cientos de miniaturas a la vez:
    // se limita a unas pocas en paralelo para no saturar CPU ni disco.
    private static readonly SemaphoreSlim Gate = new(4);

    public static async Task<ImageSource?> GetAsync(string path, bool isImage, int size, CancellationToken ct = default)
    {
        try
        {
            await Gate.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return null; // el usuario cambio de carpeta antes de que le tocara
        }

        try
        {
            if (ct.IsCancellationRequested) return null;

            if (isImage)
            {
                var decoded = await Task.Run(() => DecodeImage(path, size), ct);
                if (decoded != null) return decoded;
            }

            if (ct.IsCancellationRequested) return null;
            return await ShellThumbnailService.GetAsync(path, size);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static ImageSource? DecodeImage(string path, int size)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // no deja el fichero bloqueado
            bitmap.DecodePixelWidth = size;                // no carga la imagen completa en memoria
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null; // formato no soportado o fichero ilegible: se intenta con la miniatura de Windows
        }
    }
}
