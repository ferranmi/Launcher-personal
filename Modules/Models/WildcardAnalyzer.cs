using System.Globalization;
using System.Text.RegularExpressions;

namespace Launcher.Modules.Models;

public enum LineStatus { None, Ok, Unknown, Ambiguous, Missing, Malformed }

public class LineCheck
{
    public LineStatus Status { get; set; }

    /// <summary>La misma linea (ignorando espacios al borde y mayusculas) ya aparece antes en el archivo.</summary>
    public bool IsDuplicate { get; set; }

    public string? Detail { get; set; }
}

public readonly record struct CheckSummary(int Missing, int Ambiguous, int Malformed, int Duplicates, int Unknown)
{
    public bool HasProblems => Missing + Ambiguous + Malformed + Duplicates > 0;
}

/// <summary>Cuantos archivos LoRA/LyCORIS hay por nombre. Se construye una vez y sirve para analizar muchas lineas.</summary>
public sealed class ModelLookup
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

    public int Total { get; }

    public ModelLookup(IEnumerable<ModelEntry> models)
    {
        var total = 0;
        foreach (var model in models)
        {
            if (model.Type == ModelType.Checkpoint) continue; // los checkpoints no se llaman con etiqueta

            _counts[model.Name] = _counts.GetValueOrDefault(model.Name) + 1;
            total++;
        }
        Total = total;
    }

    public int Count(string name) => _counts.GetValueOrDefault(name);
}

/// <summary>Comprobacion de solo lectura: no modifica nada, solo describe el estado de cada linea.</summary>
public static class WildcardAnalyzer
{
    private static readonly Regex Tag = new(
        @"<(?<kind>lora|lyco):(?<name>[^:>]+)(?::(?<weight>[^>]*))?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Opening = new(@"<\s*(lora|lyco)\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static int Rank(LineStatus status) => status switch
    {
        LineStatus.Ok => 1,
        LineStatus.Unknown => 2,
        LineStatus.Ambiguous => 3,
        LineStatus.Missing => 4,
        LineStatus.Malformed => 5,
        _ => 0
    };

    private static bool IsNumber(string text) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    public static LineCheck[] Analyze(IReadOnlyList<string> lines, ModelLookup lookup)
    {
        var result = new LineCheck[lines.Count];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < lines.Count; i++)
        {
            var check = new LineCheck();
            result[i] = check;

            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;

            if (!seen.Add(line.Trim())) check.IsDuplicate = true;

            var worst = LineStatus.None;
            string? detail = null;

            // Lo que queda tras quitar las etiquetas bien formadas no deberia contener otra apertura de etiqueta.
            if (Opening.IsMatch(Tag.Replace(line, "")))
            {
                worst = LineStatus.Malformed;
                detail = "Etiqueta <lora:...> mal formada (falta el nombre o el '>').";
            }

            foreach (Match match in Tag.Matches(line))
            {
                var name = match.Groups["name"].Value.Trim();
                var weight = match.Groups["weight"].Success ? match.Groups["weight"].Value : null;

                LineStatus status;
                string? statusDetail = null;

                if (weight != null && !IsNumber(weight.Split(':')[0]))
                {
                    status = LineStatus.Malformed;
                    statusDetail = $"Peso no valido en '{name}': {weight}";
                }
                else if (lookup.Total == 0)
                {
                    status = LineStatus.Unknown;
                    statusDetail = "Aun no hay inventario de modelos.";
                }
                else
                {
                    var count = lookup.Count(name);
                    if (count == 0)
                    {
                        status = LineStatus.Missing;
                        statusDetail = $"No se encuentra '{name}' en la biblioteca.";
                    }
                    else if (count > 1)
                    {
                        status = LineStatus.Ambiguous;
                        statusDetail = $"Hay {count} archivos llamados '{name}'.";
                    }
                    else
                    {
                        status = LineStatus.Ok;
                    }
                }

                if (Rank(status) > Rank(worst))
                {
                    worst = status;
                    detail = statusDetail ?? detail;
                }
            }

            check.Status = worst;
            check.Detail = detail;
        }

        return result;
    }

    public static CheckSummary Summarize(IEnumerable<LineCheck> checks)
    {
        int missing = 0, ambiguous = 0, malformed = 0, duplicates = 0, unknown = 0;

        foreach (var check in checks)
        {
            switch (check.Status)
            {
                case LineStatus.Missing: missing++; break;
                case LineStatus.Ambiguous: ambiguous++; break;
                case LineStatus.Malformed: malformed++; break;
                case LineStatus.Unknown: unknown++; break;
            }

            if (check.IsDuplicate) duplicates++;
        }

        return new CheckSummary(missing, ambiguous, malformed, duplicates, unknown);
    }
}

/// <summary>Una linea del tipo "&lt;lora:nombre:peso&gt;, texto" descompuesta para editarla por campos.
/// Se conserva el espaciado original (pre y sep) para no reformatear lo que el usuario no toca.</summary>
public sealed class ParsedModelLine
{
    public string Pre { get; set; } = "";
    public string Kind { get; set; } = "lora";
    public string Name { get; set; } = "";
    public string Weight { get; set; } = "";
    public string Sep { get; set; } = "";
    public string Rest { get; set; } = "";
}

public static class ModelLine
{
    // Solo reconoce la forma sencilla <lora:nombre:peso>. Etiquetas con parametros extra (p.ej. lbw)
    // no encajan y la linea se edita solo como texto: asi nunca se pierden esos parametros.
    private static readonly Regex Pattern = new(
        @"^(?<pre>\s*)<(?<kind>lora|lyco):(?<name>[^:>]+)(?::(?<weight>[^>:]*))?>(?<sep>\s*,?\s*)(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ParsedModelLine? Parse(string line)
    {
        var match = Pattern.Match(line);
        if (!match.Success) return null;

        return new ParsedModelLine
        {
            Pre = match.Groups["pre"].Value,
            Kind = match.Groups["kind"].Value,
            Name = match.Groups["name"].Value,
            Weight = match.Groups["weight"].Success ? match.Groups["weight"].Value : "",
            Sep = match.Groups["sep"].Value,
            Rest = match.Groups["rest"].Value
        };
    }

    public static string Build(ParsedModelLine p)
    {
        var tag = p.Weight.Length > 0 ? $"<{p.Kind}:{p.Name}:{p.Weight}>" : $"<{p.Kind}:{p.Name}>";

        var sep = p.Sep;
        if (p.Rest.Length == 0) sep = "";
        else if (sep.Length == 0) sep = ", ";

        return p.Pre + tag + sep + p.Rest;
    }
}
