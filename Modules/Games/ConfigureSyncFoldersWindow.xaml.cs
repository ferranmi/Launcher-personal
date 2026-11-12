using System.Windows;
using System.Windows.Input;
using Launcher.Core.Services;
using WinForms = System.Windows.Forms;

namespace Launcher.Modules.Games;

public partial class ConfigureSyncFoldersWindow : Window
{
    private readonly LibraryPathsService _settings;
    private readonly bool _deferSave;

    /// <summary>True si el usuario confirmo los cambios del dialogo.</summary>
    public bool Saved { get; private set; }

    public ConfigureSyncFoldersWindow() : this(LibraryPathsService.Instance, false) { }

    public ConfigureSyncFoldersWindow(LibraryPathsService settings, bool deferSave)
    {
        _settings = settings;
        _deferSave = deferSave;
        InitializeComponent();

        SteamPathBox.Text = _settings.SteamPath ?? "";
        EpicPathBox.Text = _settings.EpicManifestsPath ?? "";
    }

    private void BrowseSteam_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog { Description = "Selecciona la carpeta de instalacion de Steam" };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            SteamPathBox.Text = dialog.SelectedPath;
        }
    }

    private void BrowseEpic_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog { Description = "Selecciona la carpeta de manifiestos de Epic (...\\Data\\Manifests)" };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            EpicPathBox.Text = dialog.SelectedPath;
        }
    }

    private void AutoDetect_Click(object sender, RoutedEventArgs e)
    {
        var steam = LibraryPathsService.AutoDetectSteamPath();
        var epic = LibraryPathsService.AutoDetectEpicManifestsPath();

        if (steam != null) SteamPathBox.Text = steam;
        if (epic != null) EpicPathBox.Text = epic;

        if (steam == null && epic == null)
        {
            MessageBox.Show(this,
                "No se ha encontrado ninguna instalacion automaticamente. Indica las carpetas a mano con el boton \"...\".",
                "Auto-deteccion", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.SteamPath = string.IsNullOrWhiteSpace(SteamPathBox.Text) ? null : SteamPathBox.Text.Trim();
        _settings.EpicManifestsPath = string.IsNullOrWhiteSpace(EpicPathBox.Text) ? null : EpicPathBox.Text.Trim();

        if (!_deferSave && !_settings.TrySave())
        {
            MessageBox.Show(this, "No se han podido guardar las rutas de las bibliotecas.",
                "Juegos", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Saved = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
