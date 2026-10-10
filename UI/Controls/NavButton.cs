using System.Windows;
using System.Windows.Controls;

namespace Launcher.UI.Controls;

/// <summary>
/// Boton de navegacion del sidebar. Es un RadioButton con un icono (glifo Segoe Fluent Icons)
/// ademas del texto, y actua como toggle exclusivo dentro de su GroupName.
/// </summary>
public class NavButton : RadioButton
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(NavButton), new PropertyMetadata(""));

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    static NavButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NavButton), new FrameworkPropertyMetadata(typeof(NavButton)));
    }

    public NavButton()
    {
        Style = (Style)Application.Current.FindResource("NavButtonStyle");
    }
}
