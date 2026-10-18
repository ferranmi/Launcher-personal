using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Launcher.Core.Services;

/// <summary>
/// Recuerda el tamano de las filas/columnas de un Grid que el usuario cambia arrastrando un GridSplitter:
/// tanto las proporcionales ("*") como las de tamano fijo en pixeles; las "Auto" no se tocan.
/// Uso: LayoutPersistence.Attach(MiGrid, "clave") (filas) o Attach(MiGrid, "clave", columns: true).
/// Pon MinHeight/MaxHeight (o MinWidth/MaxWidth) en las definiciones para que nada se desborde.
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
        ? grid.ColumnDefinitions.Select(c => c.Width.IsAuto ? 0 : c.ActualWidth).ToArray()
        : grid.RowDefinitions.Select(r => r.Height.IsAuto ? 0 : r.ActualHeight).ToArray();

    private static void Write(Grid grid, double[] values, bool columns)
    {
        if (columns)
        {
            for (var i = 0; i < Math.Min(values.Length, grid.ColumnDefinitions.Count); i++)
            {
                var def = grid.ColumnDefinitions[i];
                if (values[i] <= 0 || def.Width.IsAuto) continue;
                def.Width = new GridLength(values[i], def.Width.IsStar ? GridUnitType.Star : GridUnitType.Pixel);
            }
        }
        else
        {
            for (var i = 0; i < Math.Min(values.Length, grid.RowDefinitions.Count); i++)
            {
                var def = grid.RowDefinitions[i];
                if (values[i] <= 0 || def.Height.IsAuto) continue;
                def.Height = new GridLength(values[i], def.Height.IsStar ? GridUnitType.Star : GridUnitType.Pixel);
            }
        }
    }
}
