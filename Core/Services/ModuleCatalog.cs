using Launcher.Core.Interfaces;

namespace Launcher.Core.Services;

/// <summary>
/// Lista de modulos registrados, accesible desde cualquier parte (p.ej. Configuracion,
/// para ofrecer "Modulo inicial"). MainWindow la rellena una vez al arrancar.
/// </summary>
public static class ModuleCatalog
{
    public static IReadOnlyList<IModule> Modules { get; private set; } = Array.Empty<IModule>();

    public static void Register(IEnumerable<IModule> modules) =>
        Modules = modules.OrderBy(m => m.Order).ToList();
}