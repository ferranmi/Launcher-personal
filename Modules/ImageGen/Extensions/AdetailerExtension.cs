using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace Launcher.Modules.ImageGen.Extensions;

/// <summary>
/// ADetailer: detecta caras/manos/personas y las rehace con un segundo pase.
/// API: alwayson_scripts["ADetailer"].args = [enable, skip_img2img, {unidad1}, ...].
/// Las versiones antiguas (anteriores a ~23.7) no llevan el segundo booleano:
/// si tu version falla, quita el "false" de Apply.
/// </summary>
public class AdetailerExtension : SdExtension
{
    public override string Id => "adetailer";
    public override string DisplayName => "ADetailer";
    public override string Description => "Detecta caras, manos o personas y las rehace con un segundo pase.";
    public override string? ServerScriptName => "adetailer";

    private static readonly string[] Models =
    {
        "face_yolov8n.pt", "face_yolov8s.pt", "hand_yolov8n.pt",
        "person_yolov8n-seg.pt", "mediapipe_face_full"
    };

    private readonly ComboBox _model = MakeCombo(Models, Models[0], editable: true);
    private readonly TextBox _prompt = MakeBox("", "vacio = usa el prompt principal");
    private readonly TextBox _negative = MakeBox("", "vacio = usa el negativo principal");
    private readonly TextBox _confidence = MakeBox("0.3");
    private readonly TextBox _denoise = MakeBox("0.4");

    public override FrameworkElement BuildPanel()
    {
        var panel = new StackPanel();
        panel.Children.Add(MakeField("MODELO DE DETECCION", _model));
        panel.Children.Add(MakeField("PROMPT DEL DETALLE", _prompt));
        panel.Children.Add(MakeField("NEGATIVO DEL DETALLE", _negative));
        panel.Children.Add(MakePair(
            MakeField("CONFIANZA", _confidence, 0),
            MakeField("DENOISING", _denoise, 0),
            0));
        return panel;
    }

    public override void Apply(SdRequest request)
    {
        var model = string.IsNullOrWhiteSpace(_model.Text) ? Models[0] : _model.Text.Trim();

        request.AddScript("ADetailer", true, false, new Dictionary<string, object?>
        {
            ["ad_model"] = model,
            ["ad_prompt"] = _prompt.Text.Trim(),
            ["ad_negative_prompt"] = _negative.Text.Trim(),
            ["ad_confidence"] = ParseDouble(_confidence.Text, 0.3),
            ["ad_denoising_strength"] = ParseDouble(_denoise.Text, 0.4)
        });
    }

    protected override JsonObject SaveCore() => new()
    {
        ["model"] = _model.Text,
        ["prompt"] = _prompt.Text,
        ["negative"] = _negative.Text,
        ["confidence"] = _confidence.Text,
        ["denoise"] = _denoise.Text
    };

    protected override void LoadCore(JsonObject state)
    {
        _model.Text = Str(state, "model", Models[0]);
        _prompt.Text = Str(state, "prompt", "");
        _negative.Text = Str(state, "negative", "");
        _confidence.Text = Str(state, "confidence", "0.3");
        _denoise.Text = Str(state, "denoise", "0.4");
    }
}
