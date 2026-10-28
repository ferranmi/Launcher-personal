using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace Launcher.Modules.ImageGen.Extensions;

/// <summary>
/// Valvula de escape: permite usar CUALQUIER extension alwayson de A1111 sin panel propio.
/// Se indica el nombre del script y su lista de argumentos en JSON, tal cual la espera la
/// API (alwayson_scripts: {"nombre": {"args": [...]}}). Los argumentos de cada extension
/// estan documentados en su repositorio o en http://127.0.0.1:7860/docs.
/// </summary>
public class RawScriptExtension : SdExtension
{
    public override string Id => "rawscript";
    public override string DisplayName => "Script JSON (avanzado)";
    public override string Description => "Envia a A1111 cualquier script alwayson indicando su nombre y sus args en JSON.";

    private readonly TextBox _name = MakeBox("", "p.ej. Regional Prompter");
    private readonly TextBox _args = MakeBox("[]", "[true, \"valor\", 0.5]", multiline: true);

    public override FrameworkElement BuildPanel()
    {
        var panel = new StackPanel();
        panel.Children.Add(MakeField("NOMBRE DEL SCRIPT", _name));
        panel.Children.Add(MakeField("ARGS (array JSON)", _args, 0));
        return panel;
    }

    public override void Apply(SdRequest request)
    {
        var name = _name.Text.Trim();
        if (name.Length == 0)
        {
            throw new InvalidOperationException("Script JSON esta activado pero no tiene nombre.");
        }

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(_args.Text) ? "[]" : _args.Text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Los args del script JSON deben ser un array, p.ej. [true, 0.5].");
            }

            request.AlwaysOn[name] = new Dictionary<string, object?> { ["args"] = doc.RootElement.Clone() };
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"JSON de args no valido: {ex.Message}");
        }
    }

    protected override JsonObject SaveCore() => new()
    {
        ["name"] = _name.Text,
        ["args"] = _args.Text
    };

    protected override void LoadCore(JsonObject state)
    {
        _name.Text = Str(state, "name", "");
        _args.Text = Str(state, "args", "[]");
    }
}
