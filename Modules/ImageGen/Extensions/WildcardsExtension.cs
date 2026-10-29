using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Launcher.Core.Services;
using Launcher.Modules.ImageGen.Autocomplete;

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

    private readonly WildcardAutocompleteProvider _autocomplete = new();

    public override IAutocompleteProvider? AutocompleteProvider => _autocomplete;

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


/// <summary>
/// Sugiere wildcards al escribir "__x" en el prompt: lista los .txt de la carpeta de wildcards
/// (con subcarpetas) cuyo nombre contiene lo escrito, con el numero de valores y una muestra
/// en el tooltip. Al elegir uno inserta __nombre__ completo.
/// </summary>
public sealed class WildcardAutocompleteProvider : IAutocompleteProvider
{
    // "__" que no va pegado a una palabra, seguido de lo escrito hasta ahora.
    private static readonly Regex Open = new(@"(?<![A-Za-z0-9])__([A-Za-z0-9_\-/]*)$", RegexOptions.Compiled);

    private readonly Dictionary<string, (DateTime Stamp, int Count, string Preview)> _info = new();
    private List<string> _names = new();
    private string _cachedFolder = "";
    private DateTime _cachedAt = DateTime.MinValue;

    public AutocompleteResult? GetSuggestions(string textBeforeCaret)
    {
        var match = Open.Match(textBeforeCaret);
        if (!match.Success) return null;

        var query = match.Groups[1].Value;
        if (query.EndsWith("__", StringComparison.Ordinal)) return null; // wildcard ya cerrado

        var items = GetNames()
            .Where(n => n.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => !n.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .Take(40)
            .Select(name =>
            {
                var (count, preview) = Info(name);
                return new Suggestion
                {
                    Label = name,
                    Insert = $"__{name}__",
                    Detail = count == 1 ? "1 valor" : $"{count} valores",
                    Note = preview,
                    Tint = SuggestionBrushes.Get("#FF7FD6A8")
                };
            })
            .ToList();

        return items.Count == 0 ? null : new AutocompleteResult(match.Length, items);
    }

    private List<string> GetNames()
    {
        var folder = WildcardsExtension.Folder;

        // La lista se relee como mucho cada 3 s: tecleando no se recorre el disco en cada tecla.
        if (folder == _cachedFolder && DateTime.UtcNow - _cachedAt < TimeSpan.FromSeconds(3)) return _names;

        _cachedFolder = folder;
        _cachedAt = DateTime.UtcNow;

        try
        {
            _names = Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, "*.txt", SearchOption.AllDirectories)
                    .Select(f => Path.ChangeExtension(Path.GetRelativePath(folder, f), null)!.Replace('\\', '/'))
                    .ToList()
                : new List<string>();
        }
        catch
        {
            _names = new List<string>();
        }

        return _names;
    }

    private (int Count, string Preview) Info(string name)
    {
        try
        {
            var path = Path.Combine(WildcardsExtension.Folder, name.Replace('/', Path.DirectorySeparatorChar) + ".txt");
            var stamp = File.GetLastWriteTimeUtc(path);

            if (_info.TryGetValue(path, out var cached) && cached.Stamp == stamp) return (cached.Count, cached.Preview);

            // Se lee un maximo de 5000 valores: basta para contar y mostrar una muestra.
            var values = File.ReadLines(path)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('#'))
                .Take(5000)
                .ToList();

            var preview = string.Join("\n", values.Take(8)) + (values.Count > 8 ? "\n..." : "");
            _info[path] = (stamp, values.Count, preview);
            return (values.Count, preview);
        }
        catch
        {
            return (0, "");
        }
    }
}
