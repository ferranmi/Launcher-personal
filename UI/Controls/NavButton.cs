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

    public static readonly DependencyProperty IsShortProperty =
        DependencyProperty.Register(nameof(IsShort), typeof(bool), typeof(NavButton),
            new PropertyMetadata(false, (d, e) => ((NavButton)d).OnIsShortChanged((bool)e.NewValue)));

    /// <summary>Texto abreviado (p.ej. "JUE") que se muestra cuando IsShort esta activo.</summary>
    public string? ShortText { get; set; }

    private string? _fullText;

    /// <summary>Menu a ancho intermedio: en lugar del nombre completo se muestra la abreviatura.</summary>
    public bool IsShort
    {
        get => (bool)GetValue(IsShortProperty);
        set => SetValue(IsShortProperty, value);
    }

    private void OnIsShortChanged(bool isShort)
    {
        _fullText ??= Content as string;
        if (_fullText == null) return;

        Content = isShort && !string.IsNullOrEmpty(ShortText) ? ShortText : _fullText;
    }

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
