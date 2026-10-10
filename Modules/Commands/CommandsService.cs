using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Launcher.Core.Models;

namespace Launcher.Modules.Commands;

/// <summary>
/// Fuente de verdad de los accesos directos de Comandos. Persiste en JSON en
/// %AppData%\Launcher\commands.json. En el primer arranque siembra un acceso
/// directo a Kunai (via PowerShell) para que el modulo no aparezca vacio.
/// </summary>
public class CommandsService
{
    private static readonly string AppFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Launcher");

    private static readonly string LibraryPath = Path.Combine(AppFolder, "commands.json");

    public ObservableCollection<CommandShortcut> Commands { get; } = new();

    public CommandsService()
    {
        Directory.CreateDirectory(AppFolder);
        Load();
    }

    private void Load()
    {
        Commands.Clear();

        if (!File.Exists(LibraryPath))
        {
            SeedDefaults();
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(LibraryPath);
            var items = JsonSerializer.Deserialize<List<CommandShortcut>>(json) ?? new List<CommandShortcut>();
            foreach (var c in items) Commands.Add(c);
        }
        catch
        {
            // commands.json corrupto: arrancamos con los valores por defecto en vez de reventar.
            SeedDefaults();
        }
    }

    private void SeedDefaults()
    {
        Commands.Add(new CommandShortcut
        {
            Name = "Kunai",
            Description = "Streaming de anime, series y peliculas en terminal",
            Category = CommandCategory.Cli,
            Icon = "\uE756",
            Target = "kunai",
            Arguments = "",
            Terminal = TerminalHost.PowerShell
        });
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(Commands.ToList(), options));
    }

    public void Add(CommandShortcut command)
    {
        Commands.Add(command);
        Save();
    }

    /// <summary>Anade varios comandos de una vez (usado por la ventana de Sugerencias)
    /// guardando solo una vez al final.</summary>
    public void AddRange(IEnumerable<CommandShortcut> commands)
    {
        foreach (var c in commands) Commands.Add(c);
        Save();
    }

    public void Update(CommandShortcut _) => Save();

    public void Remove(CommandShortcut command)
    {
        Commands.Remove(command);
        Save();
    }

    /// <summary>PATH fresco leido directamente del registro (Machine + User), para no
    /// depender del PATH heredado del proceso (que puede estar desactualizado si el
    /// Launcher lleva abierto desde antes de instalar la herramienta).</summary>
    public static string GetRefreshedPath()
    {
        var machinePath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? "";
        var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
        return string.IsNullOrEmpty(userPath) ? machinePath : $"{machinePath};{userPath}";
    }

    /// <summary>True si el texto parece un protocolo/URI (steam://, ms-settings:, etc.)
    /// en vez de una ruta de disco, en cuyo caso no se puede verificar localmente.</summary>
    private static bool LooksLikeProtocolOrUri(string target)
    {
        if (target.Contains("://", StringComparison.Ordinal)) return true;

        var colonIndex = target.IndexOf(':');
        if (colonIndex <= 0) return false;

        // Las rutas tipo "C:\..." tienen una sola letra antes de los dos puntos.
        var isDriveLetter = colonIndex == 1 && char.IsLetter(target[0]);
        return !isDriveLetter;
    }

    /// <summary>Comprueba si el comando/ruta se puede resolver en este equipo: como
    /// ruta absoluta, como comando en el PATH actualizado, o como utilidad de
    /// System32 (devmgmt.msc, taskmgr.exe, etc, que a veces no aparecen en PATH
    /// explicitamente pero siempre se resuelven ahi). No verifica protocolos/URIs.</summary>
    public static bool TargetExists(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        if (LooksLikeProtocolOrUri(target)) return true;

        if (Path.IsPathRooted(target))
        {
            return File.Exists(target);
        }

        var extensions = new[] { "", ".exe", ".cmd", ".bat", ".ps1", ".msc" };
        var pathDirs = GetRefreshedPath().Split(';', StringSplitOptions.RemoveEmptyEntries);
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);

        foreach (var dir in pathDirs.Append(system32))
        {
            var trimmedDir = dir.Trim();
            if (trimmedDir.Length == 0) continue;

            foreach (var ext in extensions)
            {
                try
                {
                    if (File.Exists(Path.Combine(trimmedDir, target + ext))) return true;
                }
                catch
                {
                    // Directorio de PATH invalido/inaccesible: lo ignoramos y seguimos con el resto.
                }
            }
        }

        return false;
    }

    /// <summary>Ejecuta el acceso directo. Segun Terminal, lo lanza directamente,
    /// dentro de cmd.exe, o dentro de powershell.exe (con -NoExit para que la
    /// consola no se cierre sola si el comando falla o termina). Si RunAsAdmin
    /// esta activo, se eleva via el verbo "runas" (dispara el dialogo de UAC).
    ///
    /// El refresco de PATH se inyecta dentro del propio comando (set PATH=.../
    /// $env:Path=...) en vez de via ProcessStartInfo.EnvironmentVariables, porque
    /// esa propiedad no esta disponible cuando se eleva el proceso (UseShellExecute
    /// debe ser true para "runas", y EnvironmentVariables solo funciona con false).</summary>
    public void Launch(CommandShortcut command)
    {
        if (string.IsNullOrWhiteSpace(command.Target)) return;

        var startInfo = new ProcessStartInfo { UseShellExecute = true };

        var fullCommand = string.IsNullOrWhiteSpace(command.Arguments)
            ? command.Target
            : $"{command.Target} {command.Arguments}";

        switch (command.Terminal)
        {
            case TerminalHost.Cmd:
            {
                var refreshedPath = GetRefreshedPath();
                startInfo.FileName = "cmd.exe";
                startInfo.ArgumentList.Add("/k");
                startInfo.ArgumentList.Add($"set \"PATH={refreshedPath}\" & {fullCommand}");
                break;
            }

            case TerminalHost.PowerShell:
            {
                var refreshedPath = GetRefreshedPath().Replace("'", "''");
                startInfo.FileName = "powershell.exe";
                startInfo.ArgumentList.Add("-NoExit");
                startInfo.ArgumentList.Add("-Command");
                startInfo.ArgumentList.Add($"$env:Path = '{refreshedPath}'; {fullCommand}");
                break;
            }

            default:
                startInfo.FileName = command.Target;
                startInfo.Arguments = command.Arguments;
                break;
        }

        if (!string.IsNullOrWhiteSpace(command.WorkingDirectory))
        {
            startInfo.WorkingDirectory = command.WorkingDirectory;
        }

        if (command.RunAsAdmin)
        {
            startInfo.Verb = "runas";
        }

        try
        {
            Process.Start(startInfo);
            command.LastUsed = DateTime.Now;
            Save();
        }
        catch (Win32Exception)
        {
            // El usuario ha cancelado el dialogo de UAC (denegar elevacion): no es un fallo real.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CommandsService] No se pudo ejecutar '{command.Name}': {ex.Message}");
        }
    }
}