using Microsoft.Win32;

namespace Launcher.Core.Services;

/// <summary>
/// Registra/quita la app del arranque de Windows usando la clave Run del usuario
/// actual (no requiere permisos de administrador).
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Launcher";

    /// <summary>Argumento que MainWindow comprueba para arrancar minimizada.</summary>
    public const string MinimizedArg = "--minimized";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Activa o desactiva el arranque con Windows. Devuelve false si fallo.</summary>
    public static bool SetEnabled(bool enabled, bool startMinimized)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key == null) return false;

            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exe)) return false;

                var command = $"\"{exe}\"";
                if (startMinimized) command += " " + MinimizedArg;
                key.SetValue(ValueName, command);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}