using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Launcher.Modules.Models;

/// <summary>Selector de destino de un tipo de combinacion: un wildcard existente o uno nuevo. El Launcher usa dos
/// (parejas y trios), de modo que cada tipo puede ir a un archivo distinto y generarse a la vez.</summary>
public partial class DestinationPicker : UserControl
{
    private bool _ready;
    private bool _internal;

    /// <summary>El usuario ha cambiado el modo o el archivo elegido.</summary>
    public event Action? Changed;

    public DestinationPicker()
    {
        InitializeComponent();

        // Cada selector necesita su propio grupo: dos RadioButton con el mismo GroupName dentro de la misma
        // ventana se pisarian entre si.
        var group = Guid.NewGuid().ToString("N");
        ExistingRadio.GroupName = group;
        NewRadio.GroupName = group;
        ExistingRadio.IsChecked = true;

        _ready = true;
        UpdateUi();
    }

    public string Title
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }

    public bool IsNew => NewRadio.IsChecked == true;
    public string NewName => NewNameBox.Text.Trim();
    public WildcardFile? SelectedFile => FileCombo.SelectedItem as WildcardFile;

    private void UpdateUi()
    {
        FileCombo.Visibility = IsNew ? Visibility.Collapsed : Visibility.Visible;
        NewNameBox.Visibility = IsNew ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Actualiza la lista de wildcards conservando la seleccion (no dispara Changed).</summary>
    public void SetFiles(IReadOnlyList<WildcardFile> files)
    {
        var path = SelectedFile?.FullPath;

        _internal = true;
        FileCombo.ItemsSource = files;
        FileCombo.SelectedItem = files.FirstOrDefault(f => string.Equals(f.FullPath, path, StringComparison.OrdinalIgnoreCase));
        _internal = false;
    }

    /// <summary>Pasa a "existente" y selecciona ese archivo (p.ej. el wildcard recien creado).</summary>
    public void SelectExisting(string path)
    {
        _internal = true;
        ExistingRadio.IsChecked = true;
        UpdateUi();

        if (FileCombo.ItemsSource is IEnumerable<WildcardFile> files)
        {
            FileCombo.SelectedItem = files.FirstOrDefault(f => string.Equals(f.FullPath, path, StringComparison.OrdinalIgnoreCase));
        }

        _internal = false;
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || _internal) return;

        UpdateUi();
        Changed?.Invoke();
    }

    private void FileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && !_internal) Changed?.Invoke();
    }
}
