using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Launcher.Core.Services;

namespace Launcher.Modules.ImageGen.Extensions;

/// <summary>
/// Extension PROPIA (no necesita nada en A1111): sustituye __nombre__ en el prompt y el
/// negativo por una linea al azar de wildcards\nombre.txt. Admite subcarpetas
/// (__estilos/pintura__) y wildcards anidados. Cada imagen recibe su propia sustitucion: A1111 solo admite un prompt por
/// peticion, asi que con wildcards el modulo genera las imagenes de una en una.
/// </summary>
public class WildcardsExtension : SdExtension
{
    public override string Id => "wildcards";
    public override string DisplayName => "Wildcards";
    public override string Description => "Sustituye __nombre__ por una linea al azar de wildcards/nombre.txt (distinta en cada imagen del lote).";

    public static readonly string DefaultFolder = Path.Combine(AppSettingsService.AppFolder, "imagegen", "wildcards");

    /// <summary>La carpeta elegida en los ajustes de instalacion, o la del Launcher por defecto.</summary>
    public static string Folder =>
        ImageGenSettings.Instance.WildcardsFolder is { Length: > 0 } custom ? custom : DefaultFolder;

    private static readonly Regex Token = new(@"__([A-Za-z0-9_\-/]+)__", RegexOptions.Compiled);

    private readonly TextBlock _info;

    public WildcardsExtension()
    {
        _info = new TextBlock
        {
            FontFamily = Res<System.Windows.Media.FontFamily>("Font.Mono"),
            FontSize = 10.5,
            Foreground = Res<System.Windows.Media.Brush>("Brush.TextSecondary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };

        EnsureSample();
        RefreshInfo();
    }

    /// <summary>Crea un ejemplo la primera vez para que la carpeta no aparezca vacia.</summary>
    private static void EnsureSample()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            if (Directory.EnumerateFiles(Folder, "*.txt", SearchOption.AllDirectories).Any()) return;

            File.WriteAllLines(Path.Combine(Folder, "colores.txt"), new[]
            {
                "# Un valor por linea. Usalo en el prompt como __colores__",
                "red", "blue", "emerald green", "golden", "violet"
            });
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo preparar la carpeta de wildcards", ex);
        }
    }

    private void RefreshInfo()
    {
        var count = Directory.Exists(Folder)
            ? Directory.EnumerateFiles(Folder, "*.txt", SearchOption.AllDirectories).Count()
            : 0;
        _info.Text = $"{count} archivo(s) en la carpeta de wildcards. Ejemplo: __colores__";
    }

    public override FrameworkElement BuildPanel()
    {
        var open = new Button
        {
            Content = "Abrir carpeta",
            Style = Res<Style>("SecondaryButtonStyle"),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        open.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(Folder);
                Process.Start(new ProcessStartInfo { FileName = Folder, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LogService.Error("No se pudo abrir la carpeta de wildcards", ex);
            }
        };

        var reload = new Button
        {
            Content = "Recontar",
            Style = Res<Style>("SecondaryButtonStyle"),
            Padding = new Thickness(10, 6, 10, 6)
        };
        reload.Click += (_, _) => RefreshInfo();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(open);
        buttons.Children.Add(reload);

        var panel = new StackPanel();
        panel.Children.Add(_info);
        panel.Children.Add(buttons);
        return panel;
    }

    /// <summary>True si el prompt usa algun wildcard cuyo archivo existe: entonces cada
    /// imagen del lote debe resolverse por separado.</summary>
    public override bool VariesPerImage(string prompt, string negativePrompt) =>
        HasResolvableToken(prompt) || HasResolvableToken(negativePrompt);

    private static bool HasResolvableToken(string text) =>
        Token.Matches(text).Any(m => File.Exists(PathFor(m.Groups[1].Value)));

    private static string PathFor(string name) =>
        name.Contains("..") ? "" : Path.Combine(Folder, name.Replace('/', Path.DirectorySeparatorChar) + ".txt");

    public override void Apply(SdRequest request)
    {
        request.Prompt = Expand(request.Prompt);
        request.NegativePrompt = Expand(request.NegativePrompt);
    }

    private static string Expand(string text)
    {
        // Varias pasadas para resolver wildcards que contienen otros wildcards.
        for (var i = 0; i < 5; i++)
        {
            var next = Token.Replace(text, m => Pick(m.Groups[1].Value) ?? m.Value);
            if (next == text) break;
            text = next;
        }

        return text;
    }

    private static string? Pick(string name)
    {
        if (name.Contains("..")) return null;

        try
        {
            var path = Path.Combine(Folder, name.Replace('/', Path.DirectorySeparatorChar) + ".txt");
            if (!File.Exists(path)) return null;

            var lines = File.ReadAllLines(path)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('#'))
                .ToList();

            return lines.Count == 0 ? null : lines[Random.Shared.Next(lines.Count)];
        }
        catch
        {
            return null;
        }
    }

    protected override JsonObject SaveCore() => new();

    protected override void LoadCore(JsonObject state)
    {
        // Sin ajustes propios: solo importa si esta activada.
    }
}
