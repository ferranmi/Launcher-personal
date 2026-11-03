using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Launcher.Modules.Dashboard.Widgets;

/// <summary>
/// Calendario del mes con eventos. Arriba, el mes (semana desde el lunes, hoy resaltado, un punto bajo los dias
/// con eventos); abajo, los eventos del dia seleccionado con su hora, y una linea para anadir otro (hora opcional
/// "hh:mm" + titulo + Enter). La ultima linea indica el proximo evento. Los eventos se guardan en events.json.
/// </summary>
public sealed class CalendarWidget : UserControl
{
    private static readonly CultureInfo Es = new("es-ES");

    private readonly CalendarEventsService _service = CalendarEventsService.Instance;

    private readonly TextBlock _title = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = 12,
        Cursor = Cursors.Hand,
        ToolTip = "Volver a hoy"
    };

    private readonly UniformGrid _days = new() { Columns = 7 };
    private readonly TextBlock _dayTitle = Kit.Label("", 10.5, "Brush.TextSecondary", mono: true);
    private readonly TextBlock _next = Kit.Label("", 10, "Brush.TextDim", mono: true);
    private readonly StackPanel _list = new();
    private readonly TextBox _timeBox = Kit.Input("hh:mm");
    private readonly TextBox _titleBox = Kit.Input("nuevo evento + Enter");
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };

    private DateTime _today = DateTime.Today;
    private DateTime _selected = DateTime.Today;
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public CalendarWidget()
    {
        Content = BuildLayout();

        _timer.Tick += (_, _) =>
        {
            if (DateTime.Today == _today)
            {
                RenderEvents(); // refresca el "proximo evento" a medida que pasa el tiempo
                return;
            }

            _today = DateTime.Today;
            Build();
        };

        Loaded += (_, _) =>
        {
            _today = DateTime.Today;
            _service.Changed += OnEventsChanged;
            Build();
            RenderEvents();
            _timer.Start();
        };

        Unloaded += (_, _) =>
        {
            _service.Changed -= OnEventsChanged;
            _timer.Stop();
        };
    }

    // ===================== Estructura =====================

    private UIElement BuildLayout()
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

        var month = new StackPanel { Width = 220 };
        month.Children.Add(header);
        month.Children.Add(weekdays);
        month.Children.Add(_days);

        // El mes se escala entero con la caja (Viewbox); los eventos de abajo no.
        var monthView = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Child = month };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4, GridUnitType.Star) });

        var events = BuildEventsPanel();
        Grid.SetRow(events, 1);
        root.Children.Add(monthView);
        root.Children.Add(events);
        return root;
    }

    private UIElement BuildEventsPanel()
    {
        _titleBox.KeyDown += OnInputKeyDown;
        _timeBox.KeyDown += OnInputKeyDown;
        _timeBox.Width = 58;
        _timeBox.ToolTip = "Hora opcional, p.ej. 9:30. Vacio = todo el dia";
        _timeBox.TextChanged += (_, _) => _timeBox.Foreground = Kit.Br("Brush.TextPrimary");

        var add = Kit.ActionButton("+", (_, _) => AddEvent(), primary: true);
        add.Padding = new Thickness(10, 4, 10, 4);

        var input = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        input.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        input.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        input.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _timeBox.Margin = new Thickness(0, 0, 6, 0);
        add.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(_titleBox, 1);
        Grid.SetColumn(add, 2);
        input.Children.Add(_timeBox);
        input.Children.Add(_titleBox);
        input.Children.Add(add);

        _dayTitle.Margin = new Thickness(0, 8, 0, 6);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _list };

        var panel = new Grid();
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(scroll, 1);
        Grid.SetRow(input, 2);
        Grid.SetRow(_next, 3);
        _next.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(_dayTitle);
        panel.Children.Add(scroll);
        panel.Children.Add(input);
        panel.Children.Add(_next);
        return panel;
    }

    // ===================== Navegacion =====================

    private void ChangeMonth(int delta)
    {
        _month = _month.AddMonths(delta);
        Build();
    }

    private void GoToToday()
    {
        _today = DateTime.Today;
        _selected = _today;
        _month = new DateTime(_today.Year, _today.Month, 1);
        Build();
        RenderEvents();
    }

    private void Select(DateTime day)
    {
        _selected = day;
        Build();
        RenderEvents();
    }

    private void OnEventsChanged(object? sender, EventArgs e)
    {
        Build(); // los puntos de los dias con eventos
        RenderEvents();
    }

    // ===================== Mes =====================

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
            var isSelected = date == _selected;

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

            var content = new Grid();
            content.Children.Add(number);

            if (_service.HasEvents(date))
            {
                content.Children.Add(new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = Kit.Uplink,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 2)
                });
            }

            var cell = new Border
            {
                Height = 26,
                Margin = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(isToday || isSelected ? 1 : 0),
                BorderBrush = Kit.Br(isToday ? "Brush.Accent" : "Brush.TextSecondary"),
                Background = isToday ? Kit.Br("Brush.AccentDim") : System.Windows.Media.Brushes.Transparent,
                Child = content
            };

            var captured = date;
            cell.MouseLeftButtonUp += (_, _) => Select(captured);
            _days.Children.Add(cell);
        }
    }

    // ===================== Eventos =====================

    private void RenderEvents()
    {
        _dayTitle.Text = _selected.ToString("dddd d MMMM", Es).ToUpperInvariant();
        _list.Children.Clear();

        var events = _service.On(_selected);
        if (events.Count == 0)
        {
            _list.Children.Add(Kit.Label("> sin eventos_", 10.5, "Brush.TextDim", mono: true));
        }

        foreach (var ev in events) _list.Children.Add(EventRow(ev));

        var upcoming = _service.Next(DateTime.Now);
        _next.Text = upcoming == null
            ? ""
            : $"proximo: {upcoming.Date:dd/MM}{(upcoming.Time.Length > 0 ? " " + upcoming.Time : "")}  {upcoming.Title}";
    }

    private UIElement EventRow(CalendarEvent ev)
    {
        var past = ev.Time.Length == 0 ? ev.Date.Date < DateTime.Today : ev.When < DateTime.Now;

        var row = new Grid { Margin = new Thickness(0, 0, 0, 5) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var time = Kit.Label(ev.Time.Length > 0 ? ev.Time : "todo", 10.5, "Brush.TextDim", mono: true);
        if (!past) time.Foreground = Kit.Uplink;
        time.VerticalAlignment = VerticalAlignment.Center;

        var title = Kit.Label(ev.Title, 12, past ? "Brush.TextDim" : "Brush.TextPrimary");
        title.VerticalAlignment = VerticalAlignment.Center;
        title.ToolTip = ev.Title;
        Grid.SetColumn(title, 1);

        var delete = new TextBlock
        {
            Text = "\u00D7",
            FontSize = 16,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Foreground = Kit.Br("Brush.TextDim"),
            ToolTip = "Borrar evento"
        };
        Grid.SetColumn(delete, 2);

        var id = ev.Id;
        delete.MouseLeftButtonUp += (_, _) => _service.Remove(id);

        row.Children.Add(time);
        row.Children.Add(title);
        row.Children.Add(delete);
        return row;
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        AddEvent();
        e.Handled = true;
    }

    private void AddEvent()
    {
        var title = _titleBox.Text.Trim();
        if (title.Length == 0) return;

        var time = "";
        var typed = _timeBox.Text.Trim();
        if (typed.Length > 0)
        {
            if (!TimeSpan.TryParse(typed, CultureInfo.InvariantCulture, out var parsed) ||
                parsed < TimeSpan.Zero || parsed >= TimeSpan.FromHours(24))
            {
                _timeBox.Foreground = Kit.Br("Brush.Danger"); // hora no valida: se pone en rojo hasta que se corrija
                return;
            }

            time = $"{(int)parsed.TotalHours:00}:{parsed.Minutes:00}";
        }

        _service.Add(_selected, time, title);
        _titleBox.Clear();
        _timeBox.Clear();
        _titleBox.Focus();
    }
}
