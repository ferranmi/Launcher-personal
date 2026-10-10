using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Settings;

public class SettingsModule : IModule
{
    public string Id => "settings";
    public string DisplayName => "Configuracion";
    public string Icon => "\uE713";
    public int Order => 100; // al final del sidebar

    public UserControl CreateView() => new SettingsView();
}