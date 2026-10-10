using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.Monitor;

/// <summary>Wrapper de solo-display para unidades de disco: anade texto ya formateado
/// de velocidad de lectura/escritura sin tocar el modelo de dominio DriveStat.</summary>
public class DriveDisplayItem
{
    public string Name { get; set; } = "";
    public double UsedGB { get; set; }
    public double TotalGB { get; set; }
    public double UsedPercent { get; set; }
    public string ReadDisplay { get; set; } = "0 KB/s";
    public string WriteDisplay { get; set; } = "0 KB/s";
}

public partial class MonitorView : UserControl
{
    private const int HistoryLength = 60;

    private readonly SystemMonitorService _service = new();
    private readonly DispatcherTimer _timer;

    private readonly Queue<double> _cpuHistory = new();
    private readonly Queue<double> _ramHistory = new();
    private readonly Queue<double> _gpuHistory = new();
    private readonly Queue<double> _vramHistory = new();
    private readonly Queue<double> _netHistory = new();

    public ObservableCollection<DriveDisplayItem> Drives { get; } = new();
    
    public MonitorView()
    {
        InitializeComponent();
        DrivesItems.ItemsSource = Drives;
        
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();

        Loaded += (_, _) => { Tick(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Tick()
    {
        var snap = _service.Sample();

        // CPU
        CpuPercentText.Text = $"{snap.CpuPercent:0}%";
        PushHistory(_cpuHistory, snap.CpuPercent);
        DrawSparkline(CpuSparkline, _cpuHistory, 100);

        // RAM
        var ramUsedGb = snap.RamUsedBytes / 1024d / 1024 / 1024;
        var ramTotalGb = snap.RamTotalBytes / 1024d / 1024 / 1024;
        RamPercentText.Text = $"{snap.RamPercent:0}%";
        RamDetailText.Text = $"{ramUsedGb:0.#} GB / {ramTotalGb:0.#} GB";
        PushHistory(_ramHistory, snap.RamPercent);
        DrawSparkline(RamSparkline, _ramHistory, 100);

        // GPU
        GpuLabelText.Text = snap.GpuName;
        GpuPercentText.Text = $"{snap.GpuPercent:0}%";
        PushHistory(_gpuHistory, snap.GpuPercent);
        DrawSparkline(GpuSparkline, _gpuHistory, 100);

        // VRAM (mismo formato que RAM: % grande + detalle GB usado/total)
        var vramUsedGb = snap.VramUsedBytes / 1024d / 1024 / 1024;
        var vramTotalGb = snap.VramTotalBytes / 1024d / 1024 / 1024;
        VramPercentText.Text = $"{snap.VramPercent:0}%";
        VramDetailText.Text = vramTotalGb > 0
            ? $"{vramUsedGb:0.#} GB / {vramTotalGb:0.#} GB"
            : $"{vramUsedGb:0.#} GB";
        PushHistory(_vramHistory, snap.VramPercent);
        DrawSparkline(VramSparkline, _vramHistory, 100);

        // Red
        NetDownText.Text = FormatRate(snap.NetDownBytesPerSec);
        NetUpText.Text = FormatRate(snap.NetUpBytesPerSec);
        var combinedNetKb = (snap.NetDownBytesPerSec + snap.NetUpBytesPerSec) / 1024d;
        PushHistory(_netHistory, combinedNetKb);
        var netMax = _netHistory.Count > 0 ? Math.Max(50, _netHistory.Max()) : 50;
        DrawSparkline(NetSparkline, _netHistory, netMax);

        // Uptime
        UptimeText.Text = $"Actividad: {FormatUptime(snap.Uptime)}";

        
        // Discos (capacidad + velocidad de lectura/escritura por unidad)
        Drives.Clear();
        foreach (var d in snap.Drives)
        {
            Drives.Add(new DriveDisplayItem
            {
                Name = d.Name,
                UsedGB = d.UsedGB,
                TotalGB = d.TotalGB,
                UsedPercent = d.UsedPercent,
                ReadDisplay = FormatRate(d.ReadBytesPerSec),
                WriteDisplay = FormatRate(d.WriteBytesPerSec)
            });
        }
    }

    private static void PushHistory(Queue<double> history, double value)
    {
        history.Enqueue(value);
        while (history.Count > HistoryLength) history.Dequeue();
    }

    private static void DrawSparkline(Polyline line, Queue<double> history, double maxValue)
    {
        if (history.Count < 2)
        {
            line.Points = new PointCollection();
            return;
        }

        var width = line.ActualWidth > 0 ? line.ActualWidth : 160;
        var height = line.ActualHeight > 0 ? line.ActualHeight : 42;
        var values = history.ToArray();
        var stepX = width / Math.Max(1, HistoryLength - 1);
        var startIndex = HistoryLength - values.Length;

        var points = new PointCollection();
        for (var i = 0; i < values.Length; i++)
        {
            var x = (startIndex + i) * stepX;
            var normalized = maxValue <= 0 ? 0 : Math.Min(1, values[i] / maxValue);
            var y = height - (normalized * height);
            points.Add(new System.Windows.Point(x, y));
        }

        line.Points = points;
    }

    private static string FormatRate(double bytesPerSec)
    {
        var kb = bytesPerSec / 1024d;
        return kb >= 1024 ? $"{kb / 1024:0.#} MB/s" : $"{kb:0.#} KB/s";
    }

    private static string FormatUptime(TimeSpan ts)
    {
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{ts.Minutes}m {ts.Seconds}s";
    }
}