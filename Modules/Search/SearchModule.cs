using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.Search;

public class SearchModule : IModule
{
    public string Id => "search";
    public string DisplayName => "Buscar";
    public string Icon => "\uE721";
    public int Order => 4;

    public UserControl CreateView() => new SearchView();
}
