using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using Launcher.Core.Services;

namespace Launcher.Modules.Dashboard.Widgets;

/// <summary>
/// Estado de los servicios que el Launcher usa o vigila: Ollama (y que modelo tiene cargado), AUTOMATIC1111,
/// Steam y Tailscale. Se comprueba cada 5 s mientras el widget esta en pantalla.
/// </summary>
public sealed class ServicesWidget : UserControl
{
    // Puertos/URLs por defecto: cambialos aqui si los tienes en otro sitio.
    private const string OllamaPsUrl = "http://localhost:11434/api/ps";
    private const int A1111Port = 7860;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

    private readonly StackPanel _rows = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _busy;

    public ServicesWidget()
    {
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _rows };

        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) =>
        {
            _timer.Start();
            await RefreshAsync();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;

        try
        {
            var ollama = await CheckOllamaAsync();
            var a1111 = await PortOpenAsync(A1111Port);
            var steam = IsRunning("steam");
            var tailscale = IsRunning("tailscaled", "tailscale-ipn");

            _rows.Children.Clear();
            AddRow("Ollama", ollama.Up, ollama.Detail);
            AddRow("Stable Diffusion", a1111, a1111 ? $"A1111 :{A1111Port}" : "apagado");
            AddRow("Steam", steam, steam ? "en ejecucion" : "cerrado");
            AddRow("Tailscale", tailscale, tailscale ? "conectado al servicio" : "no detectado");
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo el widget de servicios", ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private static async Task<(bool Up, string Detail)> CheckOllamaAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(OllamaPsUrl);
            using var doc = JsonDocument.Parse(json);

            var names = new List<string>();
            if (doc.RootElement.TryGetProperty("models", out var models))
            {
                foreach (var model in models.EnumerateArray())
                {
                    if (model.TryGetProperty("name", out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
                        names.Add(name.GetString()!);
                }
            }

            return (true, names.Count == 0 ? "sin modelo cargado" : string.Join(", ", names));
        }
        catch
        {
            return (false, "apagado");
        }
    }

    private static async Task<bool> PortOpenAsync(int port)
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync("127.0.0.1", port);
            if (await Task.WhenAny(connect, Task.Delay(400)) != connect) return false;

            await connect; // si la conexion fallo, esto lanza y caemos al catch
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRunning(params string[] processNames)
    {
        foreach (var name in processNames)
        {
            var found = Process.GetProcessesByName(name);
            var any = found.Length > 0;
            foreach (var process in found) process.Dispose();
            if (any) return true;
        }

        return false;
    }

    private void AddRow(string name, bool up, string detail)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Fill = Kit.Br(up ? "Brush.Accent" : "Brush.TextDim")
        };

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(Kit.Label(name, 12));
        texts.Children.Add(Kit.Label(detail, 10, "Brush.TextDim", mono: true));
        Grid.SetColumn(texts, 1);

        var state = Kit.Label(up ? "ON" : "OFF", 10, up ? "Brush.Accent" : "Brush.TextDim", mono: true);
        state.VerticalAlignment = VerticalAlignment.Center;
        state.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(state, 2);

        row.Children.Add(dot);
        row.Children.Add(texts);
        row.Children.Add(state);
        _rows.Children.Add(row);
    }
}
