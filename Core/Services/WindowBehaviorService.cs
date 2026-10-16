using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Launcher.Core.Services;

/// <summary>
/// Comportamiento de la ventana principal que depende de los ajustes: bandeja del
/// sistema (cerrar a bandeja), recordar tamano/posicion, atajo global y arranque
/// minimizado. Se engancha a MainWindow con una sola linea: WindowBehaviorService.Attach(this).
/// </summary>
public sealed class WindowBehaviorService
{
    private const int HotkeyId = 0x4C41;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1;
    private const uint MOD_CONTROL = 0x2;
    private const uint MOD_SHIFT = 0x4;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int WM_NCHITTEST = 0x0084;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    public static WindowBehaviorService? Instance { get; private set; }

    private readonly Window _window;
    private readonly AppSettingsService _settings = AppSettingsService.Instance;
    private Forms.NotifyIcon? _tray;
    private bool _hotkeyRegistered;
    private bool _exiting;

    private WindowBehaviorService(Window window) => _window = window;

    public static void Attach(Window window)
    {
        var service = new WindowBehaviorService(window);
        Instance = service;
        service.Initialize();
    }

    private void Initialize()
    {
        _settings.ApplyToWindow(_window);
        ApplySavedBounds();

        _window.SourceInitialized += (_, _) =>
        {
            HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle)?.AddHook(WndProc);
            ApplyHotkey();
        };

        _window.Closing += OnClosing;

        // Sin esto, cancelar el cierre bloquearia el apagado/cierre de sesion de Windows.
        Application.Current.SessionEnding += (_, _) => _exiting = true;
        Application.Current.Exit += (_, _) => Cleanup();

        ApplyTray();

        if (Environment.GetCommandLineArgs().Contains(StartupService.MinimizedArg))
        {
            if (_settings.CloseToTray)
                _window.Loaded += (_, _) => _window.Hide();
            else
                _window.WindowState = WindowState.Minimized;
        }
    }

    /// <summary>Reaplica todos los ajustes (usado tras "Restablecer").</summary>
    public void ApplyAll()
    {
        _settings.ApplyToWindow(_window);
        ApplyTray();
        ApplyHotkey();
    }

    // ===================== Tamano y posicion =====================

    private void ApplySavedBounds()
    {
        if (!_settings.RememberWindowBounds) return;
        if (_settings.WindowLeft is not double left || _settings.WindowTop is not double top ||
            _settings.WindowWidth is not double width || _settings.WindowHeight is not double height)
        {
            return;
        }

        width = Math.Max(width, _window.MinWidth);
        height = Math.Max(height, _window.MinHeight);

        // Si el monitor donde estaba ya no existe, ignoramos la posicion guardada.
        const double margin = 100;
        var vl = SystemParameters.VirtualScreenLeft;
        var vt = SystemParameters.VirtualScreenTop;
        var vw = SystemParameters.VirtualScreenWidth;
        var vh = SystemParameters.VirtualScreenHeight;

        var visible = left + width > vl + margin && left < vl + vw - margin &&
                      top + height > vt + margin && top < vt + vh - margin;
        if (!visible) return;

        _window.WindowStartupLocation = WindowStartupLocation.Manual;
        _window.Left = left;
        _window.Top = top;
        _window.Width = width;
        _window.Height = height;
    }

    private void SaveBounds()
    {
        if (!_settings.RememberWindowBounds) return;

        var bounds = _window.RestoreBounds;
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;

        _settings.WindowLeft = bounds.Left;
        _settings.WindowTop = bounds.Top;
        _settings.WindowWidth = bounds.Width;
        _settings.WindowHeight = bounds.Height;
        _settings.Save();
    }

    // ===================== Cierre / bandeja =====================

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        SaveBounds();

        if (_settings.CloseToTray && !_exiting)
        {
            e.Cancel = true;
            _window.Hide();
        }
    }

    public void ApplyTray()
    {
        if (_settings.CloseToTray)
        {
            EnsureTray();
            _tray!.Visible = true;
        }
        else if (_tray != null)
        {
            _tray.Visible = false;
        }
    }

    private void EnsureTray()
    {
        if (_tray != null) return;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir", null, (_, _) => ShowWindow());
        menu.Items.Add("Salir", null, (_, _) => ExitApp());

        _tray = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Launcher",
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => ShowWindow();
    }

    /// <summary>Icono de bandeja generado en codigo: el cuadrado verde del titulo.</summary>
    private static Drawing.Icon CreateIcon()
    {
        using var bitmap = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(0x3E, 0xCF, 0x7E));
            g.FillRectangle(brush, 8, 8, 16, 16);
        }

        var handle = bitmap.GetHicon();
        using var temp = Drawing.Icon.FromHandle(handle);
        var icon = (Drawing.Icon)temp.Clone();
        DestroyIcon(handle);
        return icon;
    }

    /// <summary>Carga el icono real de la app desde los recursos; si falla, usa el generado.</summary>
    private static Drawing.Icon LoadIcon()
    {
        try
        {
            var stream = Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/launcher.ico"))?.Stream;

            if (stream != null)
            {
                using (stream)
                {
                    return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
                }
            }
        }
        catch
        {
            // Recurso ausente o ilegible: caemos al icono generado en codigo.
        }

        return CreateIcon();
    }

    private void ExitApp()
    {
        _exiting = true;
        Application.Current.Shutdown();
    }

    public void ShowWindow()
    {
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();

        // Truco para forzar el primer plano aunque Windows se resista.
        _window.Topmost = true;
        _window.Topmost = _settings.AlwaysOnTop;
    }

    private void ToggleWindow()
    {
        if (_window.IsVisible && _window.WindowState != WindowState.Minimized && _window.IsActive)
        {
            if (_settings.CloseToTray) _window.Hide();
            else _window.WindowState = WindowState.Minimized;
        }
        else
        {
            ShowWindow();
        }
    }

    // ===================== Atajo global =====================

    /// <summary>Registra el atajo elegido en los ajustes. Devuelve false si otra app ya lo usa.</summary>
    public bool ApplyHotkey()
    {
        UnregisterHotkey();

        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero) return true; // SourceInitialized lo volvera a intentar

        (uint mods, uint vk) = _settings.Hotkey switch
        {
            "Ctrl+Alt+L" => (MOD_CONTROL | MOD_ALT, 0x4Cu),
            "Ctrl+Alt+Space" => (MOD_CONTROL | MOD_ALT, 0x20u),
            "Ctrl+Shift+Space" => (MOD_CONTROL | MOD_SHIFT, 0x20u),
            _ => (0u, 0u)
        };

        if (vk == 0) return true; // sin atajo

        _hotkeyRegistered = RegisterHotKey(handle, HotkeyId, mods | MOD_NOREPEAT, vk);
        return _hotkeyRegistered;
    }

    private void UnregisterHotkey()
    {
        if (!_hotkeyRegistered) return;
        UnregisterHotKey(new WindowInteropHelper(_window).Handle, HotkeyId);
        _hotkeyRegistered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            ToggleWindow();
            handled = true;
        }
        else if (msg == WM_GETMINMAXINFO)
        {
            FixMaximizedBounds(hwnd, lParam);
            handled = true;
        }
        else if (msg == WM_NCHITTEST && _window.WindowState == WindowState.Normal)
        {
            var hit = HitTestEdges(hwnd, lParam);
            if (hit != 0)
            {
                handled = true;
                return (IntPtr)hit;
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>Devuelve el codigo HT* si el cursor esta en un borde o esquina (6 px), o 0.</summary>
    private int HitTestEdges(IntPtr hwnd, IntPtr lParam)
    {
        if (!GetWindowRect(hwnd, out var r)) return 0;

        var x = (short)(lParam.ToInt64() & 0xFFFF);
        var y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        var border = (int)Math.Round(6 * System.Windows.Media.VisualTreeHelper.GetDpi(_window).DpiScaleX);

        var left = x - r.Left < border;
        var right = r.Right - x <= border;
        var top = y - r.Top < border;
        var bottom = r.Bottom - y <= border;

        if (top && left) return 13;      // HTTOPLEFT
        if (top && right) return 14;     // HTTOPRIGHT
        if (bottom && left) return 16;   // HTBOTTOMLEFT
        if (bottom && right) return 17;  // HTBOTTOMRIGHT
        if (left) return 10;             // HTLEFT
        if (right) return 11;            // HTRIGHT
        if (top) return 12;              // HTTOP
        if (bottom) return 15;           // HTBOTTOM
        return 0;
    }

    /// <summary>Una ventana sin bordes y transparente maximiza sobre todo el monitor y tapa la
    /// barra de tareas. Aqui le decimos que su area maxima es el area de trabajo del monitor.</summary>
    private static void FixMaximizedBounds(IntPtr hwnd, IntPtr lParam)
    {
        var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref monitorInfo))
            {
                var work = monitorInfo.rcWork;
                var full = monitorInfo.rcMonitor;

                info.ptMaxPosition.X = Math.Abs(work.Left - full.Left);
                info.ptMaxPosition.Y = Math.Abs(work.Top - full.Top);
                info.ptMaxSize.X = Math.Abs(work.Right - work.Left);
                info.ptMaxSize.Y = Math.Abs(work.Bottom - work.Top);
            }
        }

        Marshal.StructureToPtr(info, lParam, true);
    }

    public void Cleanup()
    {
        UnregisterHotkey();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
    }
}