using Launcher.Core.Services;

namespace Launcher.Modules.Search;

/// <summary>Lanza todos los proveedores en paralelo y mezcla los resultados por puntuacion.</summary>
public class SearchService
{
    private readonly List<ISearchProvider> _providers = new()
    {
        new PathSearchProvider(),
        new GamesSearchProvider(),
        new CommandsSearchProvider(),
        new FoldersSearchProvider(),
        new AppsSearchProvider(),
        new FilesSearchProvider(),
        new WebSearchProvider()
    };

    public async Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        var tasks = _providers.Select(async p =>
        {
            try { return await p.SearchAsync(query, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Un proveedor roto no debe tumbar la barra entera.
                LogService.Error($"Proveedor de busqueda {p.Category} fallo", ex);
                return new List<SearchResult>();
            }
        });

        var all = await Task.WhenAll(tasks);

        return all.SelectMany(r => r)
                  .OrderByDescending(r => r.Score)
                  .Take(30)
                  .ToList();
    }
}
