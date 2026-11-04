using System.Text.RegularExpressions;

namespace Launcher.Modules.Models;

/// <summary>Reescribe SOLO el nombre dentro de las etiquetas &lt;lora:nombre:peso&gt; / &lt;lyco:...&gt; cuyo nombre
/// coincide; el resto de la linea (pesos, trigger words, espaciado) queda intacto. Ignora comentarios.</summary>
public static class ReferenceRewriter
{
    private static readonly Regex Tag = new(
        @"<(?<kind>lora|lyco):(?<name>[^:>]+)(?::(?<weight>[^>]*))?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <param name="map">nombre antiguo -> nombre nuevo. Debe ser insensible a mayusculas.</param>
    public static string Rewrite(string line, IReadOnlyDictionary<string, string> map)
    {
        if (!WildcardService.IsEntryLine(line)) return line;

        return Tag.Replace(line, match =>
        {
            var group = match.Groups["name"];
            if (!map.TryGetValue(group.Value.Trim(), out var replacement)) return match.Value;

            var start = group.Index - match.Index;
            return match.Value[..start] + replacement + match.Value[(start + group.Length)..];
        });
    }
}

/// <summary>Sugerencias de modelos con nombre parecido a una referencia ausente. Solo sugiere: nunca se
/// aplica nada sin que el usuario lo elija.</summary>
public static class SimilarNames
{
    private static string Normalize(string text) =>
        new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static HashSet<string> Tokens(string text) =>
        Regex.Split(text.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(t => t.Length >= 3)
            .ToHashSet();

    public static List<ModelEntry> Find(string name, IEnumerable<ModelEntry> models, int max = 6)
    {
        var normalized = Normalize(name);
        if (normalized.Length == 0) return new List<ModelEntry>();

        var tokens = Tokens(name);

        return models
            .Where(m => m.Type != ModelType.Checkpoint)
            .Select(m =>
            {
                var other = Normalize(m.Name);
                var score = 0;

                if (other == normalized) score = 100;
                else if (normalized.Length >= 4 && other.Length >= 4 &&
                         (other.Contains(normalized) || normalized.Contains(other))) score = 60;
                else score = Tokens(m.Name).Count(tokens.Contains) * 20;

                return (Model: m, Score: score);
            })
            .Where(x => x.Score >= 20)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Model.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => x.Model)
            .Take(max)
            .ToList();
    }
}
