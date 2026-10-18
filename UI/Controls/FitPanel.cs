using System.Windows;
using System.Windows.Controls;
using Size = System.Windows.Size;

namespace Launcher.UI.Controls;

/// <summary>
/// Panel para listas dentro de una <see cref="BoardBox"/>: coloca los elementos como un WrapPanel
/// (Orientation = Horizontal) o como una lista (Vertical), pero SOLO deja visibles los que caben
/// ENTEROS en el espacio disponible; el resto se aparta fuera del recorte. Asi, al redimensionar una
/// caja nunca queda una tarjeta o una fila cortada por la mitad. Necesita recibir un tamano finito:
/// no lo pongas dentro de un StackPanel ni de un ScrollViewer.
/// </summary>
public class FitPanel : Panel
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(FitPanel),
        new FrameworkPropertyMetadata(Orientation.Horizontal,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Cuantos elementos se han ocultado por no caber (despues del ultimo diseno).</summary>
    public int HiddenCount { get; private set; }

    public FitPanel()
    {
        ClipToBounds = true; // lo apartado queda fuera del recorte: ni se ve ni recibe clics
    }

    protected override Size MeasureOverride(Size available)
    {
        foreach (UIElement child in InternalChildren)
            child.Measure(new Size(available.Width, double.PositiveInfinity));

        var used = Place(available).Used;
        return new Size(
            double.IsInfinity(available.Width) ? used.Width : Math.Min(used.Width, available.Width),
            double.IsInfinity(available.Height) ? used.Height : Math.Min(used.Height, available.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (rects, _, hidden) = Place(finalSize);
        HiddenCount = hidden;

        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            child.Arrange(rects[i] ?? new Rect(-100000, -100000, child.DesiredSize.Width, child.DesiredSize.Height));
        }

        return finalSize;
    }

    private (Rect?[] Rects, Size Used, int Hidden) Place(Size bounds)
    {
        var count = InternalChildren.Count;
        var rects = new Rect?[count];
        var vertical = Orientation == Orientation.Vertical;
        var limit = bounds.Height;

        double x = 0, y = 0, rowHeight = 0, usedWidth = 0;
        var full = false;
        var hidden = 0;

        for (var i = 0; i < count; i++)
        {
            var child = InternalChildren[i];

            if (child.Visibility == Visibility.Collapsed)
            {
                rects[i] = new Rect(0, 0, 0, 0);
                continue;
            }

            if (full)
            {
                hidden++;
                continue;
            }

            var d = child.DesiredSize;

            if (vertical)
            {
                if (!double.IsInfinity(limit) && y + d.Height > limit + 0.5)
                {
                    full = true;
                    hidden++;
                    continue;
                }

                var width = double.IsInfinity(bounds.Width) ? d.Width : bounds.Width;
                rects[i] = new Rect(0, y, width, d.Height);
                y += d.Height;
                usedWidth = Math.Max(usedWidth, width);
            }
            else
            {
                // Salto de fila cuando el elemento no cabe en lo que queda de ancho.
                if (x > 0 && !double.IsInfinity(bounds.Width) && x + d.Width > bounds.Width + 0.5)
                {
                    y += rowHeight;
                    x = 0;
                    rowHeight = 0;
                }

                if (!double.IsInfinity(limit) && y + d.Height > limit + 0.5)
                {
                    full = true;
                    hidden++;
                    continue;
                }

                rects[i] = new Rect(x, y, d.Width, d.Height);
                x += d.Width;
                rowHeight = Math.Max(rowHeight, d.Height);
                usedWidth = Math.Max(usedWidth, x);
            }
        }

        var usedHeight = vertical ? y : y + rowHeight;
        return (rects, new Size(usedWidth, usedHeight), hidden);
    }
}
