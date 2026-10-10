using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Launcher.Core.Converters;

/// <summary>Convierte la profundidad (0 = fila normal/padre, 1 = hijo dentro de un
/// grupo expandido) en un margen izquierdo para dar la sangria visual de arbol.</summary>
public class DepthToIndentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var depth = value is int i ? i : 0;
        return new Thickness(depth * 26, 0, 0, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}