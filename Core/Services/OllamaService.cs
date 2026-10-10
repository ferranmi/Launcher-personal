using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Launcher.Core.Models;

namespace Launcher.Core.Services;

/// <summary>
/// Cliente para la API local de Ollama (http://localhost:11434). Usa streaming
/// NDJSON via /api/chat para que la respuesta aparezca token a token en vez de
/// esperar a que el modelo termine entero.
/// </summary>
public class OllamaService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private const string BaseUrl = "http://localhost:11434";

    public string Model { get; set; } = "qwen3.5:4b";

    public async IAsyncEnumerable<string> StreamChatAsync(
        List<AssistantMessage> history,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new
        {
            model = Model,
            messages = history.Select(m => new { role = m.Role, content = m.Content }),
            stream = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/chat")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        // Ollama devuelve un objeto JSON por linea (no es un array), cada uno con
        // un fragmento del mensaje. La ultima linea trae "done": true.
        // Se lee hasta que ReadLineAsync devuelve null (fin del flujo) en vez de
        // consultar reader.EndOfStream, que puede bloquear el hilo en un metodo asincrono.
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (root.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var content))
            {
                var chunk = content.GetString();
                if (!string.IsNullOrEmpty(chunk)) yield return chunk;
            }

            if (root.TryGetProperty("done", out var done) && done.GetBoolean()) yield break;
        }
    }

    /// <summary>Comprueba si Ollama esta corriendo y responde. Util para avisar al
    /// usuario en vez de dejar que el chat falle en silencio.</summary>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            using var response = await Http.GetAsync($"{BaseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}