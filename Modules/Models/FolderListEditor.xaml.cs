using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Launcher.Modules.Models;

/// <summary>Lista editable de carpetas (anadir/quitar). Edita directamente la lista de ModelsSettings que recibe.</summary>
public partial class FolderListEditor : UserControl
{
    private List<string> _backing = new();
    private Action? _changed;

    public FolderListEditor()
    {
        InitializeComponent();
    }

    public void Initialize(string title, List<string> backing, Action changed)
    {
        TitleText.Text = title;
        _backing = backing;
        _changed = changed;
        Reload();
    }

    private void Reload()
    {
        FoldersList.ItemsSource = null;
        FoldersList.ItemsSource = _backing.ToList();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Selecciona una carpeta" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        var folder = dialog.FolderName;
        if (_backing.Any(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase))) return;

        _backing.Add(folder);
        Reload();
        _changed?.Invoke();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (FoldersList.SelectedItem is not string folder) return;

        _backing.Remove(folder);
        Reload();
        _changed?.Invoke();
    }
}
