using System.IO;
using System.Windows.Media;
using Launcher.Core.Mvvm;

namespace Launcher.Core.Models;

/// <summary>Fichero preparado para enviar. La miniatura llega despues (asincrona),
/// por eso Thumbnail notifica el cambio y la UI la muestra cuando esta lista.</summary>
public class StagedFile : ViewModelBase
{
    public string Path { get; }
    public string Name { get; }
    public long SizeBytes { get; }
    public string SizeText => FormatSize(SizeBytes);

    private ImageSource? _thumbnail;
    public ImageSource? Thumbnail { get => _thumbnail; set => SetField(ref _thumbnail, value); }

    public StagedFile(string path)
    {
        Path = path;
        var info = new FileInfo(path);
        Name = info.Name;
        SizeBytes = info.Exists ? info.Length : 0;
    }

    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        var i = 0;
        while (size >= 1024 && i < units.Length - 1)
        {
            size /= 1024;
            i++;
        }
        return $"{size:0.#} {units[i]}";
    }
}
