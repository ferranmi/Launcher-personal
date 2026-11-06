using System.Windows;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

/// <summary>Flujo completo de renombrado, compartido por la Biblioteca y por Referencias:
/// simulacion y confirmacion -> renombrado (con deshacer si falla) -> correccion pendiente opcional.
/// Los wildcards no se tocan aqui.</summary>
public static class ModelRenameFlow
{
    /// <summary>Devuelve la nueva ruta del modelo, o null si se cancelo o fallo.</summary>
    public static async Task<string?> RunAsync(Window? owner, ModelEntry entry)
    {
        var catalog = ModelCatalogService.Instance;

        Dictionary<string, List<WildcardRefLocation>> index;
        try
        {
            index = await WildcardService.Instance.GetReferenceIndexAsync();
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer el indice de referencias antes de renombrar", ex);
            index = new Dictionary<string, List<WildcardRefLocation>>(StringComparer.OrdinalIgnoreCase);
        }

        index.TryGetValue(entry.Name, out var refs);

        var dialog = new RenameModelWindow(entry, refs ?? new List<WildcardRefLocation>(), catalog.Models) { Owner = owner };
        if (dialog.ShowDialog() != true || dialog.Plan == null) return null;

        var plan = dialog.Plan;

        try
        {
            await Task.Run(() => ModelRenamer.Execute(plan));
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo renombrar {entry.Name}", ex);
            MessageBox.Show(owner,
                $"No se ha podido renombrar el modelo:\n{ex.Message}\n\n" +
                "Si algun archivo se habia renombrado ya, se ha deshecho. Comprueba que ningun programa " +
                "(p.ej. A1111) este usando el archivo.",
                "Renombrar modelo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        LogService.Info($"Modelo renombrado: {entry.FullPath} -> {plan.MainTarget} ({plan.Moves.Count} archivo(s))");
        ActivityLog.Add("renombrado", $"{entry.Name} -> {dialog.NewName}", 1);

        if (dialog.PrepareCorrections && refs is { Count: > 0 })
        {
            ModelsSettings.Instance.AddPending(entry.Name, dialog.NewName, "rename");
        }

        return plan.MainTarget;
    }
}
