using System.Diagnostics;
using System.Linq;
using Launcher.Core.Models;
using Launcher.Modules.Commands;
using Microsoft.Win32;

namespace Launcher.Modules.Games;

/// <summary>
/// Abre los enlaces y documentos de la ficha de un juego. Los enlaces usan la misma logica
/// que el formulario de Comandos: localizan el navegador con CommandsService.ResolveExecutable
/// (PATH, System32 y "App Paths" del registro) y anaden el argumento de ventana privada con
/// CommandsService.GetIncognitoArgument. Solo se abren URLs http/https.
/// </summary>
public static class GameLinkLauncher
{
    /// <summary>Navegadores que se ofrecen al editar un enlace (clave = nombre del ejecutable).</summary>
    public static readonly (string Key, string Name)[] KnownBrowsers =
    {
        ("chrome", "Chrome"),
        ("msedge", "Edge"),
        ("firefox", "Firefox"),
        ("brave", "Brave"),
        ("opera", "Opera"),
        ("vivaldi", "Vivaldi"),
    };

    /// <summary>Los navegadores de la lista que estan instalados en este equipo.</summary>
    public static IEnumerable<(string Key, string Name)> InstalledBrowsers() =>
        KnownBrowsers.Where(b => CommandsService.ResolveExecutable(b.Key) != null);

    /// <summary>Clave del navegador predeterminado de Windows para enlaces https, o null si no
    /// es uno de los conocidos.</summary>
    public static string? GetDefaultBrowserKey()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");

            var progId = (key?.GetValue("ProgId") as string)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(progId)) return null;

            if (progId.Contains("chrome")) return "chrome";
            if (progId.Contains("edge")) return "msedge";
            if (progId.Contains("firefox")) return "firefox";
            if (progId.Contains("brave")) return "brave";
            if (progId.Contains("opera")) return "opera";
            if (progId.Contains("vivaldi")) return "vivaldi";
        }
        catch
        {
            // sin acceso al registro: no sabemos cual es.
        }

        return null;
    }

    /// <summary>Argumento de ventana privada del navegador indicado ("" = el predeterminado),
    /// o null si no se conoce o no tiene modo privado.</summary>
    public static string? GetIncognitoFlag(string? browserKey)
    {
        var key = string.IsNullOrEmpty(browserKey) ? GetDefaultBrowserKey() : browserKey;
        return key == null ? null : CommandsService.GetIncognitoArgument(key);
    }

    /// <summary>Abre el enlace con su navegador y modo. Si el navegador elegido no se encuentra,
    /// cae al predeterminado (sin ventana privada).</summary>
    public static bool TryOpen(GameLink link, out string? error)
    {
        error = null;

        if (!TryParseWebUrl(link.Url, out var uri))
        {
            error = "La direccion del enlace no es valida (solo http o https).";
            return false;
        }

        try
        {
            var browserKey = string.IsNullOrEmpty(link.Browser)
                ? (link.Incognito ? GetDefaultBrowserKey() : null)
                : link.Browser;

            var exe = browserKey != null ? CommandsService.ResolveExecutable(browserKey) : null;

            if (exe == null)
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                return true;
            }

            var flag = link.Incognito ? CommandsService.GetIncognitoArgument(browserKey) : null;
            var arguments = flag == null ? $"\"{uri.AbsoluteUri}\"" : $"{flag} \"{uri.AbsoluteUri}\"";

            Process.Start(new ProcessStartInfo { FileName = exe, Arguments = arguments, UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Abre una URL http/https con el navegador predeterminado (tienda, capturas).</summary>
    public static bool OpenUrl(string? url)
    {
        if (!TryParseWebUrl(url, out var uri)) return false;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Abre un archivo o carpeta local con su programa por defecto.</summary>
    public static bool TryOpenPath(string path, out string? error)
    {
        error = null;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>True si el texto es una URL absoluta http/https.</summary>
    public static bool TryParseWebUrl(string? url, out Uri uri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }
}
