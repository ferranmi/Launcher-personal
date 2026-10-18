using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Launcher.Core.Services;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Launcher.UI.Controls;

/// <summary>
/// Caja de un <see cref="DashboardBoard"/>: tarjeta con titulo "[ X ]" en la cabecera y contenido SIN scroll
/// (lo que no cabe se recorta; haz la caja mas grande para ver mas). Arrastrar la cabecera mueve la caja,
/// arrastrar la esquina inferior derecha la redimensiona, y un clic en el titulo abre el menu TitleTarget.
/// El visual se construye en codigo: no depende de ningun estilo definido en otro XAML.
/// </summary>
[ContentProperty(nameof(Body))]
public class BoardBox : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(BoardBox),
        new PropertyMetadata("", (d, e) => ((BoardBox)d)._title.Text = (string)e.NewValue));

    /// <summary>Id del modulo que abre un clic en el titulo (games, commands, folders, devices...).</summary>
    public static readonly DependencyProperty TitleTargetProperty = DependencyProperty.Register(
        nameof(TitleTarget), typeof(string), typeof(BoardBox), new PropertyMetadata(null));

    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body), typeof(object), typeof(BoardBox),
        new PropertyMetadata(null, (d, e) => ((BoardBox)d)._body.Content = e.NewValue));

    /// <summary>Contenido extra a la derecha de la cabecera (p.ej. el boton "ver todo" del registro).</summary>
    public static readonly DependencyProperty HeaderExtraProperty = DependencyProperty.Register(
        nameof(HeaderExtra), typeof(object), typeof(BoardBox),
        new PropertyMetadata(null, (d, e) => ((BoardBox)d)._extra.Content = e.NewValue));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? TitleTarget { get => (string?)GetValue(TitleTargetProperty); set => SetValue(TitleTargetProperty, value); }
    public object? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }
    public object? HeaderExtra { get => GetValue(HeaderExtraProperty); set => SetValue(HeaderExtraProperty, value); }

    private readonly TextBlock _title = new();
    private readonly ContentPresenter _extra = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentPresenter _body = new();

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

        var header = new Grid
        {
            Background = WpfBrushes.Transparent, // para que toda la cabecera reciba el raton
            Cursor = Cursors.SizeAll,
            ToolTip = "Arrastra la cabecera para mover la caja",
            Margin = new Thickness(0, 0, 0, 10)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Asa visible: indica que la cabecera se puede arrastrar para mover la caja.
        var handle = new TextBlock
        {
            Text = "::",
            FontFamily = (FontFamily)app.FindResource("Font.Mono"),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)app.FindResource("Brush.BorderStrong"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        header.MouseEnter += (_, _) => handle.Foreground = (Brush)app.FindResource("Brush.Accent");
        header.MouseLeave += (_, _) => handle.Foreground = (Brush)app.FindResource("Brush.BorderStrong");

        header.Children.Add(handle);
        Grid.SetColumn(_title, 1);
        header.Children.Add(_title);
        Grid.SetColumn(_extra, 2);
        header.Children.Add(_extra);
        header.MouseLeftButtonDown += (_, e) => Board?.BeginPress(this, e, resize: false);

        var bodyHost = new Grid { ClipToBounds = true }; // sin scroll: lo que no cabe se recorta
        bodyHost.Children.Add(_body);
        Grid.SetRow(bodyHost, 1);

        var grip = new Border
        {
            Width = 16,
            Height = 16,
            Background = WpfBrushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -8, -8),
            Cursor = Cursors.SizeNWSE,
            ToolTip = "Arrastra para cambiar el tamano"
        };
        var corner = new Polygon
        {
            Points = new PointCollection { new Point(14, 2), new Point(14, 14), new Point(2, 14) },
            Fill = (Brush)app.FindResource("Brush.BorderStrong"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        grip.Child = corner;
        grip.MouseEnter += (_, _) => corner.Fill = (Brush)app.FindResource("Brush.Accent");
        grip.MouseLeave += (_, _) => corner.Fill = (Brush)app.FindResource("Brush.BorderStrong");
        grip.MouseLeftButtonDown += (_, e) => Board?.BeginPress(this, e, resize: true);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(header);
        grid.Children.Add(bodyHost);
        grid.Children.Add(grip);
        Grid.SetRowSpan(grip, 2);

        Content = new Border
        {
            Style = (Style)app.FindResource("CardStyle"),
            Padding = new Thickness(12),
            ClipToBounds = true,
            Child = grid
        };
    }

    /// <summary>Un clic sin arrastrar sobre el titulo abre el menu asociado (point en coordenadas del tablero).</summary>
    internal void OnHeaderClick(Point boardPoint)
    {
        if (string.IsNullOrEmpty(TitleTarget) || Board == null) return;

        var p = Board.TranslatePoint(boardPoint, _title);
        var inside = p.X >= 0 && p.Y >= 0 && p.X <= _title.ActualWidth && p.Y <= _title.ActualHeight;
        if (inside) Navigator.GoTo(TitleTarget);
    }
}
