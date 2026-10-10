using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Launcher.Core.Converters;

/// <summary>bool -> Visibility. Usa ConverterParameter="Invert" para el caso contrario.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is bool v && v;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase)) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
