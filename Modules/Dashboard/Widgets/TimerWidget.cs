using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Launcher.Modules.Dashboard.Widgets;

/// <summary>Temporizador / pomodoro: presets de 5, 15, 25 y 50 minutos, iniciar/pausar y reiniciar. Al llegar
/// a cero suena el aviso de Windows. La cuenta sigue aunque cambies de modulo. Se escala con la caja.</summary>
public sealed class TimerWidget : UserControl
{
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly TextBlock _time = new();
    private readonly Border _barHost = new() { Margin = new Thickness(0, 8, 0, 12) };
    private readonly Button _startButton;

    private TimeSpan _total = TimeSpan.FromMinutes(25);
    private TimeSpan _left = TimeSpan.FromMinutes(25);
    private DateTime _endAt;
    private bool _running;

    public TimerWidget()
    {
        _time.FontFamily = Kit.Mono;
        _time.FontSize = 40;
        _time.FontWeight = FontWeights.SemiBold;
        _time.Foreground = Kit.Br("Brush.TextPrimary");
        _time.HorizontalAlignment = HorizontalAlignment.Center;

        var presets = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        foreach (var minutes in new[] { 5, 15, 25, 50 })
        {
            var m = minutes;
            var button = Kit.ActionButton($"{m}m", (_, _) => SetDuration(m));
            button.Margin = new Thickness(3, 0, 3, 0);
            presets.Children.Add(button);
        }

        _startButton = Kit.ActionButton("iniciar", (_, _) => StartPause(), primary: true);
        var reset = Kit.ActionButton("reiniciar", (_, _) => Reset());

        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        _startButton.Margin = new Thickness(0, 0, 8, 0);
        controls.Children.Add(_startButton);
        controls.Children.Add(reset);

        var root = new StackPanel { Width = 200 };
        root.Children.Add(presets);
        root.Children.Add(_time);
        root.Children.Add(_barHost);
        root.Children.Add(controls);

        Content = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Child = root };

        _tick.Tick += (_, _) => Tick();
        Render();
    }

    private void SetDuration(int minutes)
    {
        _running = false;
        _tick.Stop();
        _total = TimeSpan.FromMinutes(minutes);
        _left = _total;
        _startButton.Content = "iniciar";
        Render();
    }

    private void StartPause()
    {
        if (_running)
        {
            _left = _endAt - DateTime.Now;
            if (_left < TimeSpan.Zero) _left = TimeSpan.Zero;
            _running = false;
            _tick.Stop();
            _startButton.Content = "seguir";
        }
        else
        {
            if (_left <= TimeSpan.Zero) _left = _total;
            _endAt = DateTime.Now + _left;
            _running = true;
            _tick.Start();
            _startButton.Content = "pausa";
        }

        Render();
    }

    private void Reset()
    {
        _running = false;
        _tick.Stop();
        _left = _total;
        _startButton.Content = "iniciar";
        Render();
    }

    private void Tick()
    {
        if (!_running) return;

        _left = _endAt - DateTime.Now;
        if (_left <= TimeSpan.Zero)
        {
            _left = TimeSpan.Zero;
            _running = false;
            _tick.Stop();
            _startButton.Content = "iniciar";
            System.Media.SystemSounds.Asterisk.Play();
        }

        Render();
    }

    private void Render()
    {
        var seconds = (int)Math.Ceiling(_left.TotalSeconds);
        _time.Text = $"{seconds / 60:00}:{seconds % 60:00}";

        var elapsed = _total.TotalSeconds > 0 ? 100 * (1 - _left.TotalSeconds / _total.TotalSeconds) : 0;
        _barHost.Child = Kit.Bar(elapsed, Kit.Br("Brush.Accent"));
    }
}
