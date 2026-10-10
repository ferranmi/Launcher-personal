using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Launcher.Core.Services;

/// <summary>
/// Busca la portada de un juego usando la API publica (sin clave) de busqueda de
/// la Steam Store, y descarga la carpeta vertical estandar de la ficha de Steam
/// (funciona incluso para juegos que no son de Steam, mientras el nombre coincida
/// con algo publicado ahi). Se guarda en cache local para no volver a descargarla.
/// </summary>
public class CoverArtService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static readonly string CoversFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Launcher", "covers");

        /// <summary>Ruta de la portada en cache si ya existe y no esta vacia; si no, null.</summary>
    private static string? CachedPath(string saveAsId)
    {
        var path = Path.Combine(CoversFolder, $"{saveAsId}.jpg");
        return File.Exists(path) && new FileInfo(path).Length > 0 ? path : null;
    }

    /// <summary>
    /// Busca por nombre en la Steam Store y descarga la portada del primer resultado.
    /// Si ya hay una descargada con ese id, la reutiliza (force: true la vuelve a buscar).
    /// </summary>
    public async Task<string?> SearchAndDownloadAsync(string gameName, string saveAsId, bool force = false)
    {
        if (!force && CachedPath(saveAsId) is { } cached) return cached;

        try
        {
            var query = Uri.EscapeDataString(gameName);
            var searchUrl = $"https://store.steampowered.com/api/storesearch/?term={query}&cc=us&l=en";

            var json = await Http.GetStringAsync(searchUrl);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            {
                return null;
            }

            var appId = items[0].GetProperty("id").GetInt32();
            return await DownloadByAppIdAsync(appId, saveAsId, force);
        }
        catch
        {
            // Sin conexion, API caida, o respuesta inesperada: simplemente no hay portada.
            return null;
        }
    }

    /// <summary>Descarga la portada de un appid de Steam conocido; reutiliza la de cache salvo force.</summary>
    public async Task<string?> DownloadByAppIdAsync(int appId, string saveAsId, bool force = false)
    {
        if (!force && CachedPath(saveAsId) is { } cached) return cached;

        try
        {
            var coverUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/library_600x900.jpg";
            var bytes = await Http.GetByteArrayAsync(coverUrl);

            Directory.CreateDirectory(CoversFolder);
            var path = Path.Combine(CoversFolder, $"{saveAsId}.jpg");
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch
        {
            return null;
        }
    }
}
