using System.Windows;
using System.Windows.Controls;

namespace Launcher.UI.Controls;

/// <summary>
/// Cuadricula de tarjetas (sustituye a FluidTilePanel con las mismas propiedades). Las columnas salen del ancho
/// (MinItemWidth) y las tarjetas se reparten todo el ancho. En vertical NUNCA se ve una fila cortada: si no caben
/// todas las filas, se ocultan las ultimas y las que quedan crecen en alto hasta ocupar todo el espacio. Si
/// caben todas, mantienen su altura natural (ancho x HeightRatio + ExtraHeight).
/// </summary>
public class FitGridPanel : Panel
{
    private static DependencyProperty Register(string name, double defaultValue) =>
        DependencyProperty.Register(name, typeof(double), typeof(FitGridPanel),
            new FrameworkPropertyMetadata(defaultValue,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty MinItemWidthProperty = Register(nameof(MinItemWidth), 80);
    public static readonly DependencyProperty SpacingProperty = Register(nameof(Spacing), 8);
    public static readonly DependencyProperty HeightRatioProperty = Register(nameof(HeightRatio), 1);
    public static readonly DependencyProperty ExtraHeightProperty = Register(nameof(ExtraHeight), 0);

    public double MinItemWidth { get => (double)GetValue(MinItemWidthProperty); set => SetValue(MinItemWidthProperty, value); }
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Alto natural de una tarjeta = su ancho x HeightRatio + ExtraHeight.</summary>
    public double HeightRatio { get => (double)GetValue(HeightRatioProperty); set => SetValue(HeightRatioProperty, value); }
    public double ExtraHeight { get => (double)GetValue(ExtraHeightProperty); set => SetValue(ExtraHeightProperty, value); }

    private readonly record struct Metrics(int Cols, double ItemW, double ItemH, int Rows);

    public FitGridPanel() => ClipToBounds = true;

    private Metrics Compute(Size available, int count)
    {
        var gap = Spacing;
        var width = double.IsInfinity(available.Width) ? 600 : available.Width;

        var cols = Math.Max(1, (int)Math.Floor((width + gap) / (Math.Max(1, MinItemWidth) + gap)));
        var itemW = Math.Max(1, (width - (cols - 1) * gap) / cols);
        var idealH = itemW * HeightRatio + ExtraHeight;

        var rowsTotal = count == 0 ? 0 : (int)Math.Ceiling(count / (double)cols);
        var rows = rowsTotal;
        var itemH = idealH;

        if (!double.IsInfinity(available.Height) && rowsTotal > 0)
        {
            // Filas naturales que caben, con decimales (p.ej. 1,6 = una fila y media de espacio).
            var exact = (available.Height + gap) / (idealH + gap);

            if (rowsTotal > exact)
            {
                // No caben todas con su alto natural: se elige entre redondear hacia abajo (tarjetas mas altas)
                // o hacia arriba (mas bajas), la opcion que las deje mas cerca de su proporcion natural,
                // y se ajustan para llenar justo el alto. Asi 1,6 filas pasan a ser 2 (algo mas bajas) y no 1
                // fila estirada un 60 %.
                var down = Math.Max(1, (int)Math.Floor(exact));
                var up = down + 1;

                rows = Math.Min(rowsTotal, exact * exact <= down * up ? down : up);
                itemH = Math.Max(1, (available.Height - (rows - 1) * gap) / rows);
            }
        }

        return new Metrics(cols, itemW, itemH, rows);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var m = Compute(availableSize, InternalChildren.Count);

        foreach (UIElement child in InternalChildren) child.Measure(new Size(m.ItemW, m.ItemH));

        var width = double.IsInfinity(availableSize.Width) ? m.Cols * m.ItemW + (m.Cols - 1) * Spacing : availableSize.Width;
        var height = m.Rows > 0 ? m.Rows * m.ItemH + (m.Rows - 1) * Spacing : 0;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var m = Compute(finalSize, InternalChildren.Count);
        var shown = m.Rows * m.Cols;

        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];

            if (i >= shown)
            {
                // Fuera del panel (que recorta): no se ve ni recibe el raton.
                child.Arrange(new Rect(0, finalSize.Height + 1, m.ItemW, m.ItemH));
                continue;
            }

            var row = i / m.Cols;
            var col = i % m.Cols;
            child.Arrange(new Rect(col * (m.ItemW + Spacing), row * (m.ItemH + Spacing), m.ItemW, m.ItemH));
        }

        return finalSize;
    }
}
