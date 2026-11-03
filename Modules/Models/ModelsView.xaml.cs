using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Launcher.Core.Services;
using Microsoft.Win32;

namespace Launcher.Modules.Models;

public partial class ModelsView : UserControl
{
    private readonly ModelCatalogService _catalog = ModelCatalogService.Instance;
    private readonly ModelsSettings _settings = ModelsSettings.Instance;

    private bool _initialized;

    public ModelsView()
    {
        InitializeComponent();

        _catalog.Changed += (_, _) => UpdateStatus();

        InstallPathBox.Text = _settings.InstallPath ?? "";
        UpdateFoldersText();

        TabLibrary.IsChecked = true;
        Loaded += ModelsView_Loaded;
    }

    private async void ModelsView_Loaded(object sender, RoutedEventArgs e)
    {
        // Loaded se repite cada vez que la vista vuelve al arbol visual: solo cargamos la primera vez.
        if (_initialized) return;
        _initialized = true;

        try
        {
            _catalog.LoadCache();
            UpdateStatus();

            await LibraryTab.RescanAsync();
            await WildcardsTab.RefreshAsync();
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la carga inicial del modulo Modelos", ex);
        }
    }

    // ===================== Pestanas =====================

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelHost == null) return; // se dispara durante InitializeComponent

        var tag = (string)((RadioButton)sender).Tag;
        foreach (var panel in PanelHost.Children.OfType<FrameworkElement>())
        {
            panel.Visibility = (string?)panel.Tag == tag ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateStatus()
    {
        var models = _catalog.Models;
        var lora = models.Count(m => m.Type == ModelType.Lora);
        var checkpoints = models.Count(m => m.Type == ModelType.Checkpoint);
        var lycoris = models.Count(m => m.Type == ModelType.LyCoris);

        var text = $"{models.Count} modelos  \u00b7  LoRA {lora}  \u00b7  Checkpoints {checkpoints}  \u00b7  LyCORIS {lycoris}";

        StatusText.Text = _catalog.LastScan is { } last
            ? $"{text}  \u00b7  ultimo escaneo {last:dd/MM HH:mm}"
            : "sin escanear todavia: configura la carpeta de A1111 en Configuracion";
    }

    // ===================== Configuracion =====================

    private void BrowseInstall_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Selecciona la carpeta de AUTOMATIC1111" };

        if (!string.IsNullOrWhiteSpace(InstallPathBox.Text) && Directory.Exists(InstallPathBox.Text))
        {
            dialog.InitialDirectory = InstallPathBox.Text;
        }

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            InstallPathBox.Text = dialog.FolderName;
        }
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var path = InstallPathBox.Text.Trim();

        if (path.Length > 0 && !Directory.Exists(path))
        {
            MessageBox.Show(Window.GetWindow(this), "Esa carpeta no existe.", "Configuracion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.InstallPath = path.Length == 0 ? null : path;
        _settings.Save();
        UpdateFoldersText();

        await LibraryTab.RescanAsync();
        await WildcardsTab.RefreshAsync();
    }

    private void UpdateFoldersText()
    {
        var sb = new StringBuilder();

        void Add(string label, IReadOnlyList<string> folders)
        {
            if (folders.Count == 0)
            {
                sb.AppendLine($"{label}: (sin configurar)");
                return;
            }

            foreach (var folder in folders)
            {
                sb.AppendLine($"{label}: {folder}{(Directory.Exists(folder) ? "" : "   [no existe]")}");
            }
        }

        Add("LoRA       ", _settings.FoldersFor(ModelType.Lora));
        Add("Checkpoints", _settings.FoldersFor(ModelType.Checkpoint));
        Add("LyCORIS    ", _settings.FoldersFor(ModelType.LyCoris));
        Add("Wildcards  ", _settings.WildcardFoldersEffective());

        FoldersText.Text = sb.ToString().TrimEnd();
    }
}
