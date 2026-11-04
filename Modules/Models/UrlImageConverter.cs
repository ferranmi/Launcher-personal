using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Launcher.Modules.Models;

/// <summary>URL https de una imagen -> BitmapImage reducido (ConverterParameter = ancho de decodificacion).
/// La descarga de la imagen es asincrona; devuelve null si la URL no es valida.</summary>
public class UrlImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var width = parameter is string text && int.TryParse(text, out var parsed) ? parsed : 300;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = width;
            bitmap.UriSource = uri;
            bitmap.EndInit();
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
