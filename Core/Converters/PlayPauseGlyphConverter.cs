using System.Globalization;
using System.Windows.Data;

namespace Launcher.Core.Converters;

/// <summary>bool IsPlaying -> glifo Segoe Fluent Icons: si esta sonando mostramos
/// el icono de pausa (la accion que el boton va a ejecutar), y viceversa.</summary>
public class PlayPauseGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isPlaying = value is bool b && b;
        return isPlaying ? "\uE769" : "\uE768"; // Pause / Play
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}