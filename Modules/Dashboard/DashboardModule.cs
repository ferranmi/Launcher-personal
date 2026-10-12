using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Dashboard;

/// <summary>Pestana principal: resumen del sistema, lo que suena, accesos rapidos y los
/// procesos que mas consumen. Order = -1 para que aparezca la primera en el sidebar.</summary>
public class DashboardModule : IModule
{
    public string Id => "dashboard";
    public string DisplayName => "Inicio";
    public string Icon => "\uE80F"; // Home
    public int Order => -1;

    public UserControl CreateView() => new DashboardView();
}
