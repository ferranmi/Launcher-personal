using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Launcher.Core.Models;

/// <summary>
/// Acceso directo a una aplicacion multimedia (reproductor, servicio de
/// streaming...). Se serializa tal cual a media_apps.json. Misma forma que Game,
/// pero sin los campos especificos de biblioteca de juegos (favoritos, origen,
/// tiempo jugado...) porque no aplican aqui.
/// </summary>
public class MediaAppShortcut : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _name = string.Empty;
    public string Name { get => _name; set { _name = value; OnChanged(); } }

    private string _executablePath = string.Empty;
    public string ExecutablePath { get => _executablePath; set { _executablePath = value; OnChanged(); } }

    private string _arguments = string.Empty;
    public string Arguments { get => _arguments; set { _arguments = value; OnChanged(); } }

    private string? _iconImagePath;
    public string? IconImagePath
    {
        get => _iconImagePath;
        set { _iconImagePath = value; OnChanged(); OnChanged(nameof(HasIcon)); }
    }

    public bool HasIcon => !string.IsNullOrWhiteSpace(IconImagePath) && File.Exists(IconImagePath);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
