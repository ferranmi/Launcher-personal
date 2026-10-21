using System.Windows;
using System.Windows.Threading;
using Launcher.Core.Models;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Launcher.Core.Services;

/// <summary>
/// Avisos de Windows (globo/notificacion) para los problemas del Launcher: cada entrada del registro con nivel
/// Warning o Defcon que genera el StatusEvaluator (incluidos los fallos de inicio de sesion de las webs)
/// se notifica ademas de aparecer en el Registro del dashboard. Limitado para no saturar: la misma entrada
/// no se repite en 60 s y entre dos avisos hay al menos 2 s (el Registro las muestra todas igualmente).
/// Si Windows tiene activado "No molestar", el aviso no se vera.
/// </summary>
public static class NotificationService
{
    private static Forms.NotifyIcon? _icon;
    private static DispatcherTimer? _hideTimer;
    private static DateTime _lastShown = DateTime.MinValue;
    private static readonly Dictionary<string, DateTime> Recent = new();

    public static void Initialize(StatusEvaluator evaluator) => evaluator.EventRaised += OnEvent;

    private static void OnEvent(StatusEvent e)
    {
        if (e.Level < StatusLevel.Warning) return;

        // Entradas viejas (p.ej. las que lee del Visor de eventos al arrancar) no merecen un aviso.
        if (DateTime.Now - e.Time > TimeSpan.FromMinutes(2)) return;

        Application.Current?.Dispatcher.BeginInvoke(() => Show(e.Level, e.Source, e.Message));
    }

    public static void Show(StatusLevel level, string source, string message)
    {
        var now = DateTime.Now;
        var key = $"{level}|{source}|{message}";

        if (Recent.TryGetValue(key, out var last) && now - last < TimeSpan.FromSeconds(60)) return;
        Recent[key] = now;

        if (Recent.Count > 100)
        {
            foreach (var old in Recent.Where(p => now - p.Value > TimeSpan.FromMinutes(5)).Select(p => p.Key).ToList())
                Recent.Remove(old);
        }

        if (now - _lastShown < TimeSpan.FromSeconds(2)) return;
        _lastShown = now;

        try
        {
            var icon = EnsureIcon();
            var title = (level == StatusLevel.Defcon ? "MISIL ENTRANTE" : "BLOQUEO DE RADAR") + " \u00B7 " + source;

            icon.BalloonTipTitle = title.Length > 60 ? title[..60] : title;
            icon.BalloonTipText = message.Length > 250 ? message[..250] : message;
            icon.BalloonTipIcon = level == StatusLevel.Defcon ? Forms.ToolTipIcon.Error : Forms.ToolTipIcon.Warning;
            icon.Visible = true;
            icon.ShowBalloonTip(8000);

            // El icono solo hace falta para mostrar el aviso: se oculta unos segundos despues.
            _hideTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _hideTimer.Tick -= HideIcon;
            _hideTimer.Tick += HideIcon;
            _hideTimer.Stop();
            _hideTimer.Start();
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo mostrar la notificacion", ex);
        }
    }

    private static void HideIcon(object? sender, EventArgs e)
    {
        _hideTimer?.Stop();
        if (_icon != null) _icon.Visible = false;
    }

    private static Forms.NotifyIcon EnsureIcon()
    {
        if (_icon != null) return _icon;

        Drawing.Icon appIcon;
        try
        {
            appIcon = Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? Drawing.SystemIcons.Warning;
        }
        catch
        {
            appIcon = Drawing.SystemIcons.Warning;
        }

        _icon = new Forms.NotifyIcon { Icon = appIcon, Text = "Launcher" };
        _icon.BalloonTipClicked += (_, _) => WindowBehaviorService.Instance?.ShowWindow();
        _icon.MouseClick += (_, _) => WindowBehaviorService.Instance?.ShowWindow();

        Application.Current.Exit += (_, _) =>
        {
            _icon.Visible = false;
            _icon.Dispose();
        };

        return _icon;
    }
}
