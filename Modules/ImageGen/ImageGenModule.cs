using System.Windows.Controls;
using Launcher.Core.Interfaces;

namespace Launcher.Modules.ImageGen;

public class ImageGenModule : IModule
{
    public string Id => "imagegen";
    public string DisplayName => "Imagenes IA";
    public string Icon => "\uEB9F";
    public int Order => 7; // ajusta segun donde lo quieras en el sidebar

    public UserControl CreateView() => new ImageGenView();
}
