using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
using Launcher.Core.Services;
using Launcher.Modules.Commands;
using Launcher.Modules.Games;

namespace Launcher.Modules.Search;

/// <summary>Juegos de la biblioteca (no ocultos).</summary>
public class GamesSearchProvider : ISearchProvider
{
    public string Category => "Juego";

    public Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        var library = GameLibraryService.Instance;
        var results = new List<SearchResult>();

        foreach (var game in library.Games.Where(g => !g.Hidden).ToList())
        {
            var score = Fuzzy.Score(game.Name, query);
            if (score == 0) continue;

            var g = game;
            results.Add(new SearchResult
            {
                Title = g.Name,
                Subtitle = g.Subtitle,
                Icon = "\uE7FC",
                Category = Category,
                Score = score + 5, // los juegos pesan un poco mas: es el uso principal del launcher
                Execute = () => library.Launch(g)
            });
        }

        return Task.FromResult(results);
    }
}

/// <summary>Accesos directos del modulo Comandos.</summary>
public class CommandsSearchProvider : ISearchProvider
{
    public string Category => "Comando";

    public Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        var service = new CommandsService(); // relee commands.json: siempre al dia
        var results = new List<SearchResult>();

        foreach (var cmd in service.Commands)
        {
            var score = Fuzzy.Score(cmd.Name, query);
            if (score == 0) score = Fuzzy.Score(cmd.Description, query) / 2;
            if (score == 0) continue;

            var c = cmd;
            results.Add(new SearchResult
            {
                Title = c.Name,
                Subtitle = c.Description,
                Icon = string.IsNullOrWhiteSpace(c.Icon) ? "\uE756" : c.Icon,
                Category = Category,
                Score = score + 3,
                Execute = () => service.Launch(c)
            });
        }

        return Task.FromResult(results);
    }
}

/// <summary>Carpetas del modulo Carpetas.</summary>
public class FoldersSearchProvider : ISearchProvider
{
    public string Category => "Carpeta";

    public Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        var service = new FolderShortcutsService();
        var results = new List<SearchResult>();

        foreach (var folder in service.Folders.Where(f => !f.Hidden))
        {
            var score = Fuzzy.Score(folder.Name, query);
            if (score == 0) continue;

            var f = folder;
            results.Add(new SearchResult
            {
                Title = f.Name,
                Subtitle = f.Path,
                Icon = f.Icon,
                Category = Category,
                Score = score,
                Execute = () => service.Open(f)
            });
        }

        return Task.FromResult(results);
    }
}

/// <summary>Aplicaciones instaladas: escanea los accesos directos del menu Inicio (una vez, en cache).</summary>
public class AppsSearchProvider : ISearchProvider
{
    public string Category => "App";

    private List<(string Name, string Path)>? _cache;

    private static List<(string, string)> Scan()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
        };

        var list = new List<(string, string)>();
        foreach (var root in roots)
        {
            var programs = Path.Combine(root, "Programs");
            if (!Directory.Exists(programs)) continue;

            try
            {
                foreach (var lnk in Directory.EnumerateFiles(programs, "*.lnk", SearchOption.AllDirectories))
                {
                    list.Add((Path.GetFileNameWithoutExtension(lnk), lnk));
                }
            }
            catch (Exception ex)
            {
                LogService.Error("No se pudo escanear el menu Inicio", ex);
            }
        }

        return list.GroupBy(x => x.Item1, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
    }

    public Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct) => Task.Run(() =>
    {
        _cache ??= Scan();
        var results = new List<SearchResult>();

        foreach (var (name, path) in _cache)
        {
            var score = Fuzzy.Score(name, query);
            if (score == 0) continue;

            var p = path;
            results.Add(new SearchResult
            {
                Title = name,
                Subtitle = "Aplicacion",
                Icon = "\uE71D",
                Category = Category,
                Score = score,
                Execute = () => Process.Start(new ProcessStartInfo { FileName = p, UseShellExecute = true })
            });
        }

        return results;
    }, ct);
}

/// <summary>Archivos y carpetas del indice de Windows Search (el mismo del menu Inicio). Enter los abre con su
/// programa; Ctrl+Enter los muestra en el Explorador. Si el indice no esta disponible, devuelve vacio.</summary>
public class FilesSearchProvider : ISearchProvider
{
    public string Category => "Archivo";

    public Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct) => Task.Run(() =>
    {
        var results = new List<SearchResult>();
        if (query.Length < 3) return results; // con 1-2 letras devolveria ruido

        try
        {
            var safe = query.Replace("'", "''").Replace("[", "").Replace("%", "").Replace("_", "");

            // Sin SCOPE: busca en todo lo que Windows tenga indexado (no solo el perfil); se descartan las
            // carpetas internas de aplicaciones (AppData), que llenarian los resultados de cache.
            var sql =
                "SELECT TOP 12 System.ItemName, System.ItemPathDisplay FROM SystemIndex " +
                $"WHERE System.ItemName LIKE '%{safe}%' AND NOT System.ItemPathDisplay LIKE '%\\AppData\\%' " +
                "ORDER BY System.DateModified DESC";

            using var conn = new OleDbConnection("Provider=Search.CollatorDSO;Extended Properties='Application=Windows'");
            conn.Open();
            using var cmd = new OleDbCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                ct.ThrowIfCancellationRequested();

                if (reader.IsDBNull(0) || reader.IsDBNull(1)) continue;

                var name = reader.GetString(0);
                var path = reader.GetString(1);

                // El indice puede tener entradas de archivos ya borrados o que no son rutas reales.
                var isFolder = Directory.Exists(path);
                if (!isFolder && !File.Exists(path)) continue;

                results.Add(new SearchResult
                {
                    Title = name,
                    Subtitle = path,
                    Icon = isFolder ? "\uE8B7" : "\uE7C3",
                    Category = isFolder ? "Carpeta" : Category,
                    Score = Fuzzy.Score(name, query) + 1,
                    Execute = () => PathOpener.Open(path),
                    Reveal = () => PathOpener.Reveal(path)
                });
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LogService.Error("Busqueda de archivos fallo", ex);
        }

        return results;
    }, ct);
}

/// <summary>Si lo escrito parece una ruta (C:\..., \\servidor, ~\..., %USERPROFILE%), la ofrece directamente
/// y lista lo que contiene o lo que empieza igual: sirve para abrir cualquier archivo o carpeta, este indexado o no.</summary>
public class PathSearchProvider : ISearchProvider
{
    public string Category => "Ruta";

    public Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct) => Task.Run(() =>
    {
        var results = new List<SearchResult>();

        var text = Environment.ExpandEnvironmentVariables(query.Trim().Trim('"'));
        if (text.StartsWith('~'))
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            text = Path.Combine(profile, text.TrimStart('~').TrimStart('\\', '/'));
        }

        var looksLikePath = (text.Length >= 2 && text[1] == ':' && char.IsLetter(text[0])) || text.StartsWith(@"\\");
        if (!looksLikePath) return results;

        try
        {
            var trimmed = text.Length > 3 ? text.TrimEnd('\\', '/') : text; // no quitar la barra de "C:\"

            if (Directory.Exists(trimmed) || File.Exists(trimmed)) results.Add(Make(trimmed, 130));

            // Contenido de la carpeta, o elementos de la carpeta padre que empiezan por lo ya escrito.
            var isDir = Directory.Exists(trimmed);
            var dir = isDir ? trimmed : Path.GetDirectoryName(trimmed);
            var prefix = isDir ? "" : Path.GetFileName(trimmed);

            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                var entries = Directory.EnumerateFileSystemEntries(dir)
                    .Where(e => !string.Equals(e, trimmed, StringComparison.OrdinalIgnoreCase))
                    .Where(e => prefix.Length == 0 ||
                                Path.GetFileName(e).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Take(8);

                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    results.Add(Make(entry, 100));
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException or NotSupportedException)
        {
            // Carpeta sin permisos o ruta no valida mientras se escribe: simplemente no hay sugerencias.
        }

        return results;
    }, ct);

    private static SearchResult Make(string path, int score)
    {
        var isDir = Directory.Exists(path);
        var name = Path.GetFileName(path.TrimEnd('\\', '/'));

        return new SearchResult
        {
            Title = name.Length > 0 ? name : path,
            Subtitle = path,
            Icon = isDir ? "\uE8B7" : "\uE7C3",
            Category = isDir ? "Carpeta" : "Archivo",
            Score = score,
            Execute = () => PathOpener.Open(path),
            Reveal = () => PathOpener.Reveal(path)
        };
    }
}

/// <summary>Siempre ofrece "buscar en la web" al final; si parece una URL, la abre directamente.</summary>
public class WebSearchProvider : ISearchProvider
{
    public string Category => "Web";

    public Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        var looksLikeUrl = !query.Contains(' ') && query.Contains('.') && !query.EndsWith('.');

        var result = looksLikeUrl
            ? new SearchResult
            {
                Title = $"Abrir {query}",
                Subtitle = "Direccion web",
                Icon = "\uE774",
                Category = Category,
                Score = 90,
                Execute = () => OpenUrl(query.Contains("://") ? query : "https://" + query)
            }
            : new SearchResult
            {
                Title = $"Buscar \"{query}\" en la web",
                Subtitle = "DuckDuckGo",
                Icon = "\uE774",
                Category = Category,
                Score = 1, // siempre al final
                Execute = () => OpenUrl("https://duckduckgo.com/?q=" + Uri.EscapeDataString(query))
            };

        return Task.FromResult(new List<SearchResult> { result });
    }

    private static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
}
