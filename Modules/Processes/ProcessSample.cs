using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Launcher.Modules.Processes;

/// <summary>
/// Snapshot de un proceso en un instante dado. La misma instancia se reutiliza
/// entre refrescos (ProcessMonitorService actualiza sus propiedades en sitio en
/// vez de recrearla) para que el icono cacheado sobreviva a cada tick.
/// </summary>
public class ProcessSample : INotifyPropertyChanged
{
    public int Pid { get; }

    /// <summary>PID del proceso padre segun el snapshot de Toolhelp32. 0 si no se
    /// pudo determinar. Se usa solo para agrupar procesos del mismo tipo (ej. Discord).</summary>
    public int ParentPid { get; set; }

    public string? ExecutablePath { get; set; }

    public ProcessSample(int pid, string name)
    {
        Pid = pid;
        _name = name;
    }

    private string _name;
    public string Name { get => _name; set => SetField(ref _name, value); }

    private ImageSource? _icon;
    public ImageSource? Icon { get => _icon; set => SetField(ref _icon, value); }

    private double _cpuPercent;
    public double CpuPercent { get => _cpuPercent; set => SetField(ref _cpuPercent, value); }

    private double _ramMb;
    public double RamMB { get => _ramMb; set => SetField(ref _ramMb, value); }

    private double _ramPercent;
    public double RamPercent { get => _ramPercent; set => SetField(ref _ramPercent, value); }

    private double _gpuPercent;
    public double GpuPercent { get => _gpuPercent; set => SetField(ref _gpuPercent, value); }

    private double _vramMb;
    public double VramMB { get => _vramMb; set => SetField(ref _vramMb, value); }

    private double _vramPercent;
    public double VramPercent { get => _vramPercent; set => SetField(ref _vramPercent, value); }

    private bool _isResponding = true;
    public bool IsResponding { get => _isResponding; set => SetField(ref _isResponding, value); }

    /// <summary>Congelado = no responde (solo fiable en procesos con ventana; los
    /// procesos sin UI siempre devuelven Responding = true).</summary>
    public bool IsFrozen => !IsResponding;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}