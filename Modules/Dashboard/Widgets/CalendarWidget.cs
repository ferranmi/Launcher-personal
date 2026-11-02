using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Launcher.Modules.Dashboard.Widgets;

/// <summary>Calendario del mes (semana desde el lunes). Hoy va resaltado; las flechas cambian de mes y
/// un clic en el titulo vuelve a hoy. Se escala entero con la caja (Viewbox).</summary>
public sealed class CalendarWidget : UserControl
{
    private static readonly CultureInfo Es = new("es-ES");

    private readonly TextBlock _title = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = 12,
        Cursor = Cursors.Hand,
        ToolTip = "Volver a hoy"
    };

    private readonly UniformGrid _days = new() { Columns = 7 };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };

    private DateTime _today = DateTime.Today;
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public CalendarWidget()
    {
        _title.FontFamily = Kit.Mono;
        _title.Foreground = Kit.Br("Brush.TextPrimary");
        _title.MouseLeftButtonUp += (_, _) => GoToToday();

        var prev = Kit.ActionButton("\u2039", (_, _) => ChangeMonth(-1));
        var next = Kit.ActionButton("\u203A", (_, _) => ChangeMonth(1));
        prev.Padding = next.Padding = new Thickness(10, 2, 10, 2);

        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_title, 1);
        Grid.SetColumn(next, 2);
        header.Children.Add(prev);
        header.Children.Add(_title);
        header.Children.Add(next);

        var weekdays = new UniformGrid { Columns = 7, Margin = new Thickness(0, 0, 0, 4) };
        foreach (var letter in new[] { "L", "M", "X", "J", "V", "S", "D" })
        {
            var label = Kit.Label(letter, 10, "Brush.TextDim", mono: true);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            weekdays.Children.Add(label);
        }

        var root = new StackPanel { Width = 220 };
        root.Children.Add(header);
        root.Children.Add(weekdays);
        root.Children.Add(_days);

        Content = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Child = root };

        _timer.Tick += (_, _) =>
        {
            if (DateTime.Today == _today) return;
            _today = DateTime.Today;
            Build();
        };

        Loaded += (_, _) =>
        {
            _today = DateTime.Today;
            Build();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void ChangeMonth(int delta)
    {
        _month = _month.AddMonths(delta);
        Build();
    }

    private void GoToToday()
    {
        _today = DateTime.Today;
        _month = new DateTime(_today.Year, _today.Month, 1);
        Build();
    }

    private void Build()
    {
        _title.Text = _month.ToString("MMMM yyyy", Es).ToUpperInvariant();
        _days.Children.Clear();

        var offset = ((int)_month.DayOfWeek + 6) % 7; // lunes = 0
        for (var i = 0; i < offset; i++) _days.Children.Add(new Border { Height = 26 });

        var count = DateTime.DaysInMonth(_month.Year, _month.Month);
        for (var day = 1; day <= count; day++)
        {
            var date = new DateTime(_month.Year, _month.Month, day);
            var isToday = date == _today;

            var number = new TextBlock
            {
                Text = day.ToString(CultureInfo.InvariantCulture),
                FontFamily = Kit.Mono,
                FontSize = 11,
                FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Kit.Br(isToday ? "Brush.Accent" : date.DayOfWeek == DayOfWeek.Sunday ? "Brush.TextSecondary" : "Brush.TextPrimary")
            };

            _days.Children.Add(new Border
            {
                Height = 26,
                Margin = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(isToday ? 1 : 0),
                BorderBrush = Kit.Br("Brush.Accent"),
                Background = isToday ? Kit.Br("Brush.AccentDim") : System.Windows.Media.Brushes.Transparent,
                Child = number
            });
        }
    }
}
