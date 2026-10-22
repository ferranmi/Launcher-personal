using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Music;

public class MusicModule : IModule
{
    public string Id => "music";
    public string DisplayName => "Musica";
    public string Icon => "\uEC4F";
    public int Order => 3;

    public UserControl CreateView() => new MusicView();
}
