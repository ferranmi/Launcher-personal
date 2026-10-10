using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Launcher.Core.Models;

/// <summary>
/// Entidad de un acceso directo del modulo Comandos: puede ser una aplicacion,
/// un gestor de periferico, una herramienta de sistema o un comando de terminal
/// (p.ej. Kunai). Se serializa a commands.json.
/// </summary>
public class CommandShortcut : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _name = string.Empty;
    public string Name { get => _name; set { _name = value; OnChanged(); } }

    private string _description = string.Empty;
    public string Description { get => _description; set { _description = value; OnChanged(); } }

    private CommandCategory _category = CommandCategory.App;
    public CommandCategory Category
    {
        get => _category;
        set { _category = value; OnChanged(); OnChanged(nameof(CategoryLabel)); }
    }

    /// <summary>Ruta del ejecutable, o un comando resoluble por PATH (p.ej. "kunai").</summary>
    private string _target = string.Empty;
    public string Target { get => _target; set { _target = value; OnChanged(); } }

    private string _arguments = string.Empty;
    public string Arguments { get => _arguments; set { _arguments = value; OnChanged(); } }

    private string? _workingDirectory;
    public string? WorkingDirectory { get => _workingDirectory; set { _workingDirectory = value; OnChanged(); } }

    /// <summary>Que consola aloja el comando: Ninguna (lanzamiento directo), CMD o PowerShell.
    /// Kunai, por ejemplo, necesita PowerShell.</summary>
    private TerminalHost _terminal = TerminalHost.None;
    public TerminalHost Terminal
    {
        get => _terminal;
        set { _terminal = value; OnChanged(); OnChanged(nameof(TerminalTag)); }
    }

    /// <summary>Si es true, se lanza elevado (UAC) via el verbo "runas" de Windows.</summary>
    private bool _runAsAdmin;
    public bool RunAsAdmin { get => _runAsAdmin; set { _runAsAdmin = value; OnChanged(); } }

    private bool _favorite;
    public bool Favorite { get => _favorite; set { _favorite = value; OnChanged(); } }

    /// <summary>Glifo de Segoe Fluent Icons. Libre para que el usuario pegue el que quiera;
    /// si se deja vacio se usa un icono generico de terminal.</summary>
    private string _icon = "\uE756";
    public string Icon
    {
        get => _icon;
        set { _icon = string.IsNullOrWhiteSpace(value) ? "\uE756" : value; OnChanged(); }
    }

    public DateTime? LastUsed { get; set; }

    public string CategoryLabel => Category switch
    {
        CommandCategory.Peripheral => "Periferico",
        CommandCategory.System => "Sistema",
        CommandCategory.Cli => "CLI",
        _ => "Aplicacion"
    };

    /// <summary>Etiqueta corta para la tarjeta ("CMD"/"PS"), o null si se lanza directo.</summary>
    public string? TerminalTag => Terminal switch
    {
        TerminalHost.Cmd => "CMD",
        TerminalHost.PowerShell => "PS",
        _ => null
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}