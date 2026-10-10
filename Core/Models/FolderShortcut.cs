using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Launcher.Core.Models;

/// <summary>
/// Acceso directo a una carpeta del sistema (Documentos, Descargas...) o anadida
/// a mano por el usuario. Se serializa tal cual a folders.json.
/// </summary>
public class FolderShortcut : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _name = string.Empty;
    public string Name { get => _name; set { _name = value; OnChanged(); } }

    private string _path = string.Empty;
    public string Path { get => _path; set { _path = value; OnChanged(); OnChanged(nameof(Exists)); } }

    /// <summary>Icono (glifo Segoe Fluent Icons) para la tarjeta. Distinto segun el
    /// tipo de carpeta conocido (Documentos, Descargas...) o generico si es una
    /// carpeta anadida a mano.</summary>
    public string Icon { get; set; } = "\uE8B7";

    /// <summary>True para las carpetas del sistema sembradas la primera vez
    /// (Escritorio, Documentos, Descargas...). No se pueden eliminar, solo ocultar,
    /// para que el usuario no se quede sin ellas por error.</summary>
    public bool IsBuiltIn { get; set; }

    private bool _hidden;
    public bool Hidden { get => _hidden; set { _hidden = value; OnChanged(); } }

    /// <summary>Comprobado en caliente contra el disco (no se persiste): si el
    /// usuario borro o movio la carpeta, la tarjeta lo indica en vez de fallar
    /// silenciosamente al abrirla.</summary>
    public bool Exists => !string.IsNullOrWhiteSpace(Path) && Directory.Exists(Path);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}