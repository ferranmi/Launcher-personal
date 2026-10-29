using System.Windows.Media;

namespace Launcher.Modules.ImageGen.Autocomplete;

/// <summary>Una opcion de la lista de sugerencias.</summary>
public sealed class Suggestion
{
    /// <summary>Texto mostrado.</summary>
    public required string Label { get; init; }

    /// <summary>Texto que sustituye a lo escrito al elegir la opcion.</summary>
    public required string Insert { get; init; }

    /// <summary>Se anade tras Insert (p.ej. ", "), salvo que ya haya una coma justo despues.</summary>
    public string Suffix { get; init; } = "";

    /// <summary>Texto tenue a la derecha (recuento, tipo...).</summary>
    public string Detail { get; init; } = "";

    /// <summary>Tooltip opcional.</summary>
    public string? Note { get; init; }

    /// <summary>Color del texto (p.ej. por categoria de tag).</summary>
    public Brush Tint { get; init; } = SuggestionBrushes.Default;
}

/// <summary>Resultado de un proveedor: cuantos caracteres antes del cursor se sustituyen y las opciones.</summary>
public sealed record AutocompleteResult(int ReplaceLength, IReadOnlyList<Suggestion> Items);

/// <summary>Fuente de sugerencias (wildcards, tags, LoRAs...). Recibe el texto anterior al cursor
/// y devuelve null si no aplica. Se ejecuta en el hilo de la UI en cada tecla: debe ser rapido.</summary>
public interface IAutocompleteProvider
{
    AutocompleteResult? GetSuggestions(string textBeforeCaret);
}

/// <summary>Pinceles congelados y cacheados para colorear sugerencias.</summary>
public static class SuggestionBrushes
{
    private static readonly Dictionary<string, Brush> Cache = new();

    public static readonly Brush Default = Get("#FFE6F2EA");

    public static Brush Get(string hex)
    {
        if (Cache.TryGetValue(hex, out var cached)) return cached;

        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        Cache[hex] = brush;
        return brush;
    }
}
