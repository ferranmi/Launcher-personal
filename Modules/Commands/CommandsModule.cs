using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Commands;

public class CommandsModule : IModule
{
    public string Id => "commands";
    public string DisplayName => "Comandos";
    public string Icon => "\uE756";

    // AJUSTA este valor segun donde quieras que aparezca en el sidebar
    // respecto a los demas modulos (Juegos=0, etc.). No tengo visibilidad
    // de los Order actuales de Media/Monitor/Procesos.
    public int Order => 10;

    public UserControl CreateView() => new CommandsView();
}