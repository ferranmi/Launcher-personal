using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Processes;

public class ProcessesModule : IModule
{
    public string Id => "processes";
    public string DisplayName => "Procesos";
    public string Icon => "\uE9D9";
    public int Order => 4; // ajusta segun donde quieras que aparezca en el sidebar

    public UserControl CreateView() => new ProcessesView();
}