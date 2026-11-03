using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Models;

public class ModelsModule : IModule
{
    public string Id => "models";
    public string DisplayName => "Modelos";
    public string Icon => "\uE8F1"; // Library
    public int Order => 8;

    public UserControl CreateView() => new ModelsView();
}
