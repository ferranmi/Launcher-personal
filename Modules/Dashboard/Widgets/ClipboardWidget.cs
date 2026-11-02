using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Launcher.Modules.Dashboard.Widgets;

/// <summary>
/// Historial del portapapeles (solo texto). Vive en memoria mientras el Launcher esta abierto: no se guarda
/// en disco. Empieza a vigilar la primera vez que el widget se muestra y sigue aunque cambies de modulo.
/// Ignora lo que las aplicaciones marcan como "excluir del historial" (gestores de contrasenas).
/// </summary>
public sealed class ClipboardHistoryService
{
    public static ClipboardHistoryService Instance { get; } = new();

    private const int MaxItems = 20;

    public List<string> Items { get; } = new();
    public event EventHandler? Changed;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private string? _last;
    private bool _started;

    private ClipboardHistoryService() => _timer.Tick += (_, _) => Poll();

    public void Start()
    {
        if (_started) return;
        _started = true;
        _last = ReadText(); // lo que ya habia al arrancar no se anade solo
        _timer.Start();
    }

    private void Poll()
    {
        var text = ReadText();
        if (string.IsNullOrWhiteSpace(text) || text == _last) return;

        _last = text;
        Push(text);
    }

    /// <summary>Vuelve a poner un elemento del historial en el portapapeles.</summary>
    public void Copy(string text)
    {
        try
        {
            _last = text;
            Clipboard.SetText(text);
            Push(text);
        }
        catch
        {
            // el portapapeles puede estar ocupado por otra aplicacion: se reintenta con otro clic
        }
    }

    public void Clear()
    {
        Items.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Push(string text)
    {
        Items.Remove(text);
        Items.Insert(0, text);
        if (Items.Count > MaxItems) Items.RemoveRange(MaxItems, Items.Count - MaxItems);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string? ReadText()
    {
        try
        {
            if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")) return null;
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch
        {
            return null; // portapapeles ocupado en este instante
        }
    }
}

public sealed class ClipboardWidget : UserControl
{
    private readonly StackPanel _list = new();
    private readonly ClipboardHistoryService _service = ClipboardHistoryService.Instance;

    public ClipboardWidget()
    {
        var clear = Kit.ActionButton("borrar historial", (_, _) => _service.Clear());
        clear.FontSize = 10;
        clear.Padding = new Thickness(8, 3, 8, 3);

        var note = Kit.Label("solo en memoria", 10, "Brush.TextDim", mono: true);
        note.VerticalAlignment = VerticalAlignment.Center;

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _list };

        var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(clear, 1);
        footer.Children.Add(note);
        footer.Children.Add(clear);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(footer, 1);
        root.Children.Add(scroll);
        root.Children.Add(footer);
        Content = root;

        Loaded += (_, _) =>
        {
            _service.Start();
            _service.Changed += OnChanged;
            Render();
        };
        Unloaded += (_, _) => _service.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, EventArgs e) => Render();

    private void Render()
    {
        _list.Children.Clear();

        if (_service.Items.Count == 0)
        {
            _list.Children.Add(Kit.Label("> copia algo y aparecera aqui_", 10.5, "Brush.TextDim", mono: true));
            return;
        }

        foreach (var text in _service.Items)
        {
            var preview = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (preview.Length > 200) preview = preview[..200];

            var line = Kit.Label(preview, 11.5);
            var row = new Border
            {
                Style = (Style)Application.Current.FindResource("CardStyle"),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
                ToolTip = "Clic para volver a copiarlo",
                Child = line
            };

            var captured = text;
            row.MouseLeftButtonUp += (_, _) => _service.Copy(captured);
            _list.Children.Add(row);
        }
    }
}
