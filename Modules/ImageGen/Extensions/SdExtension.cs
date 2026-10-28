using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Launcher.Core.Services;

namespace Launcher.Modules.ImageGen.Extensions;

/// <summary>
/// Contrato de una extension del modulo de imagenes. Cubre dos tipos:
///  - Oficiales de A1111 (ADetailer, ControlNet...): anaden su script a request.AlwaysOn.
///  - Propias (Wildcards...): modifican request.Prompt / request.Body directamente.
///
/// Para crear una nueva: hereda de esta clase, construye los controles como campos,
/// implementa BuildPanel / Apply / SaveCore / LoadCore y anadela en SdExtensionCatalog.
/// </summary>
public abstract class SdExtension
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }

    /// <summary>Fragmento del nombre del script en A1111 (script-info). Null = extension
    /// local que no necesita nada instalado en el servidor.</summary>
    public virtual string? ServerScriptName => null;

    /// <summary>True si, con este prompt, cada imagen necesita su propia peticion (p.ej.
    /// wildcards distintos por imagen). A1111 solo acepta un prompt por peticion, asi que
    /// el modulo genera entonces las imagenes de una en una.</summary>
    public virtual bool VariesPerImage(string prompt, string negativePrompt) => false;

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            Changed?.Invoke();
        }
    }

    public bool IsAvailable { get; private set; } = true;

    public string StatusText =>
        ServerScriptName == null ? "local" : IsAvailable ? "" : "no instalada en A1111";

    /// <summary>Se dispara al cambiar IsEnabled o la disponibilidad (la UI se resincroniza).</summary>
    public event Action? Changed;

    /// <summary>Se llama tras conectar con A1111. serverScripts = null significa que el
    /// servidor no lo informa: la extension se considera disponible.</summary>
    public async Task InitializeAsync(StableDiffusionService sd, HashSet<string>? serverScripts)
    {
        IsAvailable = ServerScriptName == null || serverScripts == null ||
                      serverScripts.Any(s => s.Contains(ServerScriptName, StringComparison.OrdinalIgnoreCase));

        if (IsAvailable)
        {
            try
            {
                await LoadServerDataAsync(sd);
            }
            catch (Exception ex)
            {
                LogService.Error($"Extension {Id}: no se pudieron leer sus datos del servidor", ex);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>Carga listas del servidor (modelos de ControlNet, etc.).</summary>
    protected virtual Task LoadServerDataAsync(StableDiffusionService sd) => Task.CompletedTask;

    /// <summary>Panel de ajustes (los controles deben existir ya como campos).</summary>
    public abstract FrameworkElement BuildPanel();

    /// <summary>Modifica la peticion. Solo se llama si esta activada y disponible.
    /// Si algo falta, lanza InvalidOperationException con un mensaje legible.</summary>
    public abstract void Apply(SdRequest request);

    protected abstract JsonObject SaveCore();
    protected abstract void LoadCore(JsonObject state);

    public string SaveState()
    {
        var node = SaveCore();
        node["enabled"] = IsEnabled;
        return node.ToJsonString();
    }

    public void LoadState(string json)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject node) return;
            LoadCore(node);
            IsEnabled = (bool?)node["enabled"] ?? false;
        }
        catch (Exception ex)
        {
            LogService.Error($"Extension {Id}: estado guardado ilegible", ex);
        }
    }

    // ===================== Ayudas para construir paneles =====================

    protected static T Res<T>(string key) => (T)System.Windows.Application.Current.FindResource(key);

    protected static TextBox MakeBox(string text = "", string? hint = null, bool multiline = false)
    {
        var box = new TextBox
        {
            Text = text,
            Tag = hint,
            Style = Res<Style>("TextBoxStyle"),
            Padding = new Thickness(8, 6, 8, 6)
        };

        if (multiline)
        {
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.Height = 90;
            box.VerticalContentAlignment = VerticalAlignment.Top;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }

        return box;
    }

    protected static ComboBox MakeCombo(IEnumerable<string> items, string? selected = null, bool editable = false)
    {
        var combo = new ComboBox { ItemsSource = items.ToList(), IsEditable = editable };

        if (selected != null)
        {
            if (editable) combo.Text = selected;
            else combo.SelectedItem = selected;
        }

        return combo;
    }

    protected static StackPanel MakeField(string label, UIElement control, double bottom = 10)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, bottom) };
        panel.Children.Add(new TextBlock { Text = label, Style = Res<Style>("FieldLabelStyle") });
        panel.Children.Add(control);
        return panel;
    }

    /// <summary>Dos controles en columnas iguales.</summary>
    protected static Grid MakePair(UIElement left, UIElement right, double bottom = 10)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, bottom) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    protected static double ParseDouble(string? text, double fallback) =>
        double.TryParse(text?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;

    protected static string Str(JsonObject state, string key, string fallback) =>
        (string?)state[key] ?? fallback;
}
