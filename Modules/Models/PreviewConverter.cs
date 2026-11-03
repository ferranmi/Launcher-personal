using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Launcher.Modules.Models;

/// <summary>Ruta de imagen -> BitmapImage reducido. ConverterParameter = ancho de decodificacion en
/// pixeles (por defecto 300): asi miles de previews no ocupan memoria a tamano completo. Devuelve
/// null, sin lanzar, si el archivo no existe o no se puede leer. OnLoad no deja el archivo bloqueado.</summary>
public class PreviewConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        var width = parameter is string text && int.TryParse(text, out var parsed) ? parsed : 300;

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
