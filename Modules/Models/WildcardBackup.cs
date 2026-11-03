using System.Globalization;
using System.IO;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

public record BackupInfo(string Path, DateTime Created, long Size);

/// <summary>Copias de seguridad de wildcards en %AppData%\Launcher\models-backups\&lt;nombre&gt;\. Siempre se
/// crea una antes de sobrescribir un archivo. Se conservan las 30 mas recientes de cada uno.</summary>
public static class WildcardBackup
{
    private const int KeepPerFile = 30;
    private const string StampFormat = "yyyyMMdd-HHmmss-fff";

    private static string FolderFor(string wildcardName)
    {
        var safe = wildcardName.Replace('/', '~');
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
        return System.IO.Path.Combine(AppSettingsService.AppFolder, "models-backups", safe);
    }

    /// <summary>Copia el archivo tal cual esta ahora. Lanza si no puede: quien llame no debe guardar sin copia.</summary>
    public static string Create(string sourcePath, string wildcardName)
    {
        var folder = FolderFor(wildcardName);
        Directory.CreateDirectory(folder);

        var target = System.IO.Path.Combine(folder, $"{DateTime.Now.ToString(StampFormat, CultureInfo.InvariantCulture)}.txt");
        File.Copy(sourcePath, target, overwrite: false);

        Prune(folder);
        return target;
    }

    public static List<BackupInfo> List(string wildcardName)
    {
        var folder = FolderFor(wildcardName);
        if (!Directory.Exists(folder)) return new List<BackupInfo>();

        return Directory.EnumerateFiles(folder, "*.txt")
            .Select(path =>
            {
                var stamp = System.IO.Path.GetFileNameWithoutExtension(path);
                var created = DateTime.TryParseExact(stamp, StampFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed)
                    ? parsed
                    : File.GetCreationTime(path);
                return new BackupInfo(path, created, new FileInfo(path).Length);
            })
            .OrderByDescending(b => b.Created)
            .ToList();
    }

    private static void Prune(string folder)
    {
        try
        {
            foreach (var old in Directory.EnumerateFiles(folder, "*.txt")
                         .OrderByDescending(f => f, StringComparer.Ordinal)
                         .Skip(KeepPerFile))
            {
                File.Delete(old);
            }
        }
        catch
        {
            // Limpieza best-effort.
        }
    }
}
