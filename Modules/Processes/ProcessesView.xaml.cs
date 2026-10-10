using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Launcher.Modules.Processes;

public partial class ProcessesView : UserControl
{
    private enum SortColumn { Name, Cpu, Ram, Gpu, Vram, Status }

    private readonly ProcessMonitorService _service = new();
    private readonly DispatcherTimer _timer;

    private List<ProcessGroup> _groups = new();

    /// <summary>Nombres (en minuscula) de los grupos que el usuario ha expandido a
    /// mano. Persiste entre refrescos aunque las instancias de ProcessGroup se
    /// reconstruyan en cada tick.</summary>
    private readonly HashSet<string> _expandedGroups = new(StringComparer.OrdinalIgnoreCase);

    private SortColumn _sortColumn = SortColumn.Name;
    private bool _sortAscending = true;
    private string _search = "";

    public ProcessesView()
    {
        InitializeComponent();

        _service.Refresh();
        _groups = _service.BuildGroups();
        RebuildRows();

        // 1.5s: suficiente para que la delta de CPU sea estable sin saturar la
        // relectura de los contadores de GPU en cada tick.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _timer.Tick += (_, _) => RefreshTick();
        _timer.Start();

        Unloaded += (_, _) =>
        {
            _timer.Stop();
            _service.Dispose();
        };
    }

    private void RefreshTick()
    {
        _service.Refresh();
        _groups = _service.BuildGroups();
        RebuildRows();
    }

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

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshTick();

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

        RefreshTick();
    }
}