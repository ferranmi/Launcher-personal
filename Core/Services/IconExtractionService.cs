using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Launcher.Core.Services;

/// <summary>
/// Logica compartida para anadir apps/juegos desde un .exe: detectar el
/// ejecutable principal de una carpeta y extraer su icono como imagen de
/// portada. La usan tanto AddEditGameWindow (Juegos) como AddEditMediaAppWindow
/// (Media) para no duplicar la misma heuristica dos veces.
/// </summary>
public static class IconExtractionService
{
    /// <summary>Nombres de .exe habituales que casi nunca son la app/juego en si
    /// (instaladores, desinstaladores, redistribuibles, anticheat...).</summary>
    private static readonly string[] ExcludedExeKeywords =
    {
        "unins", "uninstall", "setup", "install", "redist", "vcredist",
        "dxsetup", "crashpad", "crashreport", "battleye", "easyanticheat",
        "eosbootstrapper", "directx", "vc_redist"
    };

    /// <summary>De los .exe en la raiz de una carpeta, descarta los que parecen
    /// instaladores/utilidades por nombre y, si queda mas de uno, asume que el
    /// ejecutable real suele ser el mas pesado.</summary>
    public static string? FindLikelyExecutable(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return null;

        var candidates = Directory.GetFiles(folderPath, "*.exe", SearchOption.TopDirectoryOnly)
            .Where(f => !LooksLikeUtilityExe(Path.GetFileName(f)))
            .ToList();

        return candidates.Count == 0
            ? null
            : candidates.OrderByDescending(f => new FileInfo(f).Length).First();
    }

    private static bool LooksLikeUtilityExe(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return ExcludedExeKeywords.Any(lower.Contains);
    }

    /// <summary>Extrae el icono asociado a exePath y lo guarda como PNG en
    /// CoverArtService.CoversFolder con el nombre "{saveAsId}.png". Devuelve la
    /// ruta guardada, o null si no se pudo extraer (icono ausente, proceso sin
    /// permisos, etc.) sin lanzar excepcion: no debe bloquear el guardado de
    /// quien la llama.</summary>
    public static string? ExtractAsCover(string exePath, string saveAsId)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            if (icon == null) return null;

            using var bitmap = icon.ToBitmap();
            Directory.CreateDirectory(CoverArtService.CoversFolder);
            var path = Path.Combine(CoverArtService.CoversFolder, $"{saveAsId}.png");
            bitmap.Save(path, ImageFormat.Png);
            return path;
        }
        catch
        {
            return null;
        }
    }
}
