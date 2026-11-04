namespace Launcher.Modules.Models;

public record FilePlan(string WildcardName, string FullPath, WildcardDocument Doc,
    List<string> NewLines, List<ChangeLine> Changes);

public class ApplyResult
{
    public int FilesSaved { get; set; }
    public int LinesChanged { get; set; }
    public List<string> Failed { get; } = new();
}

/// <summary>Aplica correcciones de nombre de referencia sobre los wildcards. Construir el plan solo lee;
/// aplicarlo crea una copia por archivo y salta los que hayan cambiado en disco desde que se leyeron.</summary>
public static class ReferenceFixService
{
    public static async Task<List<FilePlan>> BuildPlanAsync(IReadOnlyList<PendingCorrection> corrections)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var correction in corrections) map[correction.OldName] = correction.NewName;

        var index = await WildcardService.Instance.GetReferenceIndexAsync(force: true);

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // ruta -> nombre del wildcard
        foreach (var oldName in map.Keys)
        {
            if (!index.TryGetValue(oldName, out var locations)) continue;
            foreach (var location in locations) files[location.FullPath] = location.FileName;
        }

        return await Task.Run(() =>
        {
            var plans = new List<FilePlan>();

            foreach (var (path, name) in files)
            {
                WildcardDocument doc;
                try { doc = WildcardDocument.Load(path); }
                catch { continue; }

                var newLines = new List<string>(doc.Lines.Count);
                var changes = new List<ChangeLine>();

                for (var i = 0; i < doc.Lines.Count; i++)
                {
                    var line = doc.Lines[i];
                    var rewritten = ReferenceRewriter.Rewrite(line, map);
                    newLines.Add(rewritten);

                    if (!string.Equals(line, rewritten, StringComparison.Ordinal))
                    {
                        changes.Add(new ChangeLine("-", $"L{i + 1}   {line}"));
                        changes.Add(new ChangeLine("+", $"L{i + 1}   {rewritten}"));
                    }
                }

                if (changes.Count > 0) plans.Add(new FilePlan(name, path, doc, newLines, changes));
            }

            return plans.OrderBy(p => p.WildcardName, StringComparer.CurrentCultureIgnoreCase).ToList();
        });
    }

    public static Task<ApplyResult> ApplyAsync(IReadOnlyList<FilePlan> plans) => Task.Run(() =>
    {
        var result = new ApplyResult();

        foreach (var plan in plans)
        {
            try
            {
                if (plan.Doc.ChangedOnDisk())
                {
                    result.Failed.Add($"{plan.WildcardName}: ha cambiado en disco desde que se reviso (no se ha tocado)");
                    continue;
                }

                WildcardBackup.Create(plan.FullPath, plan.WildcardName); // si falla, no se escribe
                plan.Doc.Save(plan.NewLines);

                result.FilesSaved++;
                result.LinesChanged += plan.Changes.Count(c => c.Kind == "+");
            }
            catch (Exception ex)
            {
                result.Failed.Add($"{plan.WildcardName}: {ex.Message}");
            }
        }

        return result;
    });
}
