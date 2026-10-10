using System.Runtime.InteropServices;

namespace Launcher.Core.Services;

/// <summary>
/// Atajo global via hook de teclado de bajo nivel (WH_KEYBOARD_LL) en vez de
/// RegisterHotKey. Necesario porque Alt+Space esta reservado por Windows para
/// el menu de sistema (SC_KEYMENU) y RegisterHotKey no lo intercepta de forma
/// fiable en esa combinacion; el hook si, porque se ejecuta antes de que el
/// mensaje llegue al procesamiento de teclas del sistema.
/// </summary>
public class GlobalHotkeyService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_MENU = 0x12; // Alt, izquierdo o derecho

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // Mantenemos el delegate como campo: si se lo comiera el GC a mitad de
    // ejecucion, el hook dejaria de funcionar de forma intermitente.
    private LowLevelKeyboardProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private uint _targetKey;

    public event Action? HotkeyPressed;

    public void Register(uint virtualKey)
    {
        _targetKey = virtualKey;
        _proc = HookCallback;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);

        if (_hookId == IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine("[GlobalHotkeyService] No se pudo instalar el hook de teclado.");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && ((int)wParam == WM_KEYDOWN || (int)wParam == WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            if (data.vkCode == _targetKey && (GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
            {
                HotkeyPressed?.Invoke();
                return (IntPtr)1; // consumimos la tecla: evita que Windows abra el menu de sistema
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}