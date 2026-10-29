using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Launcher.Core.Services;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Launcher.UI.Controls;

/// <summary>
/// Caja de un <see cref="DashboardBoard"/>: tarjeta con titulo "[ X ]" opcional y contenido SIN scroll
/// (lo que no cabe se recorta; haz la caja mas grande para ver mas). Mover, redimensionar y quitar se
/// hacen desde el modo edicion del tablero (boton "editar"). Fuera de edicion, un clic en el titulo abre
/// el menu TitleTarget. El visual se construye en codigo: no depende de ningun estilo de otro XAML.
/// </summary>
[ContentProperty(nameof(Body))]
public class BoardBox : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(BoardBox),
        new PropertyMetadata("", (d, e) => ((BoardBox)d)._title.Text = (string)e.NewValue));

    /// <summary>Id del modulo que abre un clic en el titulo (games, commands, folders, devices...).</summary>
    public static readonly DependencyProperty TitleTargetProperty = DependencyProperty.Register(
        nameof(TitleTarget), typeof(string), typeof(BoardBox),
        new PropertyMetadata(null, (d, e) => ((BoardBox)d).UpdateTitleCursor()));

    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body), typeof(object), typeof(BoardBox),
        new PropertyMetadata(null, (d, e) => ((BoardBox)d)._body.Content = e.NewValue));

    /// <summary>Contenido extra a la derecha de la cabecera (p.ej. el boton "ver todo" del registro).</summary>
    public static readonly DependencyProperty HeaderExtraProperty = DependencyProperty.Register(
        nameof(HeaderExtra), typeof(object), typeof(BoardBox),
        new PropertyMetadata(null, (d, e) => ((BoardBox)d)._extra.Content = e.NewValue));

    /// <summary>False = caja sin cabecera (p.ej. las tarjetas de metricas). El Title se sigue usando
    /// en el menu "Anadir widget".</summary>
    public static readonly DependencyProperty ShowHeaderProperty = DependencyProperty.Register(
        nameof(ShowHeader), typeof(bool), typeof(BoardBox),
        new PropertyMetadata(true, (d, e) =>
            ((BoardBox)d)._header.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    public static readonly DependencyProperty ContentPaddingProperty = DependencyProperty.Register(
        nameof(ContentPadding), typeof(Thickness), typeof(BoardBox),
        new PropertyMetadata(new Thickness(12), (d, e) => ((BoardBox)d)._card.Padding = (Thickness)e.NewValue));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? TitleTarget { get => (string?)GetValue(TitleTargetProperty); set => SetValue(TitleTargetProperty, value); }
    public object? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }
    public object? HeaderExtra { get => GetValue(HeaderExtraProperty); set => SetValue(HeaderExtraProperty, value); }
    public bool ShowHeader { get => (bool)GetValue(ShowHeaderProperty); set => SetValue(ShowHeaderProperty, value); }
    public Thickness ContentPadding { get => (Thickness)GetValue(ContentPaddingProperty); set => SetValue(ContentPaddingProperty, value); }

    private readonly TextBlock _title = new();
    private readonly ContentPresenter _extra = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentPresenter _body = new();
    private readonly Grid _header = new();
    private readonly Border _card = new();

    private DashboardBoard? Board => Parent as DashboardBoard;

    public BoardBox()
    {
        var app = Application.Current;

        _title.FontFamily = (FontFamily)app.FindResource("Font.Mono");
        _title.FontSize = 10.5;
        _title.Foreground = (Brush)app.FindResource("Brush.TextDim");
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.MouseEnter += (_, _) => { if (!string.IsNullOrEmpty(TitleTarget)) _title.Foreground = (Brush)app.FindResource("Brush.Accent"); };
        _title.MouseLeave += (_, _) => _title.Foreground = (Brush)app.FindResource("Brush.TextDim");

        // Fuera de edicion el clic va directo al menu. En edicion el tablero se queda el clic
        // (PreviewMouseLeftButtonDown con captura) y esto no llega a dispararse.
        _title.MouseLeftButtonUp += (_, e) =>
        {
            if (string.IsNullOrEmpty(TitleTarget)) return;
            Navigator.GoTo(TitleTarget);
            e.Handled = true;
        };

        _header.Background = WpfBrushes.Transparent;
        _header.Margin = new Thickness(0, 0, 0, 10);
        _header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _header.Children.Add(_title);
        Grid.SetColumn(_extra, 1);
        _header.Children.Add(_extra);

        var bodyHost = new Grid { ClipToBounds = true }; // sin scroll: lo que no cabe se recorta
        bodyHost.Children.Add(_body);
        Grid.SetRow(bodyHost, 1);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(_header);
        grid.Children.Add(bodyHost);

        _card.Style = (Style)app.FindResource("CardStyle");
        _card.Padding = new Thickness(12);
        _card.ClipToBounds = true;
        _card.Child = grid;
        Content = _card;
    }

    private void UpdateTitleCursor() =>
        _title.Cursor = string.IsNullOrEmpty(TitleTarget) ? null : Cursors.Hand;

    /// <summary>Un clic sin arrastrar sobre el titulo (point en coordenadas del tablero). Se conserva por
    /// compatibilidad; el tablero solo lo llama fuera del modo edicion.</summary>
    internal void OnHeaderClick(Point boardPoint)
    {
        if (string.IsNullOrEmpty(TitleTarget) || Board == null) return;

        var p = Board.TranslatePoint(boardPoint, _title);
        var inside = p.X >= 0 && p.Y >= 0 && p.X <= _title.ActualWidth && p.Y <= _title.ActualHeight;
        if (inside) Navigator.GoTo(TitleTarget);
    }
}
