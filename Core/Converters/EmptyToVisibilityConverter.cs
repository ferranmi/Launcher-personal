using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Launcher.Core.Converters;

/// <summary>Visible cuando una coleccion esta vacia (Count == 0). Util para el
/// "empty state" de una lista. ConverterParameter="Invert" para el caso contrario.</summary>
public class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value is int i ? i : 0;
        var isEmpty = count == 0;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase)) isEmpty = !isEmpty;
        return isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
