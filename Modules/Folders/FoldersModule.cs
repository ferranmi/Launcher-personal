using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Folders;

public class FoldersModule : IModule
{
    public string Id => "folders";
    public string DisplayName => "Carpetas";
    public string Icon => "\uE8B7";
    public int Order => 1;

    public UserControl CreateView() => new FoldersView();
}