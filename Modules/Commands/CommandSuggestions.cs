using Launcher.Core.Models;

namespace Launcher.Modules.Commands;

/// <summary>Catalogo de accesos directos de sistema comunes en Windows, ofrecidos
/// como sugerencias rapidas en CommandSuggestionsWindow para no rellenar el
/// formulario a mano cada vez.</summary>
public static class CommandSuggestions
{
    public static List<CommandShortcut> GetAll() => new()
    {
        new CommandShortcut
        {
            Name = "Administrador de dispositivos",
            Description = "Gestiona el hardware y sus controladores",
            Category = CommandCategory.System,
            Target = "devmgmt.msc"
        },
        new CommandShortcut
        {
            Name = "Administrador de tareas",
            Description = "Procesos, rendimiento e inicio de sesion",
            Category = CommandCategory.System,
            Target = "taskmgr.exe"
        },
        new CommandShortcut
        {
            Name = "Programador de tareas",
            Description = "Automatiza tareas programadas de Windows",
            Category = CommandCategory.System,
            Target = "taskschd.msc"
        },
        new CommandShortcut
        {
            Name = "Panel de control",
            Description = "Configuracion clasica de Windows",
            Category = CommandCategory.System,
            Target = "control.exe"
        },
        new CommandShortcut
        {
            Name = "Configuracion de Windows",
            Description = "Panel de ajustes moderno",
            Category = CommandCategory.System,
            Target = "ms-settings:"
        },
        new CommandShortcut
        {
            Name = "Administrador de discos",
            Description = "Particiones y volumenes del sistema",
            Category = CommandCategory.System,
            Target = "diskmgmt.msc",
            RunAsAdmin = true
        },
        new CommandShortcut
        {
            Name = "Editor del registro",
            Description = "Edicion avanzada del registro de Windows",
            Category = CommandCategory.System,
            Target = "regedit.exe",
            RunAsAdmin = true
        },
        new CommandShortcut
        {
            Name = "PowerShell",
            Description = "Abre una terminal de PowerShell",
            Category = CommandCategory.Cli,
            Target = "powershell.exe"
        },
        new CommandShortcut
        {
            Name = "Simbolo del sistema",
            Description = "Abre una terminal CMD clasica",
            Category = CommandCategory.Cli,
            Target = "cmd.exe"
        }
    };
}