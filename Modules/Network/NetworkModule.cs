using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Network;

public class NetworkModule : IModule
{
    public string Id => "network";
    public string DisplayName => "Red";
    public string Icon => "\uE968";
    public int Order => 7;

    public UserControl CreateView() => new NetworkView();
}
