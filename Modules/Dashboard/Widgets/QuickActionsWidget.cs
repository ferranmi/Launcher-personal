using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Launcher.Core.Services;

namespace Launcher.Modules.Dashboard.Widgets;

/// <summary>Botones de accion rapida del sistema: bloquear, silenciar el volumen, administrador de tareas,
/// ajustes de Windows, captura de pantalla y explorador. Solo acciones inocuas (nada destructivo).</summary>
public sealed class QuickActionsWidget : UserControl
{
    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VkVolumeMute = 0xAD;
    private const uint KeyEventKeyUp = 0x0002;

    public QuickActionsWidget()
    {
        var wrap = new WrapPanel();
        wrap.Children.Add(Tile("\uE72E", "Bloquear", () => LockWorkStation()));
        wrap.Children.Add(Tile("\uE74F", "Silenciar", ToggleMute));
        wrap.Children.Add(Tile("\uE9D9", "Tareas", () => Open("taskmgr.exe")));
        wrap.Children.Add(Tile("\uE713", "Ajustes", () => Open("ms-settings:")));
        wrap.Children.Add(Tile("\uE722", "Captura", () => Open("ms-screenclip:")));
        wrap.Children.Add(Tile("\uE8B7", "Explorador", () => Open("explorer.exe")));

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = wrap };
    }

    private static Button Tile(string glyph, string label, Action action)
    {
        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(new TextBlock
        {
            Text = glyph,
            FontFamily = Kit.Icons,
            FontSize = 18,
            Foreground = Kit.Br("Brush.Accent"),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Margin = new Thickness(0, 5, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var button = Kit.ActionButton(content, (_, _) =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogService.Error($"Accion rapida '{label}' fallo", ex);
            }
        });
        button.Width = 100;
        button.Height = 56;
        button.Margin = new Thickness(0, 0, 8, 8);
        return button;
    }

    private static void ToggleMute()
    {
        keybd_event(VkVolumeMute, 0, 0, UIntPtr.Zero);
        keybd_event(VkVolumeMute, 0, KeyEventKeyUp, UIntPtr.Zero);
    }

    private static void Open(string target) =>
        Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
}
