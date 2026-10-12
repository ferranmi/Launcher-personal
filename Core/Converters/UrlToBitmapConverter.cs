using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Launcher.Core.Converters;

/// <summary>Convierte una URL http/https en un BitmapImage para <Image Source=.../>. La
/// descarga es asincrona (la imagen aparece cuando llega). ConverterParameter opcional: ancho
/// en pixeles al que decodificar, para no cargar en memoria una imagen enorme en un icono
/// pequeno. Devuelve null (sin lanzar) si la URL no es valida.</summary>
public class UrlToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var url = value as string;
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            if (parameter is string text && int.TryParse(text, out var width)) bitmap.DecodePixelWidth = width;
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
