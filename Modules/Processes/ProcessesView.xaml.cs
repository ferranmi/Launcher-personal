using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Launcher.Core.Services;

namespace Launcher.Modules.Processes;

public partial class ProcessesView : UserControl
{
    private enum SortColumn { Name, Cpu, Ram, Gpu, Vram, Status }

    /// <summary>Intervalo cuando el ajuste de refresco esta en "Auto": suficiente para
    /// que la delta de CPU sea estable sin saturar la relectura de los contadores de GPU.</summary>
    private const double AutoRefreshSeconds = 1.5;

    private readonly ProcessMonitorService _service = new();
    private readonly DispatcherTimer _timer;

    private List<ProcessGroup> _groups = new();

    /// <summary>Nombres (en minuscula) de los grupos que el usuario ha expandido a
    /// mano. Persiste entre refrescos aunque las instancias de ProcessGroup se
    /// reconstruyan en cada tick.</summary>
    private readonly HashSet<string> _expandedGroups = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Totales mostrados bajo el nombre de cada columna (Proceso, CPU, RAM, GPU, VRAM).</summary>
    private readonly System.Windows.Controls.TextBlock[] _headerTotals = new System.Windows.Controls.TextBlock[5];

    private SortColumn _sortColumn = SortColumn.Name;
    private bool _sortAscending = true;
    private string _search = "";

    public ProcessesView()
    {
        InitializeComponent();
        BuildHeaders();

        _timer = new DispatcherTimer { Interval = CurrentInterval() };
        _timer.Tick += async (_, _) => await RefreshAsync();

        // Si el usuario cambia el intervalo en Configuracion, se aplica al instante.
        AppSettingsService.Instance.Changed += (_, _) => ApplyInterval();

        // La vista se cachea en MainWindow: al cambiar de modulo se descarga (Unloaded) y al
        // volver se carga otra vez (Loaded), asi que el temporizador debe arrancar en Loaded
        // y no solo en el constructor, o la tabla se quedaria congelada al volver.
        Loaded += async (_, _) =>
        {
            ApplyInterval();
            await RefreshAsync(); // primera muestra: fija la referencia de CPU/GPU

            // Si la vista se descargo mientras tanto, no arrancamos el temporizador.
            if (IsLoaded) _timer.Start();
        };

        Unloaded += (_, _) =>
        {
            _timer.Stop();
            _service.Dispose(); // libera los contadores de GPU; se recrean solos en el siguiente Refresh
        };
    }

    private static TimeSpan CurrentInterval() =>
        TimeSpan.FromSeconds(AppSettingsService.Instance.GetRefreshSeconds(AutoRefreshSeconds));

    private void ApplyInterval()
    {
        var interval = CurrentInterval();
        if (_timer.Interval != interval) _timer.Interval = interval;
    }

    /// <summary>True mientras hay un muestreo en marcha: si el siguiente tick llega antes de
    /// que termine, se salta en vez de apilar trabajo (y el servicio no se toca desde dos
    /// hilos a la vez).</summary>
    private bool _refreshing;

    /// <summary>El muestreo (procesos, contadores de GPU, iconos) se hace en un hilo de fondo;
    /// la interfaz solo recibe el resultado ya calculado y reconstruye las filas.</summary>
    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;

        try
        {
            _groups = await Task.Run(() =>
            {
                _service.Refresh();
                return _service.BuildGroups();
            });

            RebuildRows();
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo el refresco de procesos", ex);
        }
        finally
        {
            _refreshing = false;
        }
    }

    // ===================== Cabeceras con totales =====================

    /// <summary>Cada cabecera lleva el nombre arriba y el total del sistema debajo (se
    /// actualiza en cada refresco). Las columnas numericas se alinean a la derecha, como sus
    /// celdas. El orden por columna sigue funcionando: solo cambia el contenido visual de la
    /// cabecera, no su SortMemberPath.</summary>
    private void BuildHeaders()
    {
        string[] titles = { "Proceso", "CPU", "RAM", "GPU", "VRAM" };
        var mono = (System.Windows.Media.FontFamily)FindResource("Font.Mono");

        for (var i = 0; i < titles.Length; i++)
        {
            var alignment = i == 0
                ? System.Windows.HorizontalAlignment.Left
                : System.Windows.HorizontalAlignment.Right;

            var title = new System.Windows.Controls.TextBlock
            {
                Text = titles[i],
                HorizontalAlignment = alignment
            };

            var total = new System.Windows.Controls.TextBlock
            {
                FontFamily = mono,
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = alignment
            };
            total.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "Brush.Accent");

            _headerTotals[i] = total;

            ProcessesGrid.Columns[i].Header = new StackPanel
            {
                HorizontalAlignment = alignment,
                Children = { title, total }
            };
        }
    }

    private void UpdateHeaderTotals()
    {
        var processCount = _groups.Sum(g => g.MemberCount);
        var cpu = Math.Min(100, _groups.Sum(g => g.TotalCpu));
        var ramMb = _groups.Sum(g => g.TotalRamMB);
        var ramPercent = Math.Min(100, _groups.Sum(g => g.TotalRamPercent)); // el working set compartido puede sumar de mas
        var gpu = Math.Min(100, _groups.Sum(g => g.TotalGpu));
        var vramMb = _groups.Sum(g => g.TotalVramMB);
        var vramPercent = Math.Min(100, _groups.Sum(g => g.TotalVramPercent));

        _headerTotals[0].Text = $"{processCount} procesos";
        _headerTotals[1].Text = $"{cpu:0}%";
        _headerTotals[2].Text = $"{FormatMemory(ramMb)} \u00b7 {ramPercent:0}%";
        _headerTotals[3].Text = $"{gpu:0}%";
        _headerTotals[4].Text = $"{FormatMemory(vramMb)} \u00b7 {vramPercent:0}%";
    }

    private static string FormatMemory(double megabytes) =>
        megabytes >= 1024 ? $"{megabytes / 1024:0.#} GB" : $"{megabytes:0} MB";

    // ===================== Construccion de filas (grupo + hijos expandidos) =====================

    private void RebuildRows()
    {
        var visibleGroups = _groups.Where(GroupMatchesSearch).ToList();
        visibleGroups = SortGroups(visibleGroups);

        var rows = new List<ProcessRow>();
        foreach (var group in visibleGroups)
        {
            // Con busqueda activa se auto-expande cualquier grupo visible para que el
            // usuario vea que hijo hizo match; sin busqueda, respeta lo que haya expandido a mano.
            var expanded = group.HasChildren &&
                           (!string.IsNullOrWhiteSpace(_search) || _expandedGroups.Contains(group.Root.Name));

            rows.Add(ToGroupRow(group, expanded));

            if (expanded)
            {
                foreach (var child in group.Children.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    rows.Add(ToChildRow(child));
                }
            }
        }

        ProcessesGrid.ItemsSource = rows;
        EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var totalProcesses = _groups.Sum(g => g.MemberCount);
        CountText.Text = $"{totalProcesses} procesos \u00b7 {_groups.Count} agrupados";

        UpdateHeaderTotals();
    }

    private bool GroupMatchesSearch(ProcessGroup group)
    {
        if (string.IsNullOrWhiteSpace(_search)) return true;
        return group.Root.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
               group.Children.Any(c => c.Name.Contains(_search, StringComparison.OrdinalIgnoreCase));
    }

    private List<ProcessGroup> SortGroups(List<ProcessGroup> groups)
    {
        IOrderedEnumerable<ProcessGroup> sorted = _sortColumn switch
        {
            SortColumn.Cpu => groups.OrderBy(g => g.TotalCpu),
            SortColumn.Ram => groups.OrderBy(g => g.TotalRamMB),
            SortColumn.Gpu => groups.OrderBy(g => g.TotalGpu),
            SortColumn.Vram => groups.OrderBy(g => g.TotalVramMB),
            SortColumn.Status => groups.OrderBy(g => g.AnyFrozen),
            _ => groups.OrderBy(g => g.Root.Name, StringComparer.CurrentCultureIgnoreCase)
        };

        var list = sorted.ToList();
        if (!_sortAscending) list.Reverse();
        return list;
    }

    private static ProcessRow ToGroupRow(ProcessGroup group, bool expanded) => new()
    {
        Name = group.Root.Name,
        Icon = group.Root.Icon,
        Pid = group.Root.Pid,
        CpuPercent = group.TotalCpu,
        RamMB = group.TotalRamMB,
        RamPercent = group.TotalRamPercent,
        GpuPercent = group.TotalGpu,
        VramMB = group.TotalVramMB,
        VramPercent = group.TotalVramPercent,
        IsFrozen = group.AnyFrozen,
        Depth = 0,
        IsGroup = true,
        HasChildren = group.HasChildren,
        IsExpanded = expanded,
        MemberCount = group.MemberCount,
        Tag = group
    };

    private static ProcessRow ToChildRow(ProcessSample sample) => new()
    {
        Name = sample.Name,
        Icon = sample.Icon,
        Pid = sample.Pid,
        CpuPercent = sample.CpuPercent,
        RamMB = sample.RamMB,
        RamPercent = sample.RamPercent,
        GpuPercent = sample.GpuPercent,
        VramMB = sample.VramMB,
        VramPercent = sample.VramPercent,
        IsFrozen = sample.IsFrozen,
        Depth = 1,
        IsGroup = false,
        HasChildren = false,
        IsExpanded = false,
        MemberCount = 1,
        Tag = sample
    };

    // ===================== Busqueda / orden / expandir =====================

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text;
        RebuildRows();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void ProcessesGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        var column = SortColumnFromMemberPath(e.Column.SortMemberPath);
        if (column == null) return;

        if (_sortColumn == column.Value) _sortAscending = !_sortAscending;
        else { _sortColumn = column.Value; _sortAscending = true; }

        foreach (var col in ProcessesGrid.Columns) col.SortDirection = null;
        e.Column.SortDirection = _sortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;

        RebuildRows();
    }

    private static SortColumn? SortColumnFromMemberPath(string? path) => path switch
    {
        "Name" => SortColumn.Name,
        "CpuPercent" => SortColumn.Cpu,
        "RamMB" => SortColumn.Ram,
        "GpuPercent" => SortColumn.Gpu,
        "VramMB" => SortColumn.Vram,
        "IsFrozen" => SortColumn.Status,
        _ => null
    };

    private void NameCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ProcessRow { HasChildren: true } row) return;

        var key = row.Name;
        if (!_expandedGroups.Add(key)) _expandedGroups.Remove(key); // toggle

        RebuildRows();
    }

    // ===================== Finalizar tarea =====================

    private void EndTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ProcessRow row) EndTaskForRow(row);
    }

    private void EndTaskMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement fe } } &&
            fe.DataContext is ProcessRow row)
        {
            EndTaskForRow(row);
        }
    }

    private void EndTaskForRow(ProcessRow row)
    {
        var targets = row.Tag switch
        {
            ProcessGroup group => group.AllMembers.ToList(),
            ProcessSample sample => new List<ProcessSample> { sample },
            _ => new List<ProcessSample>()
        };

        if (targets.Count == 0) return;

        // El ajuste "Confirmar al finalizar procesos" decide si se pregunta antes de cerrar.
        if (AppSettingsService.Instance.ConfirmKillProcess)
        {
            var description = targets.Count > 1
                ? $"{row.Name} y {targets.Count - 1} proceso(s) relacionado(s)"
                : row.Name;

            var result = MessageBox.Show(
                Window.GetWindow(this),
                $"Se finalizara: {description}\n\nLos cambios no guardados en esos procesos se perderan. Continuar?",
                "Finalizar tarea",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;
        }

        var failures = new List<string>();
        foreach (var target in targets)
        {
            if (!_service.TryKill(target, out var error))
            {
                failures.Add($"{target.Name} ({error})");
            }
        }

        if (failures.Count > 0)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                "No se pudieron finalizar algunos procesos:\n" + string.Join("\n", failures),
                "Aviso",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        _ = RefreshAsync();
    }
}
