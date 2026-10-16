using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Dashboard;

public class DashboardModule : IModule
{
    public string Id => "dashboard";
    public string DisplayName => "Inicio";
    public string Icon => "\uE80F"; // Home
    public int Order => -1;          // el primero del sidebar

    public UserControl CreateView() => new DashboardView();
}
