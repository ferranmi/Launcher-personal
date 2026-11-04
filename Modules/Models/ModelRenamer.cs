using System.IO;

namespace Launcher.Modules.Models;

public record RenameMove(string From, string To, bool IsMain);

public class RenamePlan
{
    public ModelEntry Entry { get; init; } = null!;
    public string NewName { get; init; } = "";
    public List<RenameMove> Moves { get; } = new();

    /// <summary>Motivo por el que el plan no se puede ejecutar (p.ej. el destino ya existe).</summary>
    public string? Error { get; set; }

    public string MainTarget => Moves[0].To;
}

/// <summary>Renombra un modelo y sus archivos acompanantes (preview, info...) en su misma carpeta. Nunca
/// sobrescribe nada: si algun destino ya existe, el plan trae un error. Si algo falla a medias se deshace
/// lo ya renombrado.</summary>
public static class ModelRenamer
{
    // Lista cerrada de acompanantes: coincidencia exacta "nombre + sufijo". Asi "Tsukasa" nunca arrastra
    // a otro modelo llamado "Tsukasa.v2".
    private static readonly string[] SidecarSuffixes =
    {
        ".preview.png", ".preview.jpg", ".preview.jpeg", ".preview.webp",
        ".png", ".jpg", ".jpeg", ".webp",
        ".civitai.info", ".info", ".json", ".txt", ".yaml", ".yml"
    };

    public static RenamePlan BuildPlan(ModelEntry entry, string newName)
    {
        var plan = new RenamePlan { Entry = entry, NewName = newName };

        var folder = Path.GetDirectoryName(entry.FullPath) ?? "";
        var extension = Path.GetExtension(entry.FullPath);

        plan.Moves.Add(new RenameMove(entry.FullPath, Path.Combine(folder, newName + extension), true));

        foreach (var suffix in SidecarSuffixes)
        {
            var from = Path.Combine(folder, entry.Name + suffix);
            if (File.Exists(from))
            {
                plan.Moves.Add(new RenameMove(from, Path.Combine(folder, newName + suffix), false));
            }
        }

        foreach (var move in plan.Moves)
        {
            // Cambiar solo mayusculas/minusculas del mismo archivo es valido en Windows.
            if (string.Equals(move.From, move.To, StringComparison.OrdinalIgnoreCase)) continue;

            if (File.Exists(move.To) || Directory.Exists(move.To))
            {
                plan.Error = $"Ya existe un archivo llamado \"{Path.GetFileName(move.To)}\" en esa carpeta.";
                break;
            }
        }

        return plan;
    }

    public static void Execute(RenamePlan plan)
    {
        if (plan.Error != null) throw new InvalidOperationException(plan.Error);

        var done = new List<RenameMove>();

        try
        {
            foreach (var move in plan.Moves)
            {
                File.Move(move.From, move.To); // sin overwrite: lanza si el destino aparece de repente
                done.Add(move);
            }
        }
        catch
        {
            for (var i = done.Count - 1; i >= 0; i--)
            {
                try { File.Move(done[i].To, done[i].From); }
                catch { /* intento de deshacer: no se puede hacer mas */ }
            }

            throw;
        }
    }
}
