using System.Windows;
using System.Windows.Input;
using Launcher.Core.Services;
using WinForms = System.Windows.Forms;

namespace Launcher.Modules.Games;

public partial class ConfigureSyncFoldersWindow : Window
{
    /// <summary>True si el usuario guardo (para que quien abrio el dialogo sepa si debe re-sincronizar).</summary>
    public bool Saved { get; private set; }

    public ConfigureSyncFoldersWindow()
    {
        InitializeComponent();

        var settings = LibraryPathsService.Instance;
        SteamPathBox.Text = settings.SteamPath ?? "";
        EpicPathBox.Text = settings.EpicManifestsPath ?? "";
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
        var settings = LibraryPathsService.Instance;
        settings.SteamPath = string.IsNullOrWhiteSpace(SteamPathBox.Text) ? null : SteamPathBox.Text.Trim();
        settings.EpicManifestsPath = string.IsNullOrWhiteSpace(EpicPathBox.Text) ? null : EpicPathBox.Text.Trim();
        settings.Save();

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
