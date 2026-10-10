using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Games;

public class GamesModule : IModule
{
    public string Id => "games";
    public string DisplayName => "Juegos";
    public string Icon => "\uE7FC";
    public int Order => 0;

    public UserControl CreateView() => new GamesView();
}
