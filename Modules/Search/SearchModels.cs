using System.IO;

namespace Launcher.Modules.Search;

/// <summary>Un resultado de la barra de busqueda. Execute es lo que ocurre al pulsar Enter.</summary>
public class SearchResult
{
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Icon { get; init; } = "\uE721";
    public string Category { get; init; } = "";
    public int Score { get; set; }
    public Action Execute { get; init; } = () => { };

    /// <summary>Accion secundaria (Ctrl+Enter): para archivos y carpetas, mostrarlos en el Explorador.</summary>
    public Action? Reveal { get; init; }
}

/// <summary>Abrir / mostrar archivos y carpetas igual que lo haria el Explorador de Windows.</summary>
public static class PathOpener
{
    /// <summary>Abre con el programa asociado (archivos) o en el Explorador (carpetas).</summary>
    public static void Open(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
            throw new FileNotFoundException("La ruta ya no existe", path);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    /// <summary>Abre la carpeta que contiene el elemento, con el elemento seleccionado.</summary>
    public static void Reveal(string path)
    {
        if (Directory.Exists(path))
        {
            Open(path);
            return;
        }

        if (!File.Exists(path))
            throw new FileNotFoundException("La ruta ya no existe", path);

        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
    }
}

/// <summary>Fuente de resultados. Cada una busca en lo suyo; SearchService las lanza en paralelo.</summary>
public interface ISearchProvider
{
    string Category { get; }
    Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct);
}

public static class Fuzzy
{
    /// <summary>0 = no coincide. Mayor = mejor: exacto > empieza por > inicio de palabra > contiene > todas las palabras.</summary>
    public static int Score(string? text, string query)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        if (text.Equals(query, StringComparison.OrdinalIgnoreCase)) return 120;
        if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 100;

        var idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx > 0) return char.IsLetterOrDigit(text[idx - 1]) ? 60 : 80;

        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1 && words.All(w => text.Contains(w, StringComparison.OrdinalIgnoreCase))) return 50;

        return 0;
    }
}
