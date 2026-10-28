using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Launcher.Modules.ImageGen.Extensions;

/// <summary>
/// ControlNet con una unidad. API: alwayson_scripts["controlnet"].args = [ {unidad} ].
/// Modelos y preprocesadores se leen de /controlnet/model_list y /controlnet/module_list.
/// Las claves de la unidad ("image", "control_mode", "resize_mode" en texto) son las de
/// sd-webui-controlnet 1.1.4xx; versiones antiguas usan "input_image" y enteros.
/// </summary>
public class ControlNetExtension : SdExtension
{
    public override string Id => "controlnet";
    public override string DisplayName => "ControlNet";
    public override string Description => "Guia la composicion con una imagen de referencia (pose, bordes, profundidad...).";
    public override string? ServerScriptName => "controlnet";

    private static readonly string[] Modes =
    {
        "Balanced", "My prompt is more important", "ControlNet is more important"
    };

    private readonly TextBox _image = MakeBox("", "ruta de la imagen de referencia");
    private readonly ComboBox _module = MakeCombo(new[] { "none" }, "none");
    private readonly ComboBox _model = MakeCombo(Array.Empty<string>());
    private readonly TextBox _weight = MakeBox("1.0");
    private readonly TextBox _start = MakeBox("0.0");
    private readonly TextBox _end = MakeBox("1.0");
    private readonly ComboBox _mode = MakeCombo(Modes, Modes[0]);
    private readonly CheckBox _pixelPerfect = new()
    {
        Content = "Pixel perfect",
        Foreground = Res<System.Windows.Media.Brush>("Brush.TextSecondary"),
        Margin = new Thickness(0, 2, 0, 0)
    };

    // Seleccion pedida antes de que lleguen las listas del servidor (o tras reconectar).
    private string? _wantedModule;
    private string? _wantedModel;

    public ControlNetExtension()
    {
        _module.SelectionChanged += (_, _) => { if (_module.SelectedItem is string s) _wantedModule = s; };
        _model.SelectionChanged += (_, _) => { if (_model.SelectedItem is string s) _wantedModel = s; };
    }

    protected override async Task LoadServerDataAsync(StableDiffusionService sd)
    {
        var modules = await sd.GetStringListAsync("/controlnet/module_list", "module_list");
        var models = await sd.GetStringListAsync("/controlnet/model_list", "model_list");

        Fill(_module, modules, _wantedModule);
        Fill(_model, models, _wantedModel);
    }

    private static void Fill(ComboBox combo, List<string> items, string? wanted)
    {
        if (items.Count == 0) return;

        var keep = combo.SelectedItem as string ?? wanted;
        combo.ItemsSource = items;
        combo.SelectedItem = keep != null && items.Contains(keep) ? keep : items[0];
    }

    public override FrameworkElement BuildPanel()
    {
        var browse = new Button
        {
            Content = "...",
            Style = Res<Style>("SecondaryButtonStyle"),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(8, 0, 0, 0)
        };
        browse.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "Imagen de referencia para ControlNet",
                Filter = "Imagenes (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Todos los archivos (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true) _image.Text = dialog.FileName;
        };

        var imageRow = new Grid();
        imageRow.ColumnDefinitions.Add(new ColumnDefinition());
        imageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(browse, 1);
        imageRow.Children.Add(_image);
        imageRow.Children.Add(browse);

        var panel = new StackPanel();
        panel.Children.Add(MakeField("IMAGEN DE REFERENCIA", imageRow));
        panel.Children.Add(MakeField("PREPROCESADOR", _module));
        panel.Children.Add(MakeField("MODELO", _model));
        panel.Children.Add(MakePair(MakeField("PESO", _weight, 0), MakeField("MODO", _mode, 0)));
        panel.Children.Add(MakePair(MakeField("INICIO (0-1)", _start, 0), MakeField("FIN (0-1)", _end, 0)));
        panel.Children.Add(_pixelPerfect);
        return panel;
    }

    public override void Apply(SdRequest request)
    {
        var path = _image.Text.Trim();
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("ControlNet esta activado pero falta la imagen de referencia.");
        }

        if (_model.SelectedItem is not string model || string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("ControlNet esta activado pero no hay modelo elegido.");
        }

        request.AddScript("controlnet", new Dictionary<string, object?>
        {
            ["enabled"] = true,
            ["image"] = Convert.ToBase64String(File.ReadAllBytes(path)),
            ["module"] = _module.SelectedItem as string ?? "none",
            ["model"] = model,
            ["weight"] = ParseDouble(_weight.Text, 1.0),
            ["guidance_start"] = ParseDouble(_start.Text, 0.0),
            ["guidance_end"] = ParseDouble(_end.Text, 1.0),
            ["control_mode"] = _mode.SelectedItem as string ?? Modes[0],
            ["resize_mode"] = "Crop and Resize",
            ["pixel_perfect"] = _pixelPerfect.IsChecked == true
        });
    }

    protected override JsonObject SaveCore() => new()
    {
        ["image"] = _image.Text,
        ["module"] = _module.SelectedItem as string ?? _wantedModule,
        ["model"] = _model.SelectedItem as string ?? _wantedModel,
        ["weight"] = _weight.Text,
        ["start"] = _start.Text,
        ["end"] = _end.Text,
        ["mode"] = _mode.SelectedItem as string ?? Modes[0],
        ["pixelPerfect"] = _pixelPerfect.IsChecked == true
    };

    protected override void LoadCore(JsonObject state)
    {
        _image.Text = Str(state, "image", "");
        _weight.Text = Str(state, "weight", "1.0");
        _start.Text = Str(state, "start", "0.0");
        _end.Text = Str(state, "end", "1.0");
        _pixelPerfect.IsChecked = (bool?)state["pixelPerfect"] ?? false;

        var mode = Str(state, "mode", Modes[0]);
        if (_mode.Items.Contains(mode)) _mode.SelectedItem = mode;

        _wantedModule = (string?)state["module"] ?? _wantedModule;
        _wantedModel = (string?)state["model"] ?? _wantedModel;

        if (_wantedModule != null && _module.Items.Contains(_wantedModule)) _module.SelectedItem = _wantedModule;
        if (_wantedModel != null && _model.Items.Contains(_wantedModel)) _model.SelectedItem = _wantedModel;
    }
}
