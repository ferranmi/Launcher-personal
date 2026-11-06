using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Launcher.Core.Mvvm;

namespace Launcher.Modules.Models;

public class WildcardFile : ViewModelBase
{
    private int _lineCount;
    private int _modelRefCount;
    private DateTime _modified;
    private bool _wasChecked;
    private int _missing, _ambiguous, _malformed, _duplicates;

    /// <summary>Nombre como se usa en el prompt (__nombre__): ruta relativa sin .txt, con "/".</summary>
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string Root { get; set; } = "";

    /// <summary>Entradas reales: sin contar lineas en blanco ni comentarios (#).</summary>
    public int LineCount { get => _lineCount; set => SetField(ref _lineCount, value); }

    /// <summary>Etiquetas &lt;lora:...&gt; / &lt;lyco:...&gt; que contiene.</summary>
    public int ModelRefCount { get => _modelRefCount; set => SetField(ref _modelRefCount, value); }

    public DateTime Modified { get => _modified; set => SetField(ref _modified, value); }

    /// <summary>True si ya se ha ejecutado "Comprobar" sobre este archivo.</summary>
    public bool WasChecked { get => _wasChecked; private set => SetField(ref _wasChecked, value); }

    public bool HasProblems => _wasChecked && (_missing + _ambiguous + _malformed + _duplicates) > 0;

    public string CheckSummaryText
    {
        get
        {
            if (!_wasChecked) return "";

            var parts = new List<string>();
            if (_missing > 0) parts.Add($"{_missing} ausentes");
            if (_ambiguous > 0) parts.Add($"{_ambiguous} ambiguos");
            if (_malformed > 0) parts.Add($"{_malformed} errores");
            if (_duplicates > 0) parts.Add($"{_duplicates} duplicadas");

            return parts.Count == 0 ? "sin problemas" : string.Join("  \u00b7  ", parts);
        }
    }

    public void ApplyCheck(CheckSummary summary)
    {
        _missing = summary.Missing;
        _ambiguous = summary.Ambiguous;
        _malformed = summary.Malformed;
        _duplicates = summary.Duplicates;
        WasChecked = true;
        OnPropertyChanged(nameof(HasProblems));
        OnPropertyChanged(nameof(CheckSummaryText));
    }

    /// <summary>Olvida el resultado de la comprobacion (el archivo ha cambiado).</summary>
    public void ClearCheck()
    {
        _missing = _ambiguous = _malformed = _duplicates = 0;
        WasChecked = false;
        OnPropertyChanged(nameof(HasProblems));
        OnPropertyChanged(nameof(CheckSummaryText));
    }
}

public class WildcardReference
{
    public string Kind { get; set; } = "lora";
    public string ModelName { get; set; } = "";
    public string? Weight { get; set; }
    public int LineNumber { get; set; }
    public string Line { get; set; } = "";
}

/// <summary>Donde aparece un modelo: archivo de wildcard y numero de linea (1-based).</summary>
public class WildcardRefLocation
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public int LineNumber { get; set; }
    public string? Weight { get; set; }
}

/// <summary>
/// Lectura de wildcards de Dynamic Prompts. SOLO LECTURA por ahora: no hay ningun metodo que escriba.
/// La edicion (linea a linea, con copia de seguridad y confirmacion) llega en el paso 3.
/// </summary>
public sealed class WildcardService
{
    public static WildcardService Instance { get; } = new();

    private static readonly Regex ModelTag = new(
        @"<(?<kind>lora|lyco):(?<name>[^:>]+)(?::(?<weight>[^>]*))?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private Dictionary<string, List<WildcardRefLocation>>? _refIndex;

    private WildcardService() { }

    public Task<List<WildcardFile>> ListFilesAsync()
    {
        _refIndex = null; // refrescar el listado invalida el indice de referencias
        return Task.Run(ListFiles);
    }

    /// <summary>Igual que ListFilesAsync pero sin invalidar el indice de referencias (para refrescos periodicos de widgets).</summary>
    public Task<List<WildcardFile>> ListFilesQuietAsync() => Task.Run(ListFiles);

    private static IEnumerable<(string Root, string Path)> EnumerateWildcardPaths()
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };

        foreach (var root in ModelsSettings.Instance.WildcardFoldersEffective().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root)) continue;

            foreach (var path in Directory.EnumerateFiles(root, "*.txt", options))
            {
                yield return (root, path);
            }
        }
    }

    private static string NameOf(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/')[..^4]; // quita ".txt"

    private static List<WildcardFile> ListFiles()
    {
        var result = new List<WildcardFile>();

        foreach (var (root, path) in EnumerateWildcardPaths())
        {
            try
            {
                var entries = ReadLines(path).Where(IsEntry).ToList();

                result.Add(new WildcardFile
                {
                    Name = NameOf(root, path),
                    FullPath = path,
                    Root = root,
                    LineCount = entries.Count,
                    ModelRefCount = entries.Sum(l => ModelTag.Matches(l).Count),
                    Modified = File.GetLastWriteTime(path)
                });
            }
            catch
            {
                // Un archivo ilegible no debe tumbar el listado de los demas.
            }
        }

        return result.OrderBy(w => w.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Indice nombre de modelo -> donde aparece. Se construye una vez y se reutiliza hasta que
    /// se refresca el listado de wildcards (o force = true).</summary>
    public Task<Dictionary<string, List<WildcardRefLocation>>> GetReferenceIndexAsync(bool force = false)
    {
        if (!force && _refIndex != null) return Task.FromResult(_refIndex);

        return Task.Run(() =>
        {
            var index = new Dictionary<string, List<WildcardRefLocation>>(StringComparer.OrdinalIgnoreCase);

            foreach (var (root, path) in EnumerateWildcardPaths())
            {
                try
                {
                    var name = NameOf(root, path);

                    foreach (var reference in ExtractReferences(ReadLines(path)))
                    {
                        if (!index.TryGetValue(reference.ModelName, out var list))
                        {
                            list = new List<WildcardRefLocation>();
                            index[reference.ModelName] = list;
                        }

                        list.Add(new WildcardRefLocation
                        {
                            FileName = name,
                            FullPath = path,
                            LineNumber = reference.LineNumber,
                            Weight = reference.Weight
                        });
                    }
                }
                catch
                {
                    // Archivo ilegible: se omite.
                }
            }

            _refIndex = index;
            return index;
        });
    }

    private static bool IsEntry(string line) => IsEntryLine(line);

    /// <summary>Linea con contenido: ni vacia ni comentario (#).</summary>
    public static bool IsEntryLine(string line) =>
        !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#');

    /// <summary>Entradas y referencias a modelos de un conjunto de lineas (para refrescar el listado tras guardar).</summary>
    public static (int Entries, int ModelRefs) ComputeStats(IEnumerable<string> lines)
    {
        var entries = 0;
        var refs = 0;

        foreach (var line in lines)
        {
            if (!IsEntryLine(line)) continue;
            entries++;
            refs += ModelTag.Matches(line).Count;
        }

        return (entries, refs);
    }

    /// <summary>Descarta el indice de referencias (tras guardar un wildcard ya no es fiable).</summary>
    public void InvalidateReferenceIndex() => _refIndex = null;

    /// <summary>Lee un archivo tal cual esta, sin bloquearlo (A1111 puede tenerlo abierto).</summary>
    public static string[] ReadLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line) lines.Add(line);
        return lines.ToArray();
    }

    /// <summary>Etiquetas de modelo de un archivo, con su numero de linea (1-based).</summary>
    public static List<WildcardReference> ExtractReferences(IReadOnlyList<string> lines)
    {
        var refs = new List<WildcardReference>();

        for (var i = 0; i < lines.Count; i++)
        {
            if (!IsEntry(lines[i])) continue;

            foreach (Match match in ModelTag.Matches(lines[i]))
            {
                refs.Add(new WildcardReference
                {
                    Kind = match.Groups["kind"].Value.ToLowerInvariant(),
                    ModelName = match.Groups["name"].Value.Trim(),
                    Weight = match.Groups["weight"].Success ? match.Groups["weight"].Value.Trim() : null,
                    LineNumber = i + 1,
                    Line = lines[i]
                });
            }
        }

        return refs;
    }
}
