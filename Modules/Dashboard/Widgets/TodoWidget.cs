using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Launcher.Core.Services;

namespace Launcher.Modules.Dashboard.Widgets;

public sealed class TodoItem
{
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

/// <summary>Lista de tareas local (%AppData%\Launcher\todo-local.json). Escribe y pulsa Enter para anadir;
/// clic en la casilla o en el texto para marcarla; "x" para borrarla.</summary>
public sealed class TodoWidget : UserControl
{
    private static readonly string FilePath = Path.Combine(AppSettingsService.AppFolder, "todo-local.json");

    private readonly List<TodoItem> _items = new();
    private readonly StackPanel _list = new();
    private readonly TextBlock _count = Kit.Label("", 10, "Brush.TextDim", mono: true);
    private readonly TextBox _input = Kit.Input("nueva tarea + Enter");

    public TodoWidget()
    {
        Load();

        _input.Margin = new Thickness(0, 0, 0, 8);
        _input.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;

            var text = _input.Text.Trim();
            if (text.Length > 0)
            {
                _items.Add(new TodoItem { Text = text });
                _input.Clear();
                Commit();
            }

            e.Handled = true;
        };

        var clear = Kit.ActionButton("limpiar hechas", (_, _) =>
        {
            _items.RemoveAll(i => i.Done);
            Commit();
        });
        clear.FontSize = 10;
        clear.Padding = new Thickness(8, 3, 8, 3);

        var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _count.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(clear, 1);
        footer.Children.Add(_count);
        footer.Children.Add(clear);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _list };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(scroll, 1);
        Grid.SetRow(footer, 2);
        root.Children.Add(_input);
        root.Children.Add(scroll);
        root.Children.Add(footer);

        Content = root;
        Render();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var items = JsonSerializer.Deserialize<List<TodoItem>>(File.ReadAllText(FilePath));
            if (items != null) _items.AddRange(items);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudieron leer las tareas", ex);
        }
    }

    private void Commit()
    {
        try
        {
            Directory.CreateDirectory(AppSettingsService.AppFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudieron guardar las tareas", ex);
        }

        Render();
    }

    private void Render()
    {
        _list.Children.Clear();

        foreach (var item in _items)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var box = new TextBlock
            {
                Text = item.Done ? "\uE73A" : "\uE739",
                FontFamily = Kit.Icons,
                FontSize = 15,
                Foreground = Kit.Br(item.Done ? "Brush.Accent" : "Brush.TextSecondary"),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };

            var text = new TextBlock
            {
                Text = item.Text,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Foreground = Kit.Br(item.Done ? "Brush.TextDim" : "Brush.TextPrimary"),
                TextDecorations = item.Done ? TextDecorations.Strikethrough : null
            };
            Grid.SetColumn(text, 1);

            var delete = new TextBlock
            {
                Text = "\u00D7",
                FontSize = 16,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Foreground = Kit.Br("Brush.TextDim"),
                ToolTip = "Borrar"
            };
            Grid.SetColumn(delete, 2);

            var captured = item;
            box.MouseLeftButtonUp += (_, _) => { captured.Done = !captured.Done; Commit(); };
            text.MouseLeftButtonUp += (_, _) => { captured.Done = !captured.Done; Commit(); };
            delete.MouseLeftButtonUp += (_, _) => { _items.Remove(captured); Commit(); };

            row.Children.Add(box);
            row.Children.Add(text);
            row.Children.Add(delete);
            _list.Children.Add(row);
        }

        _count.Text = $"{_items.Count(i => i.Done)}/{_items.Count} hechas";
    }
}
