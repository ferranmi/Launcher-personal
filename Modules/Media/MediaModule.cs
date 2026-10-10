using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Media;

public class MediaModule : IModule
{
    public string Id => "media";
    public string DisplayName => "Multimedia";
    public string Icon => "\uE8D6"; // MusicInfo
    public int Order => 2;

    public UserControl CreateView() => new MediaView();
}