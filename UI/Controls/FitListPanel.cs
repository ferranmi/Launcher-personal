using System.Windows;
using System.Windows.Controls;

namespace Launcher.UI.Controls;

/// <summary>
/// Lista vertical que nunca muestra una fila cortada: enseña solo las filas que caben ENTERAS y reparte el
/// espacio sobrante entre ellas (cada fila crece en proporcion a su altura), asi la lista siempre ocupa todo
/// el alto de la caja. Las filas que no caben se ocultan; HiddenCount dice cuantas son.
/// </summary>
public class FitListPanel : Panel
{
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(FitListPanel),
        new FrameworkPropertyMetadata(0.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>Separacion fija entre filas (las plantillas que ya llevan Margin pueden dejarla en 0).</summary>
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Filas que no caben y se han ocultado en la ultima colocacion.</summary>
    public int HiddenCount { get; private set; }

    public event EventHandler? HiddenCountChanged;

    public FitListPanel() => ClipToBounds = true;

    protected override Size MeasureOverride(Size availableSize)
    {
        double widest = 0;
        double total = 0;
        var count = 0;

        foreach (UIElement child in InternalChildren)
        {
            // Cada fila se mide con su altura natural; luego se decide cuantas caben.
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            widest = Math.Max(widest, child.DesiredSize.Width);
            total += child.DesiredSize.Height;
            count++;
        }

        total += Spacing * Math.Max(0, count - 1);

        var width = double.IsInfinity(availableSize.Width) ? widest : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? total : Math.Min(total, availableSize.Height);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = InternalChildren.Cast<UIElement>().ToList();

        // Cuantas filas caben enteras (la primera se muestra siempre, aunque no quepa).
        var visible = 0;
        double used = 0;
        for (var i = 0; i < children.Count; i++)
        {
            var next = used + (i > 0 ? Spacing : 0) + children[i].DesiredSize.Height;
            if (next > finalSize.Height + 0.5 && visible > 0) break;

            used = next;
            visible++;
        }

        var natural = children.Take(visible).Sum(c => c.DesiredSize.Height);
        var extra = Math.Max(0, finalSize.Height - used);

        double y = 0;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];

            if (i >= visible)
            {
                // Fuera del panel (que recorta): no se ve ni recibe el raton.
                child.Arrange(new Rect(0, finalSize.Height + 1, finalSize.Width, child.DesiredSize.Height));
                continue;
            }

            var height = child.DesiredSize.Height;
            if (natural > 0) height += extra * height / natural;

            child.Arrange(new Rect(0, y, finalSize.Width, height));
            y += height + Spacing;
        }

        SetHidden(children.Count - visible);
        return finalSize;
    }

    private void SetHidden(int hidden)
    {
        if (hidden == HiddenCount) return;

        HiddenCount = hidden;
        // Se avisa fuera de la colocacion: cambiar la interfaz dentro de Arrange provocaria otra pasada.
        Dispatcher.BeginInvoke(new Action(() => HiddenCountChanged?.Invoke(this, EventArgs.Empty)));
    }
}
