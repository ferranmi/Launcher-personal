using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Core.Models;

/// <summary>
/// Implementacion generica de IModule para modulos que todavia no tienen vista propia.
/// Cuando desarrolles un modulo de verdad (p.ej. Juegos), crea su propia clase
/// (GamesModule) que implemente IModule directamente y devuelva su UserControl real,
/// y quita el placeholder correspondiente del registro en MainWindow.
/// </summary>
public class PlaceholderModule : IModule
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Icon { get; }
    public int Order { get; }

    public PlaceholderModule(string id, string displayName, string icon, int order)
    {
        Id = id;
        DisplayName = displayName;
        Icon = icon;
        Order = order;
    }

    public UserControl CreateView()
    {
        var app = System.Windows.Application.Current;

        var panel = new StackPanel
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = $"> {DisplayName.ToUpperInvariant()}",
            FontFamily = (System.Windows.Media.FontFamily)app.FindResource("Font.Mono"),
            FontSize = 22,
            Foreground = (System.Windows.Media.Brush)app.FindResource("Brush.TextPrimary"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Text = "modulo en construccion_",
            FontFamily = (System.Windows.Media.FontFamily)app.FindResource("Font.Mono"),
            FontSize = 12,
            Margin = new System.Windows.Thickness(0, 8, 0, 0),
            Foreground = (System.Windows.Media.Brush)app.FindResource("Brush.TextDim"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        });

        return new UserControl { Content = panel };
    }
}
