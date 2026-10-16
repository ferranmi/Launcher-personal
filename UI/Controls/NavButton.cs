using System.Windows;
using System.Windows.Controls;

namespace Launcher.UI.Controls;

/// <summary>
/// Boton de navegacion del sidebar. Es un RadioButton con un icono (glifo Segoe Fluent Icons)
/// ademas del texto, y actua como toggle exclusivo dentro de su GroupName.
/// IsCompact oculta el texto y deja solo el icono (menu lateral colapsado).
/// </summary>
public class NavButton : RadioButton
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(NavButton), new PropertyMetadata(""));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(NavButton), new PropertyMetadata(false));

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
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
