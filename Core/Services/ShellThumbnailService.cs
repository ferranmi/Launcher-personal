using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Launcher.Core.Services;

/// <summary>
/// Miniaturas de ficheros usando la propia cache de miniaturas de Windows
/// (IShellItemImageFactory): las mismas que ve el Explorador. Sirve para imagenes y
/// videos, y para cualquier otro tipo devuelve su icono, asi que nunca queda vacio.
/// </summary>
public static class ShellThumbnailService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string path, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    /// <summary>Genera la miniatura fuera del hilo de la UI. Devuelve null si falla.</summary>
    public static Task<ImageSource?> GetAsync(string path, int size) => Task.Run(() => Get(path, size));

    private static ImageSource? Get(string path, int size)
    {
        var hbitmap = IntPtr.Zero;

        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory) != 0 || factory == null)
                return null;

            try
            {
                // flags = 0: miniatura si existe; si no, el icono del tipo de fichero.
                if (factory.GetImage(new SIZE { cx = size, cy = size }, 0, out hbitmap) != 0 || hbitmap == IntPtr.Zero)
                    return null;

                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze(); // congelada: se puede pasar al hilo de la UI
                return source;
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
        }
        catch
        {
            return null; // sin miniatura: la UI muestra solo el nombre
        }
        finally
        {
            if (hbitmap != IntPtr.Zero) DeleteObject(hbitmap);
        }
    }
}
