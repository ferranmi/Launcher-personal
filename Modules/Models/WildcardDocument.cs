using System.IO;
using System.Text;

namespace Launcher.Modules.Models;

/// <summary>
/// Un archivo de wildcard cargado en memoria con todo lo necesario para volver a escribirlo sin
/// "reformatearlo": codificacion (con o sin BOM), tipo de salto de linea y si terminaba en salto.
/// Guardar es atomico (archivo temporal + reemplazo), asi que un fallo a medias no deja el original roto.
/// </summary>
public sealed class WildcardDocument
{
    public string Path { get; }
    public List<string> Lines { get; }
    public string NewLine { get; }
    public bool TrailingNewLine { get; }
    public Encoding Encoding { get; }

    private readonly long _stampTicks;
    private readonly long _stampLength;

    private WildcardDocument(string path, List<string> lines, string newLine, bool trailing, Encoding encoding,
        long stampTicks, long stampLength)
    {
        Path = path;
        Lines = lines;
        NewLine = newLine;
        TrailingNewLine = trailing;
        Encoding = encoding;
        _stampTicks = stampTicks;
        _stampLength = stampLength;
    }

    public static WildcardDocument Load(string path)
    {
        byte[] bytes;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
        }

        var info = new FileInfo(path);

        Encoding encoding;
        var offset = 0;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encoding = new UTF8Encoding(true);
            offset = 3;
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encoding = new UnicodeEncoding(false, true);
            offset = 2;
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encoding = new UnicodeEncoding(true, true);
            offset = 2;
        }
        else
        {
            encoding = new UTF8Encoding(false);
        }

        string text;
        if (encoding is UnicodeEncoding)
        {
            text = encoding.GetString(bytes, offset, bytes.Length - offset);
        }
        else
        {
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
            }
            catch (DecoderFallbackException)
            {
                // No es UTF-8 valido (p.ej. guardado en ANSI): Latin1 mapea byte a caracter sin perdidas,
                // asi que al guardar se escriben exactamente los mismos bytes.
                encoding = Encoding.Latin1;
                text = encoding.GetString(bytes, offset, bytes.Length - offset);
            }
        }

        var newLine = text.Contains("\r\n") ? "\r\n" : text.Contains('\n') ? "\n" : "\r\n";
        var trailing = text.EndsWith('\n');

        var lines = new List<string>();
        if (text.Length > 0)
        {
            lines.AddRange(text.Replace("\r\n", "\n").Split('\n'));
            if (trailing) lines.RemoveAt(lines.Count - 1);
        }

        return new WildcardDocument(path, lines, newLine, trailing, encoding,
            info.LastWriteTimeUtc.Ticks, info.Length);
    }

    /// <summary>True si el archivo ha cambiado en disco desde que se cargo (A1111 u otro programa).</summary>
    public bool ChangedOnDisk()
    {
        var info = new FileInfo(Path);
        return !info.Exists || info.LastWriteTimeUtc.Ticks != _stampTicks || info.Length != _stampLength;
    }

    public void Save(IReadOnlyList<string> lines)
    {
        var text = string.Join(NewLine, lines) + (TrailingNewLine && lines.Count > 0 ? NewLine : "");
        var preamble = Encoding.GetPreamble();
        var body = Encoding.GetBytes(text);

        var temp = Path + ".lstmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(preamble, 0, preamble.Length);
            stream.Write(body, 0, body.Length);
        }

        File.Move(temp, Path, overwrite: true);
    }
}
