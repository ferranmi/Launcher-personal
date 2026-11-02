using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Launcher.Core.Services;

namespace Launcher.Modules.Dashboard.Widgets;

/// <summary>Notas rapidas: un cuadro de texto libre que se guarda solo (0,8 s despues de dejar de escribir
/// y al salir de la vista) en %AppData%\Launcher\notes.txt.</summary>
public sealed class NotesWidget : UserControl
{
    private static readonly string FilePath = Path.Combine(AppSettingsService.AppFolder, "notes.txt");

    private readonly TextBox _box = Kit.Input("notas rapidas...");
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private bool _dirty;

    public NotesWidget()
    {
        _box.AcceptsReturn = true;
        _box.TextWrapping = TextWrapping.Wrap;
        _box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        try
        {
            if (File.Exists(FilePath)) _box.Text = File.ReadAllText(FilePath);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudieron leer las notas", ex);
        }

        _box.TextChanged += (_, _) =>
        {
            _dirty = true;
            _saveTimer.Stop();
            _saveTimer.Start(); // reinicia la cuenta atras mientras se sigue escribiendo
        };

        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            Save();
        };

        Unloaded += (_, _) => Save();

        Content = _box;
    }

    private void Save()
    {
        if (!_dirty) return;

        try
        {
            Directory.CreateDirectory(AppSettingsService.AppFolder);
            File.WriteAllText(FilePath, _box.Text);
            _dirty = false;
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudieron guardar las notas", ex);
        }
    }
}
