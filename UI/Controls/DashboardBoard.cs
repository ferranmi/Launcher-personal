using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Launcher.Core.Services;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Launcher.UI.Controls;

/// <summary>
/// Tablero de cajas libres: una rejilla de Columns x Rows celdas que se reparte todo el espacio
/// disponible. Cada <see cref="BoardBox"/> hijo ocupa un rectangulo de celdas (Col, Row, ColSpan, RowSpan).
///
/// Modo edicion (IsEditing, boton "editar" del dashboard): se ve la rejilla, cada caja muestra su marco,
/// tiradores en los bordes derecho e inferior y en la esquina, su tamano en celdas y un boton para quitarla.
/// Se mueve agarrando cualquier punto de la caja. Al soltar encaja en la rejilla y las cajas que estorben
/// se empujan hacia abajo; si no hay sitio, la caja vuelve a donde estaba. Fuera del modo edicion nada se
/// mueve (no hay arrastres accidentales). La disposicion se guarda en config.json (AppSettingsService.Layouts).
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

    /// <summary>True mientras el usuario edita la disposicion (rejilla visible, tiradores, mover/redimensionar).</summary>
    public static readonly DependencyProperty IsEditingProperty = DependencyProperty.Register(
        nameof(IsEditing), typeof(bool), typeof(DashboardBoard),
        new FrameworkPropertyMetadata(false, (d, e) => ((DashboardBoard)d).OnEditingChanged((bool)e.NewValue)));

    public int Columns { get => (int)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    public int Rows { get => (int)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public bool IsEditing { get => (bool)GetValue(IsEditingProperty); set => SetValue(IsEditingProperty, value); }

    /// <summary>Identificador del tablero: prefijo con el que se guarda su disposicion.</summary>
    public string BoardId { get; set; } = "board";

    /// <summary>Tamano minimo (en celdas) por defecto al redimensionar una caja. Cada caja puede
    /// sobrescribirlo con DashboardBoard.MinCols / DashboardBoard.MinRows.</summary>
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

    /// <summary>Tamano minimo propio de una caja (0 = usar el del tablero).</summary>
    public static readonly DependencyProperty MinColsProperty =
        DependencyProperty.RegisterAttached("MinCols", typeof(int), typeof(DashboardBoard), new PropertyMetadata(0));
    public static readonly DependencyProperty MinRowsProperty =
        DependencyProperty.RegisterAttached("MinRows", typeof(int), typeof(DashboardBoard), new PropertyMetadata(0));

    /// <summary>Caja que empieza oculta (no aparece hasta que el usuario la anade con clic derecho > Anadir widget).</summary>
    public static readonly DependencyProperty StartHiddenProperty =
        DependencyProperty.RegisterAttached("StartHidden", typeof(bool), typeof(DashboardBoard), new PropertyMetadata(false));

    public static bool GetStartHidden(DependencyObject d) => (bool)d.GetValue(StartHiddenProperty);
    public static void SetStartHidden(DependencyObject d, bool v) => d.SetValue(StartHiddenProperty, v);

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
    public static int GetMinCols(DependencyObject d) => (int)d.GetValue(MinColsProperty);
    public static void SetMinCols(DependencyObject d, int v) => d.SetValue(MinColsProperty, v);
    public static int GetMinRows(DependencyObject d) => (int)d.GetValue(MinRowsProperty);
    public static void SetMinRows(DependencyObject d, int v) => d.SetValue(MinRowsProperty, v);

    private int MinColsOf(DependencyObject d) => GetMinCols(d) is > 0 and var v ? v : MinColSpan;
    private int MinRowsOf(DependencyObject d) => GetMinRows(d) is > 0 and var v ? v : MinRowSpan;

    // ===================== Estado =====================

    private readonly record struct Cell(int C, int R, int CS, int RS);

    private enum Zone { Move, Right, Bottom, Corner, Close }

    private const double EdgeGrip = 10; // ancho (px) de la zona de agarre en los bordes derecho e inferior

    private readonly Dictionary<BoardBox, Cell> _defaults = new();
    private readonly Dictionary<string, BoardBox> _removed = new(); // widgets quitados (siguen vivos para poder volver a anadirlos)
    private bool _initialized;

    private BoardBox? _pressBox;
    private bool _resize;
    private bool _growX = true;
    private bool _growY = true;
    private bool _dragging;
    private Point _pressPoint;
    private Rect _startRect;
    private Rect? _dragRect;
    private Cell? _target;
    private List<Slot>? _plan; // disposicion resultante si se soltara ahora (null = no cabe)
    private EditAdorner? _adorner;

    /// <summary>Se dispara tras "Restablecer disposicion" (para que el dashboard restablezca tambien el resto).</summary>
    public event EventHandler? LayoutReset;

    public DashboardBoard()
    {
        Background = WpfBrushes.Transparent; // sin fondo no recibe el clic derecho

        ContextMenu = new ContextMenu();
        ContextMenuOpening += (_, _) => BuildMenu();

        Loaded += (_, _) => Initialize();
        SizeChanged += (_, _) => RefreshOverlay();
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

    /// <summary>En edicion dibuja la rejilla de celdas (se ve en los huecos libres) y, mientras se arrastra,
    /// la celda destino: el "hueco" donde va a encajar la caja.</summary>
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var size = new Size(ActualWidth, ActualHeight);

        if (IsEditing)
        {
            var gridPen = new Pen((Brush)Application.Current.FindResource("Brush.BorderStrong"), 0.5)
            {
                DashStyle = DashStyles.Dot
            };

            for (var r = 0; r < Rows; r++)
                for (var c = 0; c < Columns; c++)
                    dc.DrawRoundedRectangle(null, gridPen, CellRect(new Cell(c, r, 1, 1), size), 4, 4);
        }

        if (_target is not Cell t || _pressBox == null) return;

        var rect = CellRect(t, size);
        var ok = _plan != null;
        var fill = (Brush)Application.Current.FindResource(ok ? "Brush.AccentDim" : "Brush.Surface");
        var line = (Brush)Application.Current.FindResource(ok ? "Brush.Accent" : "Brush.Danger");
        dc.DrawRoundedRectangle(fill, new Pen(line, 1.5), rect, 8, 8);
    }

    // ===================== Modo edicion =====================

    private void OnEditingChanged(bool on)
    {
        ForceCursor = on; // que el cursor de edicion gane al "Hand" de las tarjetas

        if (on)
        {
            _adorner ??= new EditAdorner(this);
            var layer = AdornerLayer.GetAdornerLayer(this);
            if (layer != null && !ReferenceEquals(_adorner.Parent, layer)) layer.Add(_adorner);
        }
        else
        {
            Cursor = null;
            if (_pressBox != null) EndPress();
            if (_adorner != null) AdornerLayer.GetAdornerLayer(this)?.Remove(_adorner);
        }

        InvalidateVisual();
    }

    private void RefreshOverlay() => _adorner?.InvalidateVisual();

    private BoardBox? HitBox(Point p)
    {
        var size = new Size(ActualWidth, ActualHeight);
        return Boxes().FirstOrDefault(b => CellRect(ReadCell(b), size).Contains(p));
    }

    private static Rect CloseRect(Rect r) => new(r.Right - 22, r.Top + 4, 18, 18);

    private static Zone ZoneAt(Rect r, Point p)
    {
        if (CloseRect(r).Contains(p)) return Zone.Close;

        var right = p.X >= r.Right - EdgeGrip;
        var bottom = p.Y >= r.Bottom - EdgeGrip;
        return right && bottom ? Zone.Corner : right ? Zone.Right : bottom ? Zone.Bottom : Zone.Move;
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsEditing || _pressBox != null)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            return;
        }

        var p = e.GetPosition(this);
        var box = HitBox(p);
        if (box == null)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            return;
        }

        var rect = CellRect(ReadCell(box), new Size(ActualWidth, ActualHeight));
        switch (ZoneAt(rect, p))
        {
            case Zone.Close: RemoveBox(box); break;
            case Zone.Right: BeginPress(box, e, resize: true, growX: true, growY: false); break;
            case Zone.Bottom: BeginPress(box, e, resize: true, growX: false, growY: true); break;
            case Zone.Corner: BeginPress(box, e, resize: true); break;
            default: BeginPress(box, e, resize: false); break; // en edicion se mueve agarrando cualquier punto
        }

        e.Handled = true; // el contenido (juegos, comandos...) no recibe el clic mientras se edita
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (!IsEditing || _pressBox != null) return;

        var p = e.GetPosition(this);
        var box = HitBox(p);
        if (box == null)
        {
            Cursor = null;
            return;
        }

        Cursor = ZoneAt(CellRect(ReadCell(box), new Size(ActualWidth, ActualHeight)), p) switch
        {
            Zone.Close => Cursors.Hand,
            Zone.Right => Cursors.SizeWE,
            Zone.Bottom => Cursors.SizeNS,
            Zone.Corner => Cursors.SizeNWSE,
            _ => Cursors.SizeAll
        };
    }

    // ===================== Arrastrar y redimensionar =====================

    internal void BeginPress(BoardBox box, MouseButtonEventArgs e, bool resize, bool growX = true, bool growY = true)
    {
        if (_pressBox != null) return;
        if (!CaptureMouse()) return;

        _pressBox = box;
        _resize = resize;
        _growX = growX;
        _growY = growY;
        _dragging = false;
        _pressPoint = e.GetPosition(this);
        _startRect = CellRect(ReadCell(box), new Size(ActualWidth, ActualHeight));
        LogService.Info($"Tablero: pulsado '{GetKey(box)}' ({(resize ? "tamano" : "mover")})");
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
            if (IsEditing) SetZIndex(_pressBox, 100); // la caja arrastrada va por encima de las demas
        }

        if (!IsEditing) return; // fuera del modo edicion no se mueve ni redimensiona nada

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
            var minC = MinColsOf(_pressBox);
            var minR = MinRowsOf(_pressBox);

            var w = _growX ? Clamp(_startRect.Width + dx, minC * px - Gap, size.Width - _startRect.X) : _startRect.Width;
            var h = _growY ? Clamp(_startRect.Height + dy, minR * py - Gap, size.Height - _startRect.Y) : _startRect.Height;
            var cs = _growX ? (int)Clamp(Math.Round((w + Gap) / px), minC, Columns - current.C) : current.CS;
            var rs = _growY ? (int)Clamp(Math.Round((h + Gap) / py), minR, Rows - current.R) : current.RS;
            _target = new Cell(current.C, current.R, cs, rs);

            // La caja salta de celda en celda mientras arrastras (no queda a medias): lo que ves es lo que queda.
            _dragRect = CellRect(_target.Value, size);
        }

        _plan = _target is Cell tc ? Plan(_pressBox, tc) : null;

        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
        RefreshOverlay();
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
        LogService.Info($"Tablero: soltado '{GetKey(box)}' arrastrando={wasDragging} cabe={plan != null}");

        if (!wasDragging)
        {
            if (!resize && !IsEditing) box.OnHeaderClick(point); // un clic sin arrastrar en el titulo abre su menu
            return;
        }

        if (plan != null && IsEditing)
        {
            foreach (var slot in plan) WriteCell(slot.Box, slot.Cell);
            Save();
        }

        InvalidateMeasure();
        InvalidateArrange();
        RefreshOverlay();
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
        RefreshOverlay();
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

    // ===================== Quitar y anadir widgets =====================

    private string HiddenKey(BoardBox box) => StorageKey(box) + ".hidden";

    /// <summary>Abre el menu (Anadir widget / Restablecer) junto a un control, p.ej. el boton "+ widget" del Dashboard.</summary>
    public void ShowMenu(FrameworkElement anchor)
    {
        BuildMenu();
        var menu = ContextMenu!;
        menu.PlacementTarget = anchor;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void BuildMenu()
    {
        var menu = ContextMenu!;
        menu.Items.Clear();

        var add = new MenuItem { Header = "Anadir widget", IsEnabled = _removed.Count > 0 };
        foreach (var removed in _removed.Values.OrderBy(b => b.Title))
        {
            var box = removed;
            var item = new MenuItem { Header = box.Title.Trim('[', ']', ' ') };
            item.Click += (_, _) =>
            {
                if (!AddBox(box))
                {
                    MessageBox.Show(Window.GetWindow(this),
                        "No hay hueco libre en el tablero. Reduce o quita otra caja y vuelve a intentarlo.",
                        "Anadir widget", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            add.Items.Add(item);
        }

        menu.Items.Add(add);
        menu.Items.Add(new Separator());

        var reset = new MenuItem { Header = "Restablecer disposicion" };
        reset.Click += (_, _) => ResetLayout();
        menu.Items.Add(reset);
    }

    /// <summary>Quita la caja del tablero (sigue disponible en "Anadir widget"). Se recuerda entre sesiones.</summary>
    internal void RemoveBox(BoardBox box)
    {
        var key = GetKey(box);
        if (string.IsNullOrEmpty(key) || !Children.Contains(box)) return;

        Save(); // guarda antes la posicion de todas, para que al volver recupere la suya

        Children.Remove(box);
        _removed[key] = box;

        var settings = AppSettingsService.Instance;
        settings.Layouts[HiddenKey(box)] = new double[] { 1 };
        settings.Save();

        InvalidateMeasure();
        InvalidateArrange();
        RefreshOverlay();
    }

    /// <summary>Vuelve a poner una caja quitada: en su ultima posicion si sigue libre, si no en la de fabrica,
    /// y si tampoco en el primer hueco libre donde quepa. Devuelve false si no hay sitio.</summary>
    public bool AddBox(BoardBox box)
    {
        var key = GetKey(box);
        if (string.IsNullOrEmpty(key) || Children.Contains(box)) return false;

        var settings = AppSettingsService.Instance;
        var occupied = Boxes().Select(ReadCell).ToList();
        bool Free(Cell c) => Fits(c) && !occupied.Any(o => Overlaps(o, c));

        Cell? spot = null;

        if (settings.Layouts.TryGetValue(StorageKey(box), out var v) && v.Length == 4)
        {
            var saved = new Cell((int)v[0], (int)v[1], (int)v[2], (int)v[3]);
            if (Free(saved)) spot = saved;
        }

        if (spot == null && _defaults.TryGetValue(box, out var factory) && Free(factory)) spot = factory;

        var size = ReadCell(box);
        spot ??= FindFreeSpot(size.CS, size.RS, Free);
        spot ??= FindFreeSpot(MinColsOf(box), MinRowsOf(box), Free);

        if (spot is not Cell found) return false;

        WriteCell(box, found);
        _removed.Remove(key);
        Children.Add(box);

        settings.Layouts[HiddenKey(box)] = new double[] { 0 };
        Save();

        InvalidateMeasure();
        InvalidateArrange();
        RefreshOverlay();
        return true;
    }

    private Cell? FindFreeSpot(int colSpan, int rowSpan, Func<Cell, bool> free)
    {
        for (var r = 0; r + rowSpan <= Rows; r++)
        {
            for (var c = 0; c + colSpan <= Columns; c++)
            {
                var candidate = new Cell(c, r, colSpan, rowSpan);
                if (free(candidate)) return candidate;
            }
        }

        return null;
    }

    // ===================== Guardar / cargar / restablecer =====================

    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        foreach (var box in Boxes()) _defaults[box] = ReadCell(box);

        var settings = AppSettingsService.Instance;

        // Cajas quitadas por el usuario (o que empiezan ocultas): salen del tablero antes de calcular nada.
        foreach (var box in Boxes().ToList())
        {
            var key = GetKey(box);
            if (string.IsNullOrEmpty(key)) continue;

            var hidden = settings.Layouts.TryGetValue(HiddenKey(box), out var h) && h.Length > 0
                ? h[0] > 0
                : GetStartHidden(box);
            if (!hidden) continue;

            Children.Remove(box);
            _removed[key] = box;
        }

        // Posiciones guardadas por el usuario. Un archivo con otra rejilla o a medias no debe romper el
        // tablero: si lo guardado no es coherente, se ignora entero y todas las cajas usan su sitio de fabrica.
        var saved = new Dictionary<BoardBox, Cell>();
        foreach (var box in Boxes())
        {
            var key = GetKey(box);
            if (!string.IsNullOrEmpty(key) &&
                settings.Layouts.TryGetValue(StorageKey(box), out var v) && v.Length == 4)
            {
                saved[box] = new Cell((int)v[0], (int)v[1], (int)v[2], (int)v[3]);
            }
        }

        if (!IsValid(saved.Values.ToList())) saved.Clear();

        foreach (var (box, cell) in saved) WriteCell(box, cell);
        var occupied = saved.Values.ToList();

        // Cajas sin posicion guardada (p.ej. un widget nuevo tras actualizar): su sitio de fabrica si esta
        // libre; si no, el primer hueco donde quepa. Si el tablero esta lleno, quedan fuera (siguen en
        // "Anadir widget") hasta que haya sitio; no se marcan como quitadas por el usuario.
        bool Free(Cell c) => Fits(c) && !occupied.Any(o => Overlaps(o, c));

        foreach (var box in Boxes().Where(b => !saved.ContainsKey(b)).ToList())
        {
            var key = GetKey(box);
            var size = ReadCell(box);

            Cell? spot = Free(size) ? size : null;
            spot ??= FindFreeSpot(size.CS, size.RS, Free);
            spot ??= FindFreeSpot(MinColsOf(box), MinRowsOf(box), Free);

            if (spot is Cell found)
            {
                WriteCell(box, found);
                occupied.Add(found);
            }
            else if (!string.IsNullOrEmpty(key))
            {
                LogService.Info($"Tablero: sin hueco para '{key}'; queda disponible en Anadir widget");
                Children.Remove(box);
                _removed[key] = box;
            }
        }
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
            if (!string.IsNullOrEmpty(GetKey(box)))
            {
                settings.Layouts.Remove(StorageKey(box));
                settings.Layouts.Remove(HiddenKey(box));
            }
        }

        // Cada caja vuelve a estar como de fabrica: visibles las normales, ocultas las StartHidden.
        foreach (var box in _defaults.Keys.ToList())
        {
            var key = GetKey(box);
            if (string.IsNullOrEmpty(key)) continue;

            var shouldHide = GetStartHidden(box);
            var isRemoved = _removed.ContainsKey(key);

            if (shouldHide && !isRemoved) { Children.Remove(box); _removed[key] = box; }
            else if (!shouldHide && isRemoved) { _removed.Remove(key); Children.Add(box); }
        }

        settings.Save();
        InvalidateMeasure();
        InvalidateArrange();
        RefreshOverlay();
        LayoutReset?.Invoke(this, EventArgs.Empty);
    }

    // ===================== Capa de edicion (marco, tiradores, tamano, quitar) =====================

    /// <summary>Se dibuja por encima del contenido de las cajas (en la capa de adorners); no recibe el raton:
    /// los clics los gestiona el propio tablero con ZoneAt.</summary>
    private sealed class EditAdorner : Adorner
    {
        private readonly DashboardBoard _b;

        public EditAdorner(DashboardBoard board) : base(board)
        {
            _b = board;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            Brush Res(string key) => (Brush)Application.Current.FindResource(key);

            var accent = Res("Brush.Accent");
            var surface = Res("Brush.Surface");
            var danger = Res("Brush.Danger");
            var text = Res("Brush.TextPrimary");
            var typeface = new Typeface((FontFamily)Application.Current.FindResource("Font.Mono"),
                FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            var size = new Size(_b.ActualWidth, _b.ActualHeight);
            var dashed = new Pen(accent, 1) { DashStyle = DashStyles.Dash };
            var closePen = new Pen(danger, 1.5);
            var pillPen = new Pen(accent, 1);

            foreach (var box in _b.Boxes())
            {
                var pressed = ReferenceEquals(box, _b._pressBox);
                var cell = pressed && _b._target is Cell t ? t : ReadCell(box);
                var r = _b.RectFor(box, size);

                dc.DrawRoundedRectangle(null, dashed, r, 8, 8);

                // tiradores: borde derecho, borde inferior y esquina
                dc.DrawRoundedRectangle(accent, null, new Rect(r.Right - 5, r.Top + r.Height / 2 - 14, 6, 28), 3, 3);
                dc.DrawRoundedRectangle(accent, null, new Rect(r.Left + r.Width / 2 - 14, r.Bottom - 5, 28, 6), 3, 3);
                dc.DrawRoundedRectangle(accent, null, new Rect(r.Right - 9, r.Bottom - 9, 12, 12), 3, 3);

                // boton quitar (misma zona que CloseRect)
                var cr = CloseRect(r);
                var centre = new Point(cr.X + cr.Width / 2, cr.Y + cr.Height / 2);
                dc.DrawEllipse(surface, closePen, centre, 9, 9);
                dc.DrawLine(closePen, new Point(centre.X - 3.5, centre.Y - 3.5), new Point(centre.X + 3.5, centre.Y + 3.5));
                dc.DrawLine(closePen, new Point(centre.X - 3.5, centre.Y + 3.5), new Point(centre.X + 3.5, centre.Y - 3.5));

                // etiqueta con el tamano en celdas
                var label = new FormattedText($"{cell.CS}\u00D7{cell.RS}", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, typeface, 10, text, dpi);
                var pill = new Rect(r.Left + 8, r.Bottom - 24, label.Width + 12, 16);
                dc.DrawRoundedRectangle(surface, pillPen, pill, 4, 4);
                dc.DrawText(label, new Point(pill.X + 6, pill.Y + 2));
            }
        }
    }
}
