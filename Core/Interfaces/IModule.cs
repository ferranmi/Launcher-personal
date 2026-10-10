using System.Windows.Controls;

namespace Launcher.Core.Interfaces;

/// <summary>
/// Contrato que debe implementar cada modulo de la plataforma (Juegos, Media, Monitor, etc).
/// Permite que MainWindow no conozca los detalles internos de ningun modulo:
/// solo pide su nombre, icono y la vista a mostrar.
/// </summary>
public interface IModule
{
    /// <summary>Identificador unico y estable del modulo (usado internamente, no visible).</summary>
    string Id { get; }

    /// <summary>Nombre mostrado en el sidebar.</summary>
    string DisplayName { get; }

    /// <summary>Glifo de Segoe Fluent Icons mostrado en el sidebar.</summary>
    string Icon { get; }

    /// <summary>Orden de aparicion en el sidebar (menor = mas arriba).</summary>
    int Order { get; }

    /// <summary>
    /// Devuelve la vista (UserControl) del modulo. Se llama la primera vez que el
    /// usuario navega al modulo; el resultado se puede cachear en MainWindow.
    /// </summary>
    UserControl CreateView();
}
