using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Launcher.Core.Services;

/// <summary>
/// Recuerda las alturas (o anchos) de las filas/columnas "*" de un Grid cuando el
/// usuario arrastra un GridSplitter. Uso: LayoutPersistence.Attach(MiGrid, "home.right").
/// Pon MinHeight/MinWidth en las definiciones para que nada se colapse del todo.
/// </summary>
public static class LayoutPersistence
{
    public static void Attach(Grid grid, string key, bool columns = false)
    {
        var settings = AppSettingsService.Instance;

        if (settings.Layouts.TryGetValue(key, out var saved)) Write(grid, saved, columns);

        grid.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) =>
        {
            var values = Read(grid, columns);
            if (values.All(v => v <= 0)) return;

            settings.Layouts[key] = values;
            settings.Save();
        }));
    }

    public static void Reset(string key)
    {
        var settings = AppSettingsService.Instance;
        if (settings.Layouts.Remove(key)) settings.Save();
    }

    private static double[] Read(Grid grid, bool columns) => columns
        ? grid.ColumnDefinitions.Select(c => c.Width.IsStar ? c.ActualWidth : 0).ToArray()
        : grid.RowDefinitions.Select(r => r.Height.IsStar ? r.ActualHeight : 0).ToArray();

    private static void Write(Grid grid, double[] values, bool columns)
    {
        if (columns)
        {
            for (var i = 0; i < Math.Min(values.Length, grid.ColumnDefinitions.Count); i++)
            {
                if (values[i] > 0 && grid.ColumnDefinitions[i].Width.IsStar)
                    grid.ColumnDefinitions[i].Width = new GridLength(values[i], GridUnitType.Star);
            }
        }
        else
        {
            for (var i = 0; i < Math.Min(values.Length, grid.RowDefinitions.Count); i++)
            {
                if (values[i] > 0 && grid.RowDefinitions[i].Height.IsStar)
                    grid.RowDefinitions[i].Height = new GridLength(values[i], GridUnitType.Star);
            }
        }
    }
}
