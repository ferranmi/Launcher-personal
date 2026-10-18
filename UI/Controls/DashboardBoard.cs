using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Launcher.Core.Services;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Launcher.UI.Controls;

/// <summary>
/// Tablero de cajas libres: una rejilla de Columns x Rows celdas que se reparte todo el espacio
/// disponible. Cada <see cref="BoardBox"/> hijo ocupa un rectangulo de celdas (Col, Row, ColSpan, RowSpan).
/// El usuario arrastra la cabecera de una caja para moverla y la esquina inferior derecha para cambiar su
/// tamano; al soltar encaja en la rejilla, y las cajas que estorben se empujan hacia abajo. Si no hay
/// sitio, la caja vuelve a donde estaba. La disposicion se guarda en config.json (AppSettingsService.Layouts).
/// Clic derecho en el tablero: "Restablecer disposicion".
/// </summary>
public class DashboardBoard : Panel
{
    // ===================== Propiedades =====================

    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int), typeof(DashboardBoard),
        new FrameworkPropertyMetadata(12, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(int), typeof(DashboardBoard),
        new FrameworkPropertyMetadata(12, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(DashboardBoard),
        new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public int Columns { get => (int)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    public int Rows { get => (int)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>Identificador del tablero: prefijo con el que se guarda su disposicion.</summary>
    public string BoardId { get; set; } = "board";

    /// <summary>Tamano minimo (en celdas) al redimensionar una caja.</summary>
    public int MinColSpan { get; set; } = 2;
    public int MinRowSpan { get; set; } = 3;

    private static DependencyProperty AttachInt(string name, int defaultValue) =>
        DependencyProperty.RegisterAttached(name, typeof(int), typeof(DashboardBoard),
            new FrameworkPropertyMetadata(defaultValue,
                FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static readonly DependencyProperty ColProperty = AttachInt("Col", 0);
    public static readonly DependencyProperty RowProperty = AttachInt("Row", 0);
    public static readonly DependencyProperty ColSpanProperty = AttachInt("ColSpan", 1);
    public static readonly DependencyProperty RowSpanProperty = AttachInt("RowSpan", 1);
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.RegisterAttached("Key", typeof(string), typeof(DashboardBoard), new PropertyMetadata(null));

    public static int GetCol(DependencyObject d) => (int)d.GetValue(ColProperty);
    public static void SetCol(DependencyObject d, int v) => d.SetValue(ColProperty, v);
    public static int GetRow(DependencyObject d) => (int)d.GetValue(RowProperty);
    public static void SetRow(DependencyObject d, int v) => d.SetValue(RowProperty, v);
    public static int GetColSpan(DependencyObject d) => (int)d.GetValue(ColSpanProperty);
    public static void SetColSpan(DependencyObject d, int v) => d.SetValue(ColSpanProperty, v);
    public static int GetRowSpan(DependencyObject d) => (int)d.GetValue(RowSpanProperty);
    public static void SetRowSpan(DependencyObject d, int v) => d.SetValue(RowSpanProperty, v);
    public static string? GetKey(DependencyObject d) => (string?)d.GetValue(KeyProperty);
    public static void SetKey(DependencyObject d, string? v) => d.SetValue(KeyProperty, v);

    // ===================== Estado =====================

    private readonly record struct Cell(int C, int R, int CS, int RS);

    private readonly Dictionary<BoardBox, Cell> _defaults = new();
    private bool _initialized;

    private BoardBox? _pressBox;
    private bool _resize;
    private bool _dragging;
    private Point _pressPoint;
    private Rect _startRect;
    private Rect? _dragRect;
    private Cell? _target;
    private List<Slot>? _plan; // disposicion resultante si se soltara ahora (null = no cabe)

    public DashboardBoard()
    {
        Background = WpfBrushes.Transparent; // sin fondo no recibe el clic derecho

        var menu = new ContextMenu();
        var reset = new MenuItem { Header = "Restablecer disposicion" };
        reset.Click += (_, _) => ResetLayout();
        menu.Items.Add(reset);
        ContextMenu = menu;

        Loaded += (_, _) => Initialize();
    }

    private IEnumerable<BoardBox> Boxes() => InternalChildren.OfType<BoardBox>();

    private static Cell ReadCell(DependencyObject d) => new(GetCol(d), GetRow(d), GetColSpan(d), GetRowSpan(d));

    private static void WriteCell(DependencyObject d, Cell c)
    {
        SetCol(d, c.C);
        SetRow(d, c.R);
        SetColSpan(d, c.CS);
        SetRowSpan(d, c.RS);
    }

    private string StorageKey(BoardBox box) => $"board.{BoardId}.{GetKey(box)}";

    // ===================== Medida y colocacion =====================

    private static Size Normalize(Size s) =>
        new(double.IsInfinity(s.Width) ? 800 : s.Width, double.IsInfinity(s.Height) ? 400 : s.Height);

    private (double X, double Y) Pitch(Size size) =>
        ((size.Width + Gap) / Math.Max(1, Columns), (size.Height + Gap) / Math.Max(1, Rows));

    private Rect CellRect(Cell c, Size size)
    {
        var (px, py) = Pitch(size);
        return new Rect(c.C * px, c.R * py, Math.Max(0, c.CS * px - Gap), Math.Max(0, c.RS * py - Gap));
    }

    private Rect RectFor(UIElement child, Size size)
    {
        if (ReferenceEquals(child, _pressBox) && _dragRect is Rect floating) return floating;
        return CellRect(ReadCell(child), size);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Normalize(availableSize);
        foreach (UIElement child in InternalChildren) child.Measure(RectFor(child, size).Size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in InternalChildren) child.Arrange(RectFor(child, finalSize));
        return finalSize;
    }

    /// <summary>Dibuja la celda destino mientras se arrastra (el "hueco" donde va a encajar la caja).</summary>
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (_target is not Cell t || _pressBox == null) return;

        var rect = CellRect(t, new Size(ActualWidth, ActualHeight));
        var ok = _plan != null;
        var fill = (Brush)Application.Current.FindResource(ok ? "Brush.AccentDim" : "Brush.Surface");
        var line = (Brush)Application.Current.FindResource(ok ? "Brush.Accent" : "Brush.Danger");
        dc.DrawRoundedRectangle(fill, new Pen(line, 1.5), rect, 8, 8);
    }

    // ===================== Arrastrar y redimensionar =====================

    internal void BeginPress(BoardBox box, MouseButtonEventArgs e, bool resize)
    {
        if (_pressBox != null) return;
        if (!CaptureMouse()) return;

        _pressBox = box;
        _resize = resize;
        _dragging = false;
        _pressPoint = e.GetPosition(this);
        _startRect = CellRect(ReadCell(box), new Size(ActualWidth, ActualHeight));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_pressBox == null) return;

        var p = e.GetPosition(this);
        var dx = p.X - _pressPoint.X;
        var dy = p.Y - _pressPoint.Y;

        if (!_dragging)
        {
            if (Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(dy) < SystemParameters.MinimumVerticalDragDistance) return;

            _dragging = true;
            SetZIndex(_pressBox, 100); // la caja arrastrada va por encima de las demas
        }

        var size = new Size(ActualWidth, ActualHeight);
        var (px, py) = Pitch(size);
        var current = ReadCell(_pressBox);

        if (!_resize)
        {
            var x = Clamp(_startRect.X + dx, 0, size.Width - _startRect.Width);
            var y = Clamp(_startRect.Y + dy, 0, size.Height - _startRect.Height);
            _dragRect = new Rect(x, y, _startRect.Width, _startRect.Height);

            var c = (int)Clamp(Math.Round(x / px), 0, Columns - current.CS);
            var r = (int)Clamp(Math.Round(y / py), 0, Rows - current.RS);
            _target = new Cell(c, r, current.CS, current.RS);
        }
        else
        {
            var w = Clamp(_startRect.Width + dx, MinColSpan * px - Gap, size.Width - _startRect.X);
            var h = Clamp(_startRect.Height + dy, MinRowSpan * py - Gap, size.Height - _startRect.Y);
            _dragRect = new Rect(_startRect.X, _startRect.Y, w, h);

            var cs = (int)Clamp(Math.Round((w + Gap) / px), MinColSpan, Columns - current.C);
            var rs = (int)Clamp(Math.Round((h + Gap) / py), MinRowSpan, Rows - current.R);
            _target = new Cell(current.C, current.R, cs, rs);
        }

        _plan = _target is Cell tc ? Plan(_pressBox, tc) : null;

        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_pressBox == null) return;

        var box = _pressBox;
        var wasDragging = _dragging;
        var resize = _resize;
        var plan = _plan;
        var point = e.GetPosition(this);

        EndPress();

        if (!wasDragging)
        {
            if (!resize) box.OnHeaderClick(point); // un clic sin arrastrar en el titulo abre su menu
            return;
        }

        if (plan != null)
        {
            foreach (var slot in plan) WriteCell(slot.Box, slot.Cell);
            Save();
        }

        InvalidateMeasure();
        InvalidateArrange();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_pressBox != null) EndPress(); // p.ej. Alt+Tab a mitad de un arrastre: se cancela
    }

    private void EndPress()
    {
        var box = _pressBox;
        _pressBox = null;
        _dragging = false;
        _dragRect = null;
        _target = null;
        _plan = null;

        if (box != null) SetZIndex(box, 0);
        if (IsMouseCaptured) ReleaseMouseCapture();

        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    private static double Clamp(double value, double min, double max) =>
        max < min ? min : Math.Min(Math.Max(value, min), max);

    // ===================== Colisiones: empujar hacia abajo =====================

    private sealed class Slot
    {
        public BoardBox Box = null!;
        public Cell Cell;
    }

    private static bool Overlaps(Cell a, Cell b) =>
        a.C < b.C + b.CS && b.C < a.C + a.CS && a.R < b.R + b.RS && b.R < a.R + a.RS;

    private bool Fits(Cell c) => c.C >= 0 && c.R >= 0 && c.CS >= 1 && c.RS >= 1 && c.C + c.CS <= Columns && c.R + c.RS <= Rows;

    private List<Slot> Snapshot() => Boxes().Select(b => new Slot { Box = b, Cell = ReadCell(b) }).ToList();

    /// <summary>Calcula la disposicion que quedaria al soltar la caja en 'target', o null si no cabe.
    /// Primero intenta empujar las cajas estorbo hacia abajo; si no hay sitio, las recoloca en el hueco
    /// libre mas cercano (con lo que dos cajas de tamano parecido se intercambian).</summary>
    private List<Slot>? Plan(BoardBox moved, Cell target)
    {
        if (!Fits(target)) return null;
        return PlanPush(moved, target) ?? PlanDisplace(moved, target);
    }

    private List<Slot>? PlanPush(BoardBox moved, Cell target)
    {
        var slots = Snapshot();
        var mover = slots.First(s => ReferenceEquals(s.Box, moved));
        mover.Cell = target;

        var queue = new Queue<Slot>();
        queue.Enqueue(mover);
        var guard = 0;

        while (queue.Count > 0)
        {
            if (++guard > 1000) return null;

            var a = queue.Dequeue();
            foreach (var b in slots)
            {
                if (ReferenceEquals(b, a) || ReferenceEquals(b, mover)) continue;
                if (!Overlaps(a.Cell, b.Cell)) continue;

                b.Cell = b.Cell with { R = a.Cell.R + a.Cell.RS };
                if (!Fits(b.Cell)) return null;
                queue.Enqueue(b);
            }
        }

        return HasOverlaps(slots) ? null : slots;
    }

    /// <summary>Las cajas que estorban se mueven (con su mismo tamano) al hueco libre mas cercano a donde
    /// estaban. Si el tablero esta lleno, el hueco que deja la caja movida permite intercambiar posiciones.</summary>
    private List<Slot>? PlanDisplace(BoardBox moved, Cell target)
    {
        var slots = Snapshot();
        var mover = slots.First(s => ReferenceEquals(s.Box, moved));
        mover.Cell = target;

        var displaced = slots
            .Where(s => !ReferenceEquals(s, mover) && Overlaps(s.Cell, target))
            .OrderByDescending(s => s.Cell.CS * s.Cell.RS)
            .ToList();

        var occupied = new List<Cell> { target };
        occupied.AddRange(slots.Where(s => !ReferenceEquals(s, mover) && !displaced.Contains(s)).Select(s => s.Cell));

        foreach (var d in displaced)
        {
            var old = d.Cell;
            Cell? best = null;
            var bestDistance = int.MaxValue;

            for (var r = 0; r + old.RS <= Rows; r++)
            {
                for (var c = 0; c + old.CS <= Columns; c++)
                {
                    var candidate = new Cell(c, r, old.CS, old.RS);
                    if (occupied.Any(o => Overlaps(o, candidate))) continue;

                    var distance = Math.Abs(c - old.C) + Math.Abs(r - old.R);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = candidate;
                    }
                }
            }

            if (best is not Cell found) return null;
            d.Cell = found;
            occupied.Add(found);
        }

        return HasOverlaps(slots) ? null : slots;
    }

    private static bool HasOverlaps(List<Slot> slots)
    {
        for (var i = 0; i < slots.Count; i++)
            for (var j = i + 1; j < slots.Count; j++)
                if (Overlaps(slots[i].Cell, slots[j].Cell)) return true;

        return false;
    }

    // ===================== Guardar / cargar / restablecer =====================

    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        foreach (var box in Boxes()) _defaults[box] = ReadCell(box);

        var settings = AppSettingsService.Instance;
        var proposal = new Dictionary<BoardBox, Cell>();

        foreach (var box in Boxes())
        {
            var key = GetKey(box);
            if (!string.IsNullOrEmpty(key) &&
                settings.Layouts.TryGetValue(StorageKey(box), out var v) && v.Length == 4)
            {
                proposal[box] = new Cell((int)v[0], (int)v[1], (int)v[2], (int)v[3]);
            }
            else
            {
                proposal[box] = ReadCell(box);
            }
        }

        // Un archivo guardado con otra rejilla o a medias no debe romper el tablero: se ignora entero.
        if (!IsValid(proposal.Values.ToList())) return;

        foreach (var (box, cell) in proposal) WriteCell(box, cell);
    }

    private bool IsValid(List<Cell> cells)
    {
        if (cells.Any(c => !Fits(c))) return false;

        for (var i = 0; i < cells.Count; i++)
            for (var j = i + 1; j < cells.Count; j++)
                if (Overlaps(cells[i], cells[j])) return false;

        return true;
    }

    private void Save()
    {
        var settings = AppSettingsService.Instance;

        foreach (var box in Boxes())
        {
            if (string.IsNullOrEmpty(GetKey(box))) continue;
            var c = ReadCell(box);
            settings.Layouts[StorageKey(box)] = new double[] { c.C, c.R, c.CS, c.RS };
        }

        settings.Save();
    }

    public void ResetLayout()
    {
        var settings = AppSettingsService.Instance;

        foreach (var (box, cell) in _defaults)
        {
            WriteCell(box, cell);
            if (!string.IsNullOrEmpty(GetKey(box))) settings.Layouts.Remove(StorageKey(box));
        }

        settings.Save();
        InvalidateMeasure();
        InvalidateArrange();
    }
}
