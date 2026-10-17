using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Devices;

public class DevicesModule : IModule
{
    public string Id => "devices";
    public string DisplayName => "Dispositivos";
    public string Icon => "\uE772";
    public int Order => 7;

    public UserControl CreateView() => new DevicesView();
}
