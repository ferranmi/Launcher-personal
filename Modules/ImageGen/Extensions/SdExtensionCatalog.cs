using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Launcher.Modules.ImageGen.Extensions;

/// <summary>Registro de extensiones (como la lista de modulos en MainWindow).
/// Para anadir una propia: crea su clase y anade una linea aqui.</summary>
public static class SdExtensionCatalog
{
    public static List<SdExtension> CreateAll() => new()
    {
        new AdetailerExtension(),
        new ControlNetExtension(),
        new WildcardsExtension(),
        new DanbooruTagsExtension(),
        new RawScriptExtension()
    };
}

/// <summary>Tarjeta de una extension: casilla de activar, estado, descripcion y su
/// panel (visible solo cuando esta activada y disponible).</summary>
public static class ExtensionShell
{
    public static FrameworkElement Create(SdExtension extension)
    {
        var app = System.Windows.Application.Current;

        var check = new CheckBox
        {
            Content = extension.DisplayName,
            Foreground = (Brush)app.FindResource("Brush.TextPrimary"),
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center
        };

        var status = new TextBlock
        {
            FontFamily = (FontFamily)app.FindResource("Font.Mono"),
            FontSize = 9.5,
            Foreground = (Brush)app.FindResource("Brush.TextDim"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(check);
        header.Children.Add(status);

        var description = new TextBlock
        {
            Text = extension.Description,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = (FontFamily)app.FindResource("Font.Mono"),
            FontSize = 9.5,
            Foreground = (Brush)app.FindResource("Brush.TextDim"),
            Margin = new Thickness(0, 4, 0, 0)
        };

        var body = new StackPanel { Margin = new Thickness(0, 12, 0, 0), Visibility = Visibility.Collapsed };
        body.Children.Add(extension.BuildPanel());

        var content = new StackPanel();
        content.Children.Add(header);
        content.Children.Add(description);
        content.Children.Add(body);

        var card = new Border
        {
            Style = (Style)app.FindResource("CardStyle"),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            Child = content
        };

        void Sync()
        {
            check.IsEnabled = extension.IsAvailable;
            if (check.IsChecked != extension.IsEnabled) check.IsChecked = extension.IsEnabled;
            body.Visibility = extension.IsEnabled && extension.IsAvailable ? Visibility.Visible : Visibility.Collapsed;
            status.Text = extension.StatusText;
        }

        check.Checked += (_, _) => extension.IsEnabled = true;
        check.Unchecked += (_, _) => extension.IsEnabled = false;
        extension.Changed += Sync;

        Sync();
        return card;
    }
}
