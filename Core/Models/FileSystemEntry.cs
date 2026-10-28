using System.IO;
using System.Windows.Media;
using Launcher.Core.Mvvm;
using Launcher.Core.Services;

namespace Launcher.Core.Models;

public enum EntryKind { Folder, Image, Video, Audio, Text, Other }

/// <summary>Fila del explorador: una subcarpeta o un archivo, con la miniatura
/// cargada en segundo plano (solo cuando la vista de iconos la necesita).</summary>
public class FileSystemEntry : ViewModelBase
{
    private static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff", ".ico" };
    private static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg" };
    private static readonly HashSet<string> AudioExt = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".flac", ".m4a", ".ogg", ".aac", ".wma" };
    private static readonly HashSet<string> TextExt = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md", ".log", ".json", ".xml", ".cs", ".xaml", ".py", ".js", ".ts", ".html", ".css",
          ".ini", ".csv", ".yml", ".yaml", ".ps1", ".bat", ".cfg", ".toml" };

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public long Size { get; }
    public DateTime Modified { get; }
    public DateTime Created { get; }
    public EntryKind Kind { get; }
    public string Extension { get; } = "";

    public FileSystemEntry(FileSystemInfo info)
    {
        Name = info.Name;
        FullPath = info.FullName;
        Modified = info.LastWriteTime;
        Created = info.CreationTime;

        if (info is FileInfo file)
        {
            try { Size = file.Length; } catch { Size = 0; }
            Extension = file.Extension;
            Kind = KindOf(Extension);
        }
        else
        {
            IsDirectory = true;
            Kind = EntryKind.Folder;
        }
    }

    private static EntryKind KindOf(string ext)
    {
        if (ImageExt.Contains(ext)) return EntryKind.Image;
        if (VideoExt.Contains(ext)) return EntryKind.Video;
        if (AudioExt.Contains(ext)) return EntryKind.Audio;
        if (TextExt.Contains(ext)) return EntryKind.Text;
        return EntryKind.Other;
    }

    public bool IsMedia => Kind is EntryKind.Image or EntryKind.Video;

    /// <summary>Glifo Segoe MDL2 segun el tipo (se muestra mientras llega la miniatura).</summary>
    public string Icon => Kind switch
    {
        EntryKind.Folder => "\uE8B7",
        EntryKind.Image => "\uEB9F",
        EntryKind.Video => "\uE714",
        EntryKind.Audio => "\uE8D6",
        EntryKind.Text => "\uE8A5",
        _ => "\uE7C3"
    };

    public string TypeName => Kind switch
    {
        EntryKind.Folder => "Carpeta",
        EntryKind.Image => "Imagen",
        EntryKind.Video => "Video",
        EntryKind.Audio => "Audio",
        EntryKind.Text => "Texto",
        _ => string.IsNullOrEmpty(Extension) ? "Archivo" : Extension.TrimStart('.').ToUpperInvariant()
    };

    public string SizeText => IsDirectory ? "" : FormatSize(Size);
    public string ModifiedText => Modified.ToString("dd/MM/yyyy HH:mm");

    // ===================== Miniatura =====================

    private ImageSource? _thumbnail;
    private bool _thumbRequested;

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        private set { if (SetField(ref _thumbnail, value)) OnPropertyChanged(nameof(HasThumbnail)); }
    }

    public bool HasThumbnail => _thumbnail != null;

    /// <summary>Pide la miniatura una sola vez. Se cancela con el token de la carpeta actual.</summary>
    public async void RequestThumbnail(CancellationToken ct)
    {
        if (_thumbRequested) return;
        _thumbRequested = true;

        var image = await ThumbnailService.GetAsync(FullPath, Kind == EntryKind.Image, 128, ct);
        if (image != null && !ct.IsCancellationRequested) Thumbnail = image;
    }

    /// <summary>Reutiliza la miniatura de una carga anterior (al refrescar la misma carpeta).</summary>
    public void AdoptThumbnail(ImageSource? thumbnail)
    {
        if (thumbnail == null) return;
        _thumbRequested = true;
        Thumbnail = thumbnail;
    }

    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        var i = 0;
        while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
        return $"{size:0.#} {units[i]}";
    }
}
