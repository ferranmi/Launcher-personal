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

        LoraEditor.Initialize("CARPETAS DE LORA", _settings.LoraFolders, OnFoldersChanged);
        CheckpointEditor.Initialize("CARPETAS DE CHECKPOINTS", _settings.CheckpointFolders, OnFoldersChanged);
        LyCorisEditor.Initialize("CARPETAS DE LYCORIS", _settings.LyCorisFolders, OnFoldersChanged);
        WildcardEditor.Initialize("CARPETAS DE WILDCARDS", _settings.WildcardFolders, OnFoldersChanged);

        ReferencesTab.ShowInLibraryRequested += name =>
        {
            CombosTab.WildcardsChanged += async () =>
        {
            try { await WildcardsTab.RefreshAsync(); }
            catch (Exception ex) { LogService.Error("No se pudo refrescar Wildcards tras guardar combinaciones", ex); }
        };

        CivitaiTab.DownloadsQueued += () => TabDownloads.IsChecked = true;
        ReferencesTab.SearchInCivitaiRequested += name =>
        {
            TabCivitai.IsChecked = true;
            CivitaiTab.SearchFor(name, $"referencia ausente en wildcards: {name}");
        };

        // Al terminar una descarga se reescanea la biblioteca para que el modelo aparezca.
        DownloadQueueService.Instance.ItemCompleted += async _ =>
        {
            try
            {
                while (_catalog.IsScanning) await Task.Delay(500);
                await _catalog.ScanAsync();
            }
            catch (Exception ex)
            {
                LogService.Error("No se pudo reescanear tras una descarga", ex);
            }
        };

        LoadDownloadSettings();

        TabLibrary.IsChecked = true;
            LibraryTab.ShowModel(name);
        };
        ReferencesTab.WildcardsChanged += async () =>
        {
            try { await WildcardsTab.RefreshAsync(); }
            catch (Exception ex) { LogService.Error("No se pudo refrescar Wildcards tras corregir referencias", ex); }
        };

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

    // ===================== Civitai y descargas =====================

    private void LoadDownloadSettings()
    {
        MaxDownloadsBox.Text = _settings.MaxConcurrentDownloads.ToString();
        AutoConcurrencyCheck.IsChecked = _settings.AutoConcurrency;
        SpeedLimitBox.Text = _settings.SpeedLimitKBps.ToString();
        RetriesBox.Text = _settings.DownloadRetries.ToString();
        CategoriesBox.Text = string.Join(", ", _settings.Categories);
        ApiKeyStatus.Text = CivitaiService.Instance.HasApiKey ? "clave guardada" : "sin clave guardada";
    }

    private void SaveApiKey_Click(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password.Trim();
        var ok = CivitaiService.Instance.SaveApiKey(key);

        ApiKeyBox.Clear();
        ApiKeyStatus.Text = !ok
            ? "no se pudo guardar la clave en el Administrador de credenciales"
            : key.Length == 0 ? "clave borrada" : "clave guardada";
    }

    private async void TestApiKey_Click(object sender, RoutedEventArgs e)
    {
        ApiKeyStatus.Text = "probando conexion...";

        try
        {
            var (ok, message) = await CivitaiService.Instance.TestConnectionAsync();
            ApiKeyStatus.Text = message;
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la prueba de conexion con Civitai", ex);
            ApiKeyStatus.Text = "no se pudo probar la conexion";
        }
    }

    private void SaveDownloadSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MaxDownloadsBox.Text.Trim(), out var max) || max < 1 || max > 8 ||
            !int.TryParse(SpeedLimitBox.Text.Trim(), out var speed) || speed < 0 ||
            !int.TryParse(RetriesBox.Text.Trim(), out var retries) || retries < 0 || retries > 10)
        {
            MessageBox.Show(Window.GetWindow(this),
                "Revisa los numeros: simultaneas entre 1 y 8, limite 0 o mas, reintentos entre 0 y 10.",
                "Ajustes de descargas", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var categories = CategoriesBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (categories.Count == 0)
        {
            MessageBox.Show(Window.GetWindow(this), "Debe haber al menos una categoria.", "Ajustes de descargas",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.MaxConcurrentDownloads = max;
        _settings.AutoConcurrency = AutoConcurrencyCheck.IsChecked == true;
        _settings.SpeedLimitKBps = speed;
        _settings.DownloadRetries = retries;
        _settings.Categories = categories;
        _settings.Save();

        DownloadQueueService.Instance.ApplySettings();
        LoadDownloadSettings();
    }

    private void OnFoldersChanged()
    {
        _settings.Save();
        UpdateFoldersText();
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
