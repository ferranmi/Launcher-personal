using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

public class CivitaiException : Exception
{
    public HttpStatusCode? Status { get; }
    public CivitaiException(string message, HttpStatusCode? status = null, Exception? inner = null)
        : base(message, inner) => Status = status;
}

/// <summary>
/// Cliente de la API publica de Civitai (solo lectura). Notas verificadas en la documentacion oficial:
/// la busqueda por texto (query) exige paginacion por cursor (nunca se envia page); page*limit &gt; 1000 da 429;
/// ante un 429 se respeta Retry-After; la API Key viaja en cabecera Authorization: Bearer (no en la URL, para
/// que no quede en registros). Las colecciones publicas solo exponen metadatos: la API NO lista su contenido.
/// </summary>
public sealed class CivitaiService
{
    public static CivitaiService Instance { get; } = new();

    public const string BaseUrl = "https://civitai.com";
    public const string CredentialKey = "Launcher.Civitai.ApiKey";

    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private CivitaiService() { }

    // ----- API Key (Administrador de credenciales de Windows, no JSON) -----

    public string? GetApiKey()
    {
        var stored = CredentialStore.Read(CredentialKey);
        return stored is { Password.Length: > 0 } ? stored.Value.Password : null;
    }

    public bool HasApiKey => GetApiKey() != null;

    public bool SaveApiKey(string key) =>
        string.IsNullOrWhiteSpace(key) ? DeleteApiKey() : CredentialStore.Save(CredentialKey, "civitai", key.Trim());

    public bool DeleteApiKey()
    {
        CredentialStore.Delete(CredentialKey);
        return true;
    }

    // ----- Peticiones -----

    private static HttpRequestMessage Build(string pathAndQuery, bool withKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + pathAndQuery);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (withKey && Instance.GetApiKey() is { } key)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        return request;
    }

    private async Task<T> GetAsync<T>(string pathAndQuery, CancellationToken ct, bool withKey = true)
    {
        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var request = Build(pathAndQuery, withKey);
                response = await Http.SendAsync(request, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CivitaiException("No se pudo conectar con Civitai. Comprueba tu conexion.", null, ex);
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt == 0)
                {
                    var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                    if (wait <= TimeSpan.FromSeconds(30))
                    {
                        await Task.Delay(wait, ct);
                        continue;
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    var message = response.StatusCode switch
                    {
                        HttpStatusCode.TooManyRequests => "Civitai limita las peticiones. Espera un momento y reintenta.",
                        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Civitai rechazo la peticion (API Key ausente o invalida).",
                        HttpStatusCode.NotFound => "No se ha encontrado en Civitai.",
                        HttpStatusCode.BadRequest => "Civitai rechazo los parametros de la busqueda.",
                        _ => $"Civitai respondio con error {(int)response.StatusCode}."
                    };
                    throw new CivitaiException(message, response.StatusCode);
                }

                try
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    var result = await JsonSerializer.DeserializeAsync<T>(stream, Json, ct);
                    return result ?? throw new CivitaiException("Respuesta vacia de Civitai.");
                }
                catch (JsonException ex)
                {
                    throw new CivitaiException("Civitai devolvio una respuesta que no se ha podido leer.", null, ex);
                }
            }
        }
    }

    private sealed class RawPage<T>
    {
        public List<T> Items { get; set; } = new();
        public RawMeta? Metadata { get; set; }
    }

    private sealed class RawMeta
    {
        public JsonElement? NextCursor { get; set; }
    }

    private static string? CursorOf(RawMeta? meta) =>
        meta?.NextCursor is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } element
            ? element.ToString()
            : null;

    /// <summary>Busca modelos. type: valor de ModelType de Civitai (LORA, LoCon, Checkpoint...) o null para todos.</summary>
    public async Task<CivitaiPage<CivitaiModel>> SearchModelsAsync(string? query, string? type, string sort,
        bool nsfw, string? cursor, CancellationToken ct = default)
    {
        var parts = new List<string> { "limit=24", $"sort={Uri.EscapeDataString(sort)}", $"nsfw={(nsfw ? "true" : "false")}" };
        if (!string.IsNullOrWhiteSpace(query)) parts.Add($"query={Uri.EscapeDataString(query.Trim())}");
        if (!string.IsNullOrWhiteSpace(type)) parts.Add($"types={Uri.EscapeDataString(type)}");
        if (!string.IsNullOrWhiteSpace(cursor)) parts.Add($"cursor={Uri.EscapeDataString(cursor)}");

        var page = await GetAsync<RawPage<CivitaiModel>>("/api/v1/models?" + string.Join("&", parts), ct);
        return new CivitaiPage<CivitaiModel> { Items = page.Items, NextCursor = CursorOf(page.Metadata) };
    }

    public Task<CivitaiModel> GetModelAsync(long id, CancellationToken ct = default) =>
        GetAsync<CivitaiModel>($"/api/v1/models/{id}", ct);

    public Task<CivitaiVersion> GetVersionAsync(long id, CancellationToken ct = default) =>
        GetAsync<CivitaiVersion>($"/api/v1/model-versions/{id}", ct);

    public async Task<CivitaiPage<CivitaiCollection>> SearchCollectionsAsync(string? query, string? cursor, CancellationToken ct = default)
    {
        var parts = new List<string> { "limit=30", "sort=Newest" };
        if (!string.IsNullOrWhiteSpace(query)) parts.Add($"query={Uri.EscapeDataString(query.Trim())}");
        if (!string.IsNullOrWhiteSpace(cursor)) parts.Add($"cursor={Uri.EscapeDataString(cursor)}");

        var page = await GetAsync<RawPage<CivitaiCollection>>("/api/v1/collections?" + string.Join("&", parts), ct);
        return new CivitaiPage<CivitaiCollection> { Items = page.Items, NextCursor = CursorOf(page.Metadata) };
    }

    /// <summary>Comprueba la API Key: el parametro favorites exige token, asi que un 401/403 indica clave ausente o invalida.</summary>
    public async Task<(bool Ok, string Message)> TestConnectionAsync(CancellationToken ct = default)
    {
        if (!HasApiKey) return (false, "No hay API Key guardada. Sin ella, la mayoria de descargas devuelven 401.");

        try
        {
            await GetAsync<JsonElement>("/api/v1/models?limit=1&favorites=true", ct);
            return (true, "Conexion correcta: la API Key es valida.");
        }
        catch (CivitaiException ex) when (ex.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return (false, "Civitai rechazo la API Key. Genera una nueva en tu cuenta de Civitai.");
        }
        catch (CivitaiException ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Extrae ids de modelo / version de URLs de Civitai o numeros sueltos (uno por linea).</summary>
    public static List<(long ModelId, long? VersionId)> ParseLinks(string text)
    {
        var result = new List<(long, long?)>();
        var seen = new HashSet<(long, long?)>();

        foreach (var raw in text.Split(new[] { '\r', '\n', ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            long? versionId = null;
            long modelId = 0;

            var versionMatch = System.Text.RegularExpressions.Regex.Match(raw, @"modelVersionId=(\d+)");
            if (versionMatch.Success) versionId = long.Parse(versionMatch.Groups[1].Value);

            var modelMatch = System.Text.RegularExpressions.Regex.Match(raw, @"/models/(\d+)");
            if (modelMatch.Success) modelId = long.Parse(modelMatch.Groups[1].Value);
            else if (long.TryParse(raw, out var plain)) modelId = plain;

            if (modelId > 0 && seen.Add((modelId, versionId))) result.Add((modelId, versionId));
        }

        return result;
    }
}
