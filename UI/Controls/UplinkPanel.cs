using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.UI.Controls;

/// <summary>
/// Lista de dispositivos externos conectados (Bluetooth y USB) para el dashboard,
/// con la etiqueta UPLINK. Muestra MaxVisible filas y un "+N mas" que despliega el
/// resto. Se actualiza cada 10 s solo mientras esta en pantalla.
/// Uso en XAML:  &lt;controls:UplinkPanel MaxVisible="3" /&gt;
/// </summary>
public class UplinkPanel : UserControl
{
    private readonly StackPanel _list = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(10) };
    private List<DeviceInfo> _devices = new();
    private bool _expanded;
    private bool _refreshing;

    public int MaxVisible { get; set; } = 3;

    public UplinkPanel()
    {
        Content = _list;

        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) => { await RefreshAsync(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;

        try
        {
            _devices = await DeviceService.GetExternalAsync();
        }
        catch (Exception ex)
        {
            LogService.Error("UplinkPanel: fallo al leer dispositivos", ex);
        }
        finally
        {
            _refreshing = false;
        }

        Render();
    }

    private void Render()
    {
        _list.Children.Clear();

        if (_devices.Count == 0)
        {
            _list.Children.Add(Text("> sin dispositivos externos_", 11, "Brush.TextDim"));
            return;
        }

        var shown = _expanded ? _devices : _devices.Take(MaxVisible).ToList();
        foreach (var device in shown) _list.Children.Add(BuildRow(device));

        var hidden = _devices.Count - shown.Count;
        if (hidden > 0 || (_expanded && _devices.Count > MaxVisible))
        {
            var toggle = Text(_expanded ? "[ menos ]" : $"[ +{hidden} mas ]", 10.5, "Brush.Uplink");
            toggle.Cursor = Cursors.Hand;
            toggle.Margin = new Thickness(0, 4, 0, 0);
            toggle.MouseLeftButtonUp += (_, _) => { _expanded = !_expanded; Render(); };
            _list.Children.Add(toggle);
        }
    }

    private UIElement BuildRow(DeviceInfo device)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tag = Text("UPLINK", 9, "Brush.Uplink");
        tag.Margin = new Thickness(0, 0, 8, 0);
        tag.VerticalAlignment = VerticalAlignment.Center;

        var name = Text(device.Name, 12, "Brush.TextPrimary");
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.ToolTip = device.Name;
        Grid.SetColumn(name, 1);

        var kind = Text(device.Connection == "Bluetooth" ? "BT" : "USB", 9.5, "Brush.TextDim");
        kind.Margin = new Thickness(8, 0, 0, 0);
        kind.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(kind, 2);

        row.Children.Add(tag);
        row.Children.Add(name);
        row.Children.Add(kind);
        return row;
    }

    private static TextBlock Text(string text, double size, string brushKey) => new()
    {
        Text = text,
        FontSize = size,
        FontFamily = (FontFamily)Application.Current.FindResource("Font.Mono"),
        Foreground = (Brush)Application.Current.FindResource(brushKey)
    };
}
