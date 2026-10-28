using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Launcher.Modules.ImageGen;

/// <summary>Ruta de archivo -> miniatura. A diferencia de PathToBitmapConverter
/// decodifica a un ancho reducido (DecodePixelWidth), para que una galeria con
/// muchas imagenes grandes no consuma gigas de memoria.</summary>
public class ThumbnailConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        var width = parameter is string s && int.TryParse(s, out var w) ? w : 200;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = width;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
