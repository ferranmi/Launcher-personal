using System.Globalization;
using System.Text.RegularExpressions;

namespace Launcher.Modules.ImageGen.Autocomplete;

/// <summary>
/// Sugerencias de LoRA/LyCORIS: al escribir "&lt;" ofrece "&lt;lora:" y "&lt;lyco:"; tras
/// "&lt;lora:" lista los LoRAs que coinciden e inserta &lt;lora:nombre:peso&gt; con el peso
/// que haya en el campo "peso" del panel.
/// </summary>
public sealed class LoraAutocompleteProvider : IAutocompleteProvider
{
    private static readonly Regex Named =
        new(@"<(?:lora|lyco):([^>:\s]*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Opening =
        new(@"<([a-z]*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] Kinds = { "lora", "lyco" };

    private readonly Func<IReadOnlyList<string>> _names;
    private readonly Func<double> _weight;

    public LoraAutocompleteProvider(Func<IReadOnlyList<string>> names, Func<double> weight)
    {
        _names = names;
        _weight = weight;
    }

    public AutocompleteResult? GetSuggestions(string textBeforeCaret)
    {
        var named = Named.Match(textBeforeCaret);
        if (named.Success)
        {
            var query = named.Groups[1].Value;
            var weight = _weight().ToString("0.##", CultureInfo.InvariantCulture);

            var items = _names()
                .Where(n => n.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => !n.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .ThenBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .Take(40)
                .Select(name => new Suggestion
                {
                    Label = name,
                    Insert = $"<lora:{name}:{weight}>",
                    Detail = "LoRA",
                    Tint = SuggestionBrushes.Get("#FFD97BFF")
                })
                .ToList();

            return items.Count == 0 ? null : new AutocompleteResult(named.Length, items);
        }

        var opening = Opening.Match(textBeforeCaret);
        if (opening.Success)
        {
            var typed = opening.Groups[1].Value.ToLowerInvariant();

            var items = Kinds
                .Where(k => k.StartsWith(typed, StringComparison.Ordinal))
                .Select(k => new Suggestion
                {
                    Label = $"<{k}:",
                    Insert = $"<{k}:", // termina en ':' → el motor sigue con la lista de nombres
                    Detail = "LoRA / LyCORIS",
                    Tint = SuggestionBrushes.Get("#FFD97BFF")
                })
                .ToList();

            return items.Count == 0 ? null : new AutocompleteResult(opening.Length, items);
        }

        return null;
    }
}
