using System.Windows;
using System.Windows.Controls.Primitives;

namespace Launcher.Core.Services;

/// <summary>
/// Navegacion entre modulos desde cualquier sitio (p.ej. las etiquetas [X] del
/// dashboard). MainWindow registra el manejador una vez. En XAML:
///   xmlns:svc="clr-namespace:Launcher.Core.Services"
///   &lt;Button Content="[JUEGOS]" svc:Navigator.To="games" Style="{StaticResource QuickLinkStyle}" /&gt;
/// </summary>
public static class Navigator
{
    private static Action<string>? _handler;

    public static void Register(Action<string> handler) => _handler = handler;

    public static void GoTo(string moduleId) => _handler?.Invoke(moduleId);

    public static readonly DependencyProperty ToProperty =
        DependencyProperty.RegisterAttached("To", typeof(string), typeof(Navigator),
            new PropertyMetadata(null, OnToChanged));

    public static string? GetTo(DependencyObject d) => (string?)d.GetValue(ToProperty);
    public static void SetTo(DependencyObject d, string? value) => d.SetValue(ToProperty, value);

    private static void OnToChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ButtonBase button) return;
        button.Click -= OnClick;
        if (e.NewValue != null) button.Click += OnClick;
    }

    private static void OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject d && GetTo(d) is { Length: > 0 } id) GoTo(id);
    }
}
