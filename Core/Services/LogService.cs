using System.IO;

namespace Launcher.Core.Services;

/// <summary>
/// Registro de errores en %AppData%\Launcher\logs\launcher-AAAAMMDD.log.
/// Nunca lanza excepciones: si no puede escribir, no hace nada.
/// </summary>
public static class LogService
{
    public static readonly string LogsFolder = Path.Combine(AppSettingsService.AppFolder, "logs");

    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string context, Exception ex) => Write("ERROR", $"{context}: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogsFolder);
                var file = Path.Combine(LogsFolder, $"launcher-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Si el log falla, no podemos hacer nada util.
        }
    }

    /// <summary>Borra los registros con mas de keepDays dias.</summary>
    public static void CleanOld(int keepDays = 7)
    {
        try
        {
            if (!Directory.Exists(LogsFolder)) return;

            var limit = DateTime.Now.AddDays(-keepDays);
            foreach (var file in Directory.GetFiles(LogsFolder, "launcher-*.log"))
            {
                if (File.GetLastWriteTime(file) < limit) File.Delete(file);
            }
        }
        catch
        {
            // Limpieza best-effort.
        }
    }
}