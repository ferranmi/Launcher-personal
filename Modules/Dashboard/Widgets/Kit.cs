using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;

namespace Launcher.Modules.Dashboard.Widgets;

/// <summary>Piezas pequenas compartidas por los widgets del dashboard: todo sale del tema de la app
/// (Theme.Dark.xaml), asi que los widgets nuevos heredan colores y tipografia sin XAML propio.</summary>
internal static class Kit
{
    private static object Res(string key) => Application.Current.FindResource(key);

    public static Brush Br(string key) => (Brush)Res(key);
    public static FontFamily Mono => (FontFamily)Res("Font.Mono");
    public static FontFamily Icons => (FontFamily)Res("Font.Icons");

    /// <summary>Azul de UPLINK, el mismo del dashboard. Ese pincel vive en el XAML de la vista y desde aqui
    /// no se puede pedir por nombre, asi que se define aparte.</summary>
    public static readonly Brush Uplink = Freeze(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0x9A, 0xFF)));

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    public static TextBlock Label(string text, double size = 11.5, string brush = "Brush.TextPrimary", bool mono = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = Br(brush),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (mono) block.FontFamily = Mono;
        return block;
    }

    /// <summary>Boton con el estilo de la app (PrimaryButtonStyle / SecondaryButtonStyle), mas compacto.</summary>
    public static Button ActionButton(object content, RoutedEventHandler onClick, bool primary = false)
    {
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(10, 5, 10, 5),
            FontSize = 11,
            Style = (Style)Res(primary ? "PrimaryButtonStyle" : "SecondaryButtonStyle")
        };
        button.Click += onClick;
        return button;
    }

    /// <summary>Caja de texto con el estilo de la app; 'placeholder' sale cuando esta vacia.</summary>
    public static TextBox Input(string placeholder) => new()
    {
        Style = (Style)Res("TextBoxStyle"),
        Tag = placeholder,
        Padding = new Thickness(8, 5, 8, 5),
        FontSize = 12
    };

    /// <summary>Barra fina de progreso sin plantilla: dos columnas proporcionales sobre un fondo.</summary>
    public static Grid Bar(double percent, Brush fill)
    {
        percent = Math.Clamp(percent, 0, 100);

        var grid = new Grid { Height = 5 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(percent, 0.0001), GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(100 - percent, 0.0001), GridUnitType.Star) });

        var track = new Border { Background = Br("Brush.Border"), CornerRadius = new CornerRadius(3) };
        Grid.SetColumnSpan(track, 2);
        grid.Children.Add(track);

        grid.Children.Add(new Border { Background = fill, CornerRadius = new CornerRadius(3) });
        return grid;
    }
}
