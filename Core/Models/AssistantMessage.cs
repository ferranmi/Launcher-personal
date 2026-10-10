namespace Launcher.Core.Models;

/// <summary>Un mensaje dentro de una conversacion con el asistente. El campo Role
/// sigue la convencion de la API de Ollama ("user", "assistant", "system").</summary>
public class AssistantMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}