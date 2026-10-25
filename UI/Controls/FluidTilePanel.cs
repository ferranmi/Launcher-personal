using System.Windows;
using System.Windows.Controls;
using Size = System.Windows.Size; // evita la ambiguedad con System.Drawing.Size (usings implicitos de WinForms)

namespace Launcher.UI.Controls;

/// <summary>
/// Panel para rejillas de tarjetas que nunca se cortan ni desaparecen:
///  - Calcula cuantas columnas caben con MinItemWidth (si caben 2,5 usa 2).
///  - Reparte TODO el ancho disponible entre esas columnas, asi las tarjetas crecen
///    de forma proporcional y no queda hueco a la derecha.
///  - Si el recuadro es mas estrecho que una tarjeta, usa 1 columna y la encoge: siempre se ve.
///  - Si el recuadro es mas BAJO que una fila (FitHeight), la tarjeta se encoge manteniendo su
///    proporcion hasta caber entera en vez de quedar recortada o invisible.
///  - El alto de cada tarjeta sale del ancho: alto = ancho * HeightRatio + ExtraHeight.
///
/// Las tarjetas NO deben fijar Width/Height/Margin: el panel les da el tamano exacto.
/// Si una plantilla tiene un cuerpo con proporcion fija (p.ej. una portada) y una linea de
/// texto debajo, usa ExtraHeight para el texto y haz que la portada ocupe el resto (Grid * / Auto).
/// </summary>
public class FluidTilePanel : Panel
{
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(FluidTilePanel),
        new FrameworkPropertyMetadata(84.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(FluidTilePanel),
        new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty HeightRatioProperty = DependencyProperty.Register(
        nameof(HeightRatio), typeof(double), typeof(FluidTilePanel),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ExtraHeightProperty = DependencyProperty.Register(
        nameof(ExtraHeight), typeof(double), typeof(FluidTilePanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FitHeightProperty = DependencyProperty.Register(
        nameof(FitHeight), typeof(bool), typeof(FluidTilePanel),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Ancho minimo deseado de una tarjeta; define cuantas columnas caben.</summary>
    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    /// <summary>Separacion entre tarjetas (horizontal y vertical).</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Alto = ancho * HeightRatio + ExtraHeight (p.ej. 1.3 para portadas verticales).</summary>
    public double HeightRatio
    {
        get => (double)GetValue(HeightRatioProperty);
        set => SetValue(HeightRatioProperty, value);
    }

    /// <summary>Alto fijo extra por tarjeta (p.ej. la linea con el nombre bajo una portada).</summary>
    public double ExtraHeight
    {
        get => (double)GetValue(ExtraHeightProperty);
        set => SetValue(ExtraHeightProperty, value);
    }

    /// <summary>Si el alto disponible es menor que una fila, encoge las tarjetas hasta que quepa
    /// una fila entera. Las filas que no caben debajo se recortan (la caja no tiene scroll).</summary>
    public bool FitHeight
    {
        get => (bool)GetValue(FitHeightProperty);
        set => SetValue(FitHeightProperty, value);
    }

    private (int Columns, double Width, double Height) Compute(Size available)
    {
        var min = Math.Max(1, MinItemWidth);
        var gap = Math.Max(0, Spacing);
        var availableWidth = available.Width;

        // Sin ancho conocido (contenedor sin limite), no se puede repartir: una columna minima.
        if (double.IsInfinity(availableWidth) || double.IsNaN(availableWidth) || availableWidth <= 0)
            availableWidth = min;

        var columns = Math.Max(1, (int)Math.Floor((availableWidth + gap) / (min + gap)));
        var width = Math.Max(1, (availableWidth - gap * (columns - 1)) / columns);
        var height = Math.Max(1, width * HeightRatio + ExtraHeight);

        // Caja mas baja que una fila: se encoge la tarjeta (misma proporcion) para que se vea entera.
        var availableHeight = available.Height;
        if (FitHeight && !double.IsInfinity(availableHeight) && !double.IsNaN(availableHeight)
            && availableHeight > 0 && height > availableHeight)
        {
            height = availableHeight;
            width = Math.Max(1, (height - ExtraHeight) / Math.Max(0.01, HeightRatio));
            columns = Math.Max(1, (int)Math.Floor((availableWidth + gap) / (width + gap)));
        }

        return (columns, width, height);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (columns, width, height) = Compute(availableSize);
        var count = InternalChildren.Count;

        foreach (UIElement child in InternalChildren)
            child.Measure(new Size(width, height));

        var rows = (int)Math.Ceiling(count / (double)columns);
        var gap = Math.Max(0, Spacing);

        var totalWidth = double.IsInfinity(availableSize.Width)
            ? columns * width + gap * (columns - 1)
            : availableSize.Width;
        var totalHeight = rows == 0 ? 0 : rows * height + gap * (rows - 1);

        return new Size(totalWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, width, height) = Compute(finalSize);
        var gap = Math.Max(0, Spacing);

        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;

            InternalChildren[i].Arrange(new Rect(
                column * (width + gap),
                row * (height + gap),
                width,
                height));
        }

        return finalSize;
    }
}
