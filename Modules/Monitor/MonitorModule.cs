using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Monitor;

public class MonitorModule : IModule
{
    public string Id => "monitor";
    public string DisplayName => "Monitor";
    public string Icon => "\uE9D9";
    public int Order => 6;

    public UserControl CreateView() => new MonitorView();
}