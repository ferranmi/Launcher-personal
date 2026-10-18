using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace Launcher.Core.Services;

/// <summary>
/// Pinta el fondo de la zona de contenido segun los ajustes: ninguno, degradado,
/// degradado animado o imagen. La animacion se detiene sola cuando la ventana esta
/// oculta o minimizada (con AllowsTransparency WPF renderiza por software, asi que
/// no conviene animar en segundo plano).
/// </summary>
public static class BackgroundService
{
    private static Border? _layer;
    private static Window? _window;
    private static string? _appliedKey;
    private static bool _running;
    private static GradientStop? _moving;
    private static GradientStop? _tinted;

    public static void Attach(Border layer, Window window)
    {
        _layer = layer;
        _window = window;

        AppSettingsService.Instance.Changed += (_, _) => Apply();
        window.StateChanged += (_, _) => UpdateRunning();
        window.IsVisibleChanged += (_, _) => UpdateRunning();

        Apply();
    }

    private static void Apply()
    {
        if (_layer == null) return;

        var s = AppSettingsService.Instance;
        var key = $"{s.BackgroundMode}|{s.BackgroundImagePath}|{s.BackgroundOpacity:0.00}";
        if (key == _appliedKey) return; // Changed salta en cada Save(): no reconstruir sin motivo
        _appliedKey = key;

        StopAnimation();
        _moving = _tinted = null;

        var opacity = Math.Clamp(s.BackgroundOpacity, 0.05, 1.0);
        _layer.Background = s.BackgroundMode switch
        {
            "gradient" => BuildGradient(opacity, animated: false),
            "animated" => BuildGradient(opacity, animated: true),
            "image" => BuildImage(s.BackgroundImagePath, opacity),
            _ => null
        };

        UpdateRunning();
    }

    private static Brush BuildGradient(double opacity, bool animated)
    {
        var bg = Res("Color.Background");
        var dim = Res("Color.AccentDim");

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            Opacity = opacity
        };

        var s0 = new GradientStop(bg, 0.0);
        var s1 = new GradientStop(dim, 0.55);
        var s2 = new GradientStop(bg, 1.0);
        brush.GradientStops.Add(s0);
        brush.GradientStops.Add(s1);
        brush.GradientStops.Add(s2);

        if (animated)
        {
            _tinted = s0;
            _moving = s1;
        }
        else
        {
            brush.Freeze();
        }

        return brush;
    }

    private static Brush? BuildImage(string? path, double opacity)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 1920; // evita cargar un 4K entero en memoria
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            var brush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill, Opacity = opacity };
            brush.Freeze();
            return brush;
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo cargar la imagen de fondo", ex);
            return null;
        }
    }

    // ===================== Animacion (solo con ventana visible) =====================

    private static void UpdateRunning()
    {
        if (_moving == null || _tinted == null || _window == null) return;

        var shouldRun = _window.IsVisible && _window.WindowState != WindowState.Minimized;
        if (shouldRun && !_running) StartAnimation();
        else if (!shouldRun && _running) StopAnimation();
    }

    private static void StartAnimation()
    {
        if (_moving == null || _tinted == null) return;

        _moving.BeginAnimation(GradientStop.OffsetProperty, new DoubleAnimation(0.25, 0.85, TimeSpan.FromSeconds(12))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });

        _tinted.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(Res("Color.Background"), Res("Color.SurfaceActive"), TimeSpan.FromSeconds(9))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        });

        _running = true;
    }

    private static void StopAnimation()
    {
        _moving?.BeginAnimation(GradientStop.OffsetProperty, null);
        _tinted?.BeginAnimation(GradientStop.ColorProperty, null);
        _running = false;
    }

    private static Color Res(string key) => (Color)Application.Current.FindResource(key);
}
