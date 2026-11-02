using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Launcher.Core.Services;

namespace Launcher.Modules.Dashboard.Widgets;

/// <summary>Uso de cada disco fijo: barra y GB usados/totales. Amarillo a partir del 85 %, rojo desde el 95 %.</summary>
public sealed class StorageWidget : UserControl
{
    private readonly StackPanel _rows = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(10) };

    public StorageWidget()
    {
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _rows };

        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) =>
        {
            Refresh();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Refresh()
    {
        try
        {
            _rows.Children.Clear();

            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            {
                var totalGb = drive.TotalSize / 1024d / 1024 / 1024;
                var usedGb = totalGb - drive.TotalFreeSpace / 1024d / 1024 / 1024;
                var percent = totalGb > 0 ? usedGb / totalGb * 100 : 0;

                var brush = percent >= 95 ? "Brush.Danger" : percent >= 85 ? "Brush.Warning" : "Brush.Accent";

                var head = new Grid();
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var name = Kit.Label(drive.Name.TrimEnd('\\'), 12, mono: true);
                var amount = Kit.Label($"{usedGb:0.#} / {totalGb:0.#} GB", 10, "Brush.TextDim", mono: true);
                Grid.SetColumn(amount, 1);
                head.Children.Add(name);
                head.Children.Add(amount);

                var bar = Kit.Bar(percent, Kit.Br(brush));
                bar.Margin = new Thickness(0, 5, 0, 0);

                var item = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
                item.Children.Add(head);
                item.Children.Add(bar);
                _rows.Children.Add(item);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo el widget de almacenamiento", ex);
        }
    }
}
