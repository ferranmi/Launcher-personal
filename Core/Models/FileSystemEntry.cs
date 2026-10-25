namespace Launcher.Core.Models;

/// <summary>Fila del explorador del panel lateral: una subcarpeta o un archivo.</summary>
public class FileSystemEntry
{
    public string Name { get; init; } = "";
    public string FullPath { get; init; } = "";
    public bool IsDirectory { get; init; }

    /// <summary>Glifo Segoe MDL2: carpeta o documento.</summary>
    public string Icon => IsDirectory ? "\uE8B7" : "\uE7C3";

    /// <summary>Tamano del archivo (vacio en carpetas).</summary>
    public string Detail { get; init; } = "";

    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        var i = 0;
        while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
        return $"{size:0.#} {units[i]}";
    }
}
