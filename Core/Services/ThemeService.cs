using System.Windows;
using System.Windows.Media.Effects;

namespace Launcher.Core.Services;

/// <summary>
/// Aplica el color de acento en caliente. Los pinceles del tema (Brush.Accent,
/// Brush.AccentHover, Brush.AccentDim) se modifican en su sitio para que tambien
/// cambien los estilos que los referencian con StaticResource; los colores y el
/// efecto de brillo se sobrescriben a nivel de aplicacion para los
/// DynamicResource (degradados, glow).
///
/// Se usan nombres completos de System.Windows.Media porque el proyecto tiene un
/// using global de System.Drawing (Color, ColorConverter... serian ambiguos).
/// </summary>
public static class ThemeService
{
    /// <summary>Paleta ofrecida en Configuracion.</summary>
    public static readonly (string Name, string Hex)[] AccentPresets =
    {
        ("Verde", "#3ECF7E"),
        ("Cian", "#35C6D9"),
        ("Azul", "#5B9DFF"),
        ("Violeta", "#A78BFA"),
        ("Rosa", "#F472B6"),
        ("Naranja", "#F59E42"),
    };

    // Mismo valor que Color.Background en Theme.Dark.xaml; se usa para derivar el tono atenuado.
    private static readonly System.Windows.Media.Color BackgroundColor =
        System.Windows.Media.Color.FromRgb(0x0B, 0x0F, 0x0D);

    public static bool TryParseColor(string? hex, out System.Windows.Media.Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        try
        {
            if (System.Windows.Media.ColorConverter.ConvertFromString(hex.Trim()) is System.Windows.Media.Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch
        {
            // formato invalido: el llamador usa el acento por defecto.
        }

        return false;
    }

    /// <summary>Aplica el acento indicado (o el verde por defecto si el valor no es valido).</summary>
    public static void ApplyAccent(string? hex)
    {
        if (!TryParseColor(hex, out var accent))
        {
            TryParseColor(AppSettingsService.DefaultAccent, out accent);
        }

        var hover = Blend(accent, System.Windows.Media.Colors.White, 0.2);
        var dim = Blend(BackgroundColor, accent, 0.25);

        var resources = Application.Current.Resources;

        // Colores: los usan degradados con DynamicResource (p.ej. la caratula vacia de Multimedia).
        resources["Color.Accent"] = accent;
        resources["Color.AccentHover"] = hover;
        resources["Color.AccentDim"] = dim;
        resources["Color.AccentGlow"] = accent;

        SetBrush(resources, "Brush.Accent", accent);
        SetBrush(resources, "Brush.AccentHover", hover);
        SetBrush(resources, "Brush.AccentDim", dim);

        // El brillo del titulo y del boton de play se vuelve a crear con el nuevo color.
        resources["Effect.AccentGlow"] = new DropShadowEffect
        {
            Color = accent,
            BlurRadius = 10,
            ShadowDepth = 0,
            Opacity = 0.55
        };
    }

    private static void SetBrush(ResourceDictionary resources, string key, System.Windows.Media.Color color)
    {
        if (resources[key] is System.Windows.Media.SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color; // cambia tambien los StaticResource que apuntan a este mismo pincel
            return;
        }

        // Pincel congelado o ausente: se sustituye (los DynamicResource lo recogen; los
        // StaticResource ya resueltos se actualizaran al reiniciar la app).
        resources[key] = new System.Windows.Media.SolidColorBrush(color);
    }

    private static System.Windows.Media.Color Blend(
        System.Windows.Media.Color from, System.Windows.Media.Color to, double t) =>
        System.Windows.Media.Color.FromRgb(
            (byte)Math.Round(from.R + (to.R - from.R) * t),
            (byte)Math.Round(from.G + (to.G - from.G) * t),
            (byte)Math.Round(from.B + (to.B - from.B) * t));
}
