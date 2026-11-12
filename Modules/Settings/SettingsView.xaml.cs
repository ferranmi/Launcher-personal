using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Modules.Games;
using WinForms = System.Windows.Forms;

namespace Launcher.Modules.Settings;

public partial class SettingsView : UserControl
{
    private static string? _pendingSection;

    public static void OpenSection(string sectionId)
    {
        _pendingSection = sectionId;
        Navigator.GoTo("settings");
    }

    private readonly AppSettingsService _settings = AppSettingsService.Instance;
    private AppSettingsService _draft = new();
    private Launcher.Core.Services.LibraryPathsService _draftLibraryPaths = new();
    private Launcher.Modules.ImageGen.ImageGenSettings _draftImageGen = new();
    private Launcher.Modules.Models.ModelsSettings _draftModels = new();
    private bool _modelsControlsDirty;
    private bool _draftStartupEnabled;

    // Evita que asignar IsChecked desde codigo dispare los handlers de usuario.
    private bool _loading = true;

    public SettingsView()
    {
        InitializeComponent();
        _draft = CloneObject(_settings);
        _draftLibraryPaths = CloneObject(LibraryPathsService.Instance);
        _draftImageGen = CloneObject(Launcher.Modules.ImageGen.ImageGenSettings.Instance);
        _draftModels = CloneObject(Launcher.Modules.Models.ModelsSettings.Instance);
        _draftStartupEnabled = StartupService.IsEnabled();
        BuildStartModuleChips();
        BuildAccentChips();
        InitializeModelsEditors();
        LoadFromState();
        MarkPending();
        Loaded += SettingsView_Loaded;
    }

    /// <summary>Refleja el estado real (registro + config.json) en los controles.</summary>
    private void LoadFromState()
    {
        // Antes de tocar ningun control: asignar IsChecked dispara Toggle_Changed.
        _loading = true;

        CheckUpdatesToggle.IsChecked = _draft.CheckUpdatesOnStartup;
        AutoApplyUpdatesToggle.IsChecked = _draft.AutoApplyUpdates;
        RefreshUpdateRow();

        var startup = _draftStartupEnabled;
        StartWithWindowsToggle.IsChecked = startup;
        StartMinimizedToggle.IsChecked = _draft.StartMinimized;
        StartMinimizedToggle.IsEnabled = startup;

        AlwaysOnTopToggle.IsChecked = _draft.AlwaysOnTop;
        CloseToTrayToggle.IsChecked = _draft.CloseToTray;
        RememberBoundsToggle.IsChecked = _draft.RememberWindowBounds;
        ConfirmKillToggle.IsChecked = _draft.ConfirmKillProcess;
        SyncOnStartupToggle.IsChecked = _draft.SyncOnStartup;
        ConfirmDeleteToggle.IsChecked = _draft.ConfirmDelete;

        SelectChip(HotkeyPanel, _draft.Hotkey);
        SelectChip(CardSizePanel, _draft.CardSize);
        SelectChip(StartModulePanel, _draft.StartModule);
        SelectChip(AccentPanel, _draft.AccentColor);
        SelectChip(RefreshPanel, _draft.RefreshSeconds.ToString(CultureInfo.InvariantCulture));
        SelectChip(MonitorOverridePanel, _draft.MonitorRefreshSecondsOverride?.ToString(CultureInfo.InvariantCulture) ?? "inherit");
        SelectChip(ProcessesOverridePanel, _draft.ProcessesRefreshSecondsOverride?.ToString(CultureInfo.InvariantCulture) ?? "inherit");
        SelectChip(BackgroundPanel, _draft.BackgroundMode);
        BackgroundOpacitySlider.Value = _draft.BackgroundOpacity;
        BackgroundImageRow.Tag = _draft.BackgroundImagePath ?? "Sin imagen seleccionada";
        MusicFolderBox.Text = _draft.MusicFolder;


        UpdateSyncFoldersSummary();
        UpdateSteamAccountSummary();
        UpdateImageGenSummary();
        UpdateIgnoredSummary();
        UpdateDataSummaries();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AboutRow.Tag = $"v{version?.ToString(3) ?? "0.1.0"}  ·  .NET {Environment.Version.Major}";

        DashboardEffectsToggle.IsChecked = _draft.DashboardEffects;
        BootSequenceToggle.IsChecked = _draft.BootSequence;
        WindowsEventLogToggle.IsChecked = _draft.WindowsEventLog;
        SelectChip(ThresholdPanel, _draft.ThresholdProfile);
        SelectChip(IntervalPanel, _draft.StatusIntervalSeconds.ToString());
        LoadModelsSettingsControls();
        RefreshOverrideTags();

        _loading = false;
    }

    private static void SelectChip(Panel panel, string value)
    {
        foreach (var chip in panel.Children.OfType<RadioButton>())
        {
            chip.IsChecked = string.Equals((string)chip.Tag, value, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Genera un chip por cada modulo real (sin placeholders ni la propia
    /// Configuracion) mas "Ultimo usado".</summary>
    private void BuildStartModuleChips()
    {
        UpdateService.Instance.Changed += (_, _) => RefreshUpdateRow();
        var chipStyle = (Style)FindResource("ChipStyle");

        void AddChip(string label, string id)
        {
            var chip = new RadioButton
            {
                Content = label,
                Tag = id,
                GroupName = "StartModule",
                Style = chipStyle
            };
            chip.Checked += Chip_Checked;
            StartModulePanel.Children.Add(chip);
        }

        AddChip("Ultimo usado", "last");

        foreach (var module in ModuleCatalog.Modules)
        {
            if (module is PlaceholderModule || module.Id == "settings") continue;
            AddChip(module.DisplayName, module.Id);
        }
    }

    /// <summary>Un chip por cada color de la paleta, con una muestra del color.</summary>
    private void BuildAccentChips()
    {
        var chipStyle = (Style)FindResource("ChipStyle");

        foreach (var (name, hex) in ThemeService.AccentPresets)
        {
            ThemeService.TryParseColor(hex, out var color);

            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = new System.Windows.Media.SolidColorBrush(color),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            content.Children.Add(new TextBlock { Text = name });

            var chip = new RadioButton
            {
                Content = content,
                Tag = hex,
                GroupName = "Accent",
                Style = chipStyle
            };
            chip.Checked += Chip_Checked;
            AccentPanel.Children.Add(chip);
        }
    }

    private static T CloneObject<T>(T source) where T : class
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("No se pudo copiar la configuracion.");
    }

    private static void CopyObject<T>(T source, T target) where T : class
    {
        foreach (var property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.CanRead && property.CanWrite)
                property.SetValue(target, property.GetValue(source));
        }
    }

    public bool HasUnsavedChanges => HasPendingChanges();

    public bool ConfirmPendingChanges(string context)
    {
        if (!HasPendingChanges()) return true;

        var result = MessageBox.Show(Window.GetWindow(this),
            $"Hay cambios sin guardar en Configuracion. Quieres guardarlos antes de {context}?\n\n" +
            "Si eliges No, se descartaran. Si eliges Cancelar, continuaras editando.",
            "Cambios pendientes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            SaveChanges_Click(this, new RoutedEventArgs());
            return !HasPendingChanges();
        }

        if (result == MessageBoxResult.No)
        {
            DiscardPendingChanges();
            return true;
        }

        return false;
    }

    private void InitializeModelsEditors()
    {
        ModelsLoraEditor.Initialize("CARPETAS DE LORA", _draftModels.LoraFolders, ModelsFolders_Changed);
        ModelsCheckpointEditor.Initialize("CARPETAS DE CHECKPOINTS", _draftModels.CheckpointFolders, ModelsFolders_Changed);
        ModelsLyCorisEditor.Initialize("CARPETAS DE LYCORIS", _draftModels.LyCorisFolders, ModelsFolders_Changed);
        ModelsWildcardEditor.Initialize("CARPETAS DE WILDCARDS", _draftModels.WildcardFolders, ModelsFolders_Changed);
    }

    private void LoadModelsSettingsControls()
    {
        ModelsInstallPathBox.Text = _draftModels.InstallPath ?? "";
        ModelsMaxDownloadsBox.Text = _draftModels.MaxConcurrentDownloads.ToString(CultureInfo.InvariantCulture);
        ModelsAutoConcurrencyCheck.IsChecked = _draftModels.AutoConcurrency;
        ModelsSpeedLimitBox.Text = _draftModels.SpeedLimitKBps.ToString(CultureInfo.InvariantCulture);
        ModelsRetriesBox.Text = _draftModels.DownloadRetries.ToString(CultureInfo.InvariantCulture);
        ModelsCategoriesBox.Text = string.Join(", ", _draftModels.Categories);
        ModelsApiKeyBox.Clear();
        ModelsClearApiKeyCheck.IsChecked = false;
        ModelsApiKeyStatus.Text = Launcher.Modules.Models.CivitaiService.Instance.HasApiKey
            ? "Hay una clave guardada en el Administrador de credenciales."
            : "No hay una clave guardada.";
        _modelsControlsDirty = false;
    }

    private void ModelsFolders_Changed() => MarkPending();

    private void ModelsControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _modelsControlsDirty = true;
        MarkPending();
    }

    private void ModelsCredential_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        MarkPending();
    }

    private void BrowseModelsInstall_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Selecciona la carpeta de AUTOMATIC1111" };
        if (Directory.Exists(ModelsInstallPathBox.Text)) dialog.InitialDirectory = ModelsInstallPathBox.Text;
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        ModelsInstallPathBox.Text = dialog.FolderName;
        _modelsControlsDirty = true;
        MarkPending();
    }

    private bool TryReadModelsSettingsControls(out string error)
    {
        error = "";
        var install = ModelsInstallPathBox.Text.Trim();
        if (install.Length > 0 && !Directory.Exists(install))
        {
            error = "La carpeta de AUTOMATIC1111 no existe.";
            return false;
        }

        if (!int.TryParse(ModelsMaxDownloadsBox.Text.Trim(), out var max) || max < 1 || max > 8 ||
            !int.TryParse(ModelsSpeedLimitBox.Text.Trim(), out var speed) || speed < 0 ||
            !int.TryParse(ModelsRetriesBox.Text.Trim(), out var retries) || retries < 0 || retries > 10)
        {
            error = "Revisa los numeros: descargas simultaneas entre 1 y 8, limite 0 o mas y reintentos entre 0 y 10.";
            return false;
        }

        var categories = ModelsCategoriesBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (categories.Count == 0)
        {
            error = "Debe haber al menos una categoria de descarga.";
            return false;
        }

        _draftModels.InstallPath = install.Length == 0 ? null : install;
        _draftModels.MaxConcurrentDownloads = max;
        _draftModels.AutoConcurrency = ModelsAutoConcurrencyCheck.IsChecked == true;
        _draftModels.SpeedLimitKBps = speed;
        _draftModels.DownloadRetries = retries;
        _draftModels.Categories = categories;
        _modelsControlsDirty = false;
        return true;
    }

    private bool ModelsControlsHavePendingChanges() =>
        _modelsControlsDirty ||
        !string.IsNullOrEmpty(ModelsApiKeyBox.Password) ||
        ModelsClearApiKeyCheck.IsChecked == true;

    private static string ModelsConfigSignature(Launcher.Modules.Models.ModelsSettings settings) =>
        JsonSerializer.Serialize(new
        {
            settings.InstallPath,
            settings.LoraFolders,
            settings.CheckpointFolders,
            settings.LyCorisFolders,
            settings.WildcardFolders,
            settings.MaxConcurrentDownloads,
            settings.AutoConcurrency,
            settings.SpeedLimitKBps,
            settings.DownloadRetries,
            settings.Categories
        });

    private static void CopyModelsConfig(
        Launcher.Modules.Models.ModelsSettings source,
        Launcher.Modules.Models.ModelsSettings target)
    {
        target.InstallPath = source.InstallPath;
        target.LoraFolders = source.LoraFolders;
        target.CheckpointFolders = source.CheckpointFolders;
        target.LyCorisFolders = source.LyCorisFolders;
        target.WildcardFolders = source.WildcardFolders;
        target.MaxConcurrentDownloads = source.MaxConcurrentDownloads;
        target.AutoConcurrency = source.AutoConcurrency;
        target.SpeedLimitKBps = source.SpeedLimitKBps;
        target.DownloadRetries = source.DownloadRetries;
        target.Categories = source.Categories;
    }

    private static string ImageGenConfigSignature(Launcher.Modules.ImageGen.ImageGenSettings settings) =>
        JsonSerializer.Serialize(new
        {
            settings.Url,
            settings.InstallPath,
            settings.ServerBatPath,
            settings.AutoStartServer,
            settings.WildcardsFolder,
            settings.CustomFolders
        });

    private void SyncRuntimeImageGenState()
    {
        var live = Launcher.Modules.ImageGen.ImageGenSettings.Instance;
        if (string.Equals(_draftImageGen.InstallPath, live.InstallPath, StringComparison.OrdinalIgnoreCase))
        {
            _draftImageGen.CkptDir = live.CkptDir;
            _draftImageGen.LoraDir = live.LoraDir;
            _draftImageGen.VaeDir = live.VaeDir;
            _draftImageGen.EmbeddingsDir = live.EmbeddingsDir;
            _draftImageGen.ControlNetDir = live.ControlNetDir;
        }

        _draftImageGen.LastParams = live.LastParams;
    }

    private static void CopyImageGenSettings(
        Launcher.Modules.ImageGen.ImageGenSettings source,
        Launcher.Modules.ImageGen.ImageGenSettings target)
    {
        target.Url = source.Url;
        target.InstallPath = source.InstallPath;
        target.ServerBatPath = source.ServerBatPath;
        target.AutoStartServer = source.AutoStartServer;
        target.WildcardsFolder = source.WildcardsFolder;
        target.CustomFolders = source.CustomFolders;
        target.CkptDir = source.CkptDir;
        target.LoraDir = source.LoraDir;
        target.VaeDir = source.VaeDir;
        target.EmbeddingsDir = source.EmbeddingsDir;
        target.ControlNetDir = source.ControlNetDir;
        target.LastParams = source.LastParams;
    }

    private bool HasPendingChanges()
    {
        return !string.Equals(JsonSerializer.Serialize(_draft), JsonSerializer.Serialize(_settings), StringComparison.Ordinal) ||
               !string.Equals(JsonSerializer.Serialize(_draftLibraryPaths), JsonSerializer.Serialize(LibraryPathsService.Instance), StringComparison.Ordinal) ||
               !string.Equals(ImageGenConfigSignature(_draftImageGen), ImageGenConfigSignature(Launcher.Modules.ImageGen.ImageGenSettings.Instance), StringComparison.Ordinal) ||
               !string.Equals(ModelsConfigSignature(_draftModels), ModelsConfigSignature(Launcher.Modules.Models.ModelsSettings.Instance), StringComparison.Ordinal) ||
               ModelsControlsHavePendingChanges() ||
               _draftStartupEnabled != StartupService.IsEnabled();
    }

    private void RefreshOverrideTags()
    {
        if (MonitorOverrideRow == null || ProcessesOverrideRow == null) return;

        MonitorOverrideRow.Tag = _draft.MonitorRefreshSecondsOverride is double monitor
            ? $"SOBRESCRIBE GLOBAL · Monitor usa {FormatOverrideValue(monitor)} y no sigue el valor compartido."
            : "HEREDA GLOBAL · Monitor utiliza el intervalo COMPARTIDO.";

        ProcessesOverrideRow.Tag = _draft.ProcessesRefreshSecondsOverride is double processes
            ? $"SOBRESCRIBE GLOBAL · Procesos usa {FormatOverrideValue(processes)} y no sigue el valor compartido."
            : "HEREDA GLOBAL · Procesos utiliza el intervalo COMPARTIDO.";
    }

    private static string FormatOverrideValue(double seconds) =>
        seconds <= 0 ? "Auto" : seconds.ToString("0.##", CultureInfo.InvariantCulture) + " s";

    private void MarkPending()
    {
        var pending = HasPendingChanges();
        PendingStatusText.Text = pending ? "Hay cambios sin guardar" : "Sin cambios pendientes";
        PendingStatusText.Foreground = (Brush)FindResource(pending ? "Brush.Accent" : "Brush.TextDim");
        SaveChangesButton.IsEnabled = pending;
        DiscardChangesButton.IsEnabled = pending;
        RefreshOverrideTags();
    }

    private void DiscardChanges_Click(object sender, RoutedEventArgs e)
    {
        if (!HasPendingChanges()) return;

        var result = MessageBox.Show(Window.GetWindow(this),
            "Se perderan todos los cambios pendientes. Continuar?",
            "Descartar cambios", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        DiscardPendingChanges();
    }

    private void DiscardPendingChanges()
    {
        _draft = CloneObject(_settings);
        _draftLibraryPaths = CloneObject(LibraryPathsService.Instance);
        _draftImageGen = CloneObject(Launcher.Modules.ImageGen.ImageGenSettings.Instance);
        _draftModels = CloneObject(Launcher.Modules.Models.ModelsSettings.Instance);
        InitializeModelsEditors();
        _draftStartupEnabled = StartupService.IsEnabled();
        LoadFromState();
        MarkPending();
    }

    private void SaveChanges_Click(object sender, RoutedEventArgs e)
    {
        if (!HasPendingChanges()) return;

        if (!TryReadModelsSettingsControls(out var modelsError))
        {
            MessageBox.Show(Window.GetWindow(this), modelsError, "Configuracion de Models",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_draft.MusicFolder) && !Directory.Exists(_draft.MusicFolder))
        {
            MessageBox.Show(Window.GetWindow(this), "La carpeta de musica no existe.",
                "Configuracion de Musica", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_draftImageGen.CustomFolders.TryGetValue("Gallery", out var galleryFolder) &&
            !string.IsNullOrWhiteSpace(galleryFolder) && !Directory.Exists(galleryFolder))
        {
            try
            {
                Directory.CreateDirectory(galleryFolder);
            }
            catch (Exception ex)
            {
                LogService.Error("No se pudo crear la carpeta de galeria de ImageGen", ex);
                MessageBox.Show(Window.GetWindow(this), "No se ha podido crear la carpeta de la galeria de Imagenes IA.",
                    "Imagenes IA", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (!string.IsNullOrEmpty(ModelsApiKeyBox.Password) && ModelsClearApiKeyCheck.IsChecked == true)
        {
            MessageBox.Show(Window.GetWindow(this), "No puedes guardar una clave nueva y eliminarla en la misma operacion.",
                "Civitai", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var previousModels = CloneObject(Launcher.Modules.Models.ModelsSettings.Instance);
        var previousApp = CloneObject(_settings);
        var previousLibraries = CloneObject(LibraryPathsService.Instance);
        var previousImageGen = CloneObject(Launcher.Modules.ImageGen.ImageGenSettings.Instance);
        var previousStartup = StartupService.IsEnabled();

        CopyObject(_draft, _settings);
        CopyObject(_draftLibraryPaths, LibraryPathsService.Instance);
        SyncRuntimeImageGenState();
        CopyImageGenSettings(_draftImageGen, Launcher.Modules.ImageGen.ImageGenSettings.Instance);
        CopyModelsConfig(_draftModels, Launcher.Modules.Models.ModelsSettings.Instance);

        if (!_settings.TrySave(notifyChanged: false) ||
            !LibraryPathsService.Instance.TrySave() ||
            !Launcher.Modules.ImageGen.ImageGenSettings.Instance.TrySave() ||
            !Launcher.Modules.Models.ModelsSettings.Instance.TrySave())
        {
            RestorePreviousConfiguration(previousApp, previousLibraries, previousImageGen, previousModels, previousStartup);
            MessageBox.Show(Window.GetWindow(this),
                "No se ha podido guardar uno de los archivos de configuracion. Los cambios siguen pendientes.",
                "Guardar configuracion", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var hotkeyOk = WindowBehaviorService.Instance?.ApplyHotkey() ?? true;
        if (!hotkeyOk)
        {
            RestorePreviousConfiguration(previousApp, previousLibraries, previousImageGen, previousModels, previousStartup);
            MessageBox.Show(Window.GetWindow(this),
                "Ese atajo ya lo esta usando otra aplicacion. Se han restaurado los valores guardados.",
                "Atajo global", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!StartupService.SetEnabled(_draftStartupEnabled, _draft.StartMinimized))
        {
            RestorePreviousConfiguration(previousApp, previousLibraries, previousImageGen, previousModels, previousStartup);
            MessageBox.Show(Window.GetWindow(this),
                "No se ha podido modificar el inicio con Windows. Los cambios siguen pendientes.",
                "Guardar configuracion", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var apiKey = ModelsApiKeyBox.Password.Trim();
        if ((apiKey.Length > 0 || ModelsClearApiKeyCheck.IsChecked == true) &&
            !Launcher.Modules.Models.CivitaiService.Instance.SaveApiKey(ModelsClearApiKeyCheck.IsChecked == true ? "" : apiKey))
        {
            RestorePreviousConfiguration(previousApp, previousLibraries, previousImageGen, previousModels, previousStartup);
            MessageBox.Show(Window.GetWindow(this),
                "No se ha podido guardar la credencial de Civitai en el Administrador de credenciales de Windows.",
                "Civitai", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _settings.NotifyChanged();
        Launcher.Modules.ImageGen.ImageGenSettings.Instance.NotifyChanged();
        _settings.ApplyToWindow(Window.GetWindow(this)!);
        WindowBehaviorService.Instance?.ApplyTray();
        ThemeService.ApplyAccent(_settings.AccentColor);

        _draft = CloneObject(_settings);
        _draftLibraryPaths = CloneObject(LibraryPathsService.Instance);
        _draftImageGen = CloneObject(Launcher.Modules.ImageGen.ImageGenSettings.Instance);
        _draftModels = CloneObject(Launcher.Modules.Models.ModelsSettings.Instance);
        InitializeModelsEditors();
        _draftStartupEnabled = StartupService.IsEnabled();
        _modelsControlsDirty = false;
        LoadFromState();
        _ = RefreshModelsAfterSettingsAsync();
        MarkPending();
    }

    private void RestorePreviousConfiguration(
        AppSettingsService previousApp,
        LibraryPathsService previousLibraries,
        Launcher.Modules.ImageGen.ImageGenSettings previousImageGen,
        Launcher.Modules.Models.ModelsSettings previousModels,
        bool previousStartup)
    {
        CopyObject(previousApp, _settings);
        CopyObject(previousLibraries, LibraryPathsService.Instance);
        CopyImageGenSettings(previousImageGen, Launcher.Modules.ImageGen.ImageGenSettings.Instance);
        CopyModelsConfig(previousModels, Launcher.Modules.Models.ModelsSettings.Instance);

        _settings.TrySave(notifyChanged: false);
        LibraryPathsService.Instance.TrySave();
        Launcher.Modules.ImageGen.ImageGenSettings.Instance.TrySave();
        Launcher.Modules.Models.ModelsSettings.Instance.TrySave();
        StartupService.SetEnabled(previousStartup, previousApp.StartMinimized);

        WindowBehaviorService.Instance?.ApplyHotkey();
        _settings.ApplyToWindow(Window.GetWindow(this)!);
        WindowBehaviorService.Instance?.ApplyTray();
        ThemeService.ApplyAccent(_settings.AccentColor);
    }

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateActiveCategory();
        if (string.IsNullOrWhiteSpace(_pendingSection)) return;

        var section = _pendingSection;
        _pendingSection = null;
        ScrollToSection(section);
    }

    private void NavigateSection_Click(object sender, RoutedEventArgs e) =>
        ScrollToSection((string?)((Button)sender).Tag ?? "");

    private void ScrollToSection(string id)
    {
        FrameworkElement? target = id switch
        {
            "startup" => StartupSection,
            "window" => WindowSection,
            "monitor" => MonitorSection,
            "background" => BackgroundSection,
            "games" => GamesSection,
            "music" => MusicSection,
            "imagegen" => ImageGenSection,
            "models" => ModelsSection,
            "updates" => UpdatesSection,
            "dashboard" => DashboardSection,
            "data" => DataSection,
            "about" => AboutSection,
            _ => null
        };

        target?.BringIntoView();
        UpdateActiveCategory();
    }

    private void SettingsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) =>
        UpdateActiveCategory();

    private void UpdateActiveCategory()
    {
        if (!IsLoaded) return;

        var sections = new (string Id, FrameworkElement Element)[]
        {
            ("startup", StartupSection), ("window", WindowSection), ("monitor", MonitorSection),
            ("background", BackgroundSection), ("games", GamesSection), ("music", MusicSection), ("imagegen", ImageGenSection),
            ("models", ModelsSection), ("updates", UpdatesSection), ("dashboard", DashboardSection), ("data", DataSection),
            ("about", AboutSection)
        };

        var active = sections[0].Id;

        if (SettingsScrollViewer.ScrollableHeight > 0 &&
            SettingsScrollViewer.VerticalOffset >= SettingsScrollViewer.ScrollableHeight - 2)
        {
            active = sections[^1].Id;
        }
        else
        {
            var nearestTop = double.NegativeInfinity;

            foreach (var section in sections)
        {
            try
            {
                var y = section.Element.TransformToAncestor(SettingsScrollViewer).Transform(new Point(0, 0)).Y;
                if (y <= 130 && y > nearestTop)
                {
                    nearestTop = y;
                    active = section.Id;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                // El elemento aun no esta conectado al visor.
            }
            }
        }

        foreach (var button in FindCategoryButtons())
        {
            var selected = string.Equals((string?)button.Tag, active, StringComparison.Ordinal);
            button.Background = selected
                ? (Brush)FindResource("Brush.AccentDim")
                : Brushes.Transparent;
            button.Foreground = selected
                ? (Brush)FindResource("Brush.Accent")
                : (Brush)FindResource("Brush.TextSecondary");
        }
    }

    private IEnumerable<Button> FindCategoryButtons()
    {
        if (CategoryPanel == null) yield break;
        foreach (var button in CategoryPanel.Children.OfType<Button>())
            yield return button;
    }

    // ===================== Interruptores =====================

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var on = ((ToggleButton)sender).IsChecked == true;

        if (sender == StartWithWindowsToggle)
        {
            _draftStartupEnabled = on;
            StartMinimizedToggle.IsEnabled = on;
        }
        else if (sender == StartMinimizedToggle) _draft.StartMinimized = on;
        else if (sender == AlwaysOnTopToggle) _draft.AlwaysOnTop = on;
        else if (sender == CloseToTrayToggle) _draft.CloseToTray = on;
        else if (sender == RememberBoundsToggle) _draft.RememberWindowBounds = on;
        else if (sender == ConfirmKillToggle) _draft.ConfirmKillProcess = on;
        else if (sender == SyncOnStartupToggle) _draft.SyncOnStartup = on;
        else if (sender == ConfirmDeleteToggle) _draft.ConfirmDelete = on;
        else if (sender == CheckUpdatesToggle) _draft.CheckUpdatesOnStartup = on;
        else if (sender == AutoApplyUpdatesToggle) _draft.AutoApplyUpdates = on;
        else if (sender == DashboardEffectsToggle) _draft.DashboardEffects = on;
        else if (sender == BootSequenceToggle) _draft.BootSequence = on;
        else if (sender == WindowsEventLogToggle) _draft.WindowsEventLog = on;

        MarkPending();
    }

        // ===================== Chips (atajo, tamano de tarjetas, modulo, acento, refresco) =====================

    private void Chip_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var chip = (RadioButton)sender;
        var value = (string)chip.Tag;

        if (chip.GroupName == "Hotkey") _draft.Hotkey = value;
        else if (chip.GroupName == "CardSize") _draft.CardSize = value;
        else if (chip.GroupName == "StartModule") _draft.StartModule = value;
        else if (chip.GroupName == "Accent") _draft.AccentColor = value;
        else if (chip.GroupName == "Refresh" &&
                 double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            _draft.RefreshSeconds = seconds;
        else if (chip.GroupName == "MonitorRefreshOverride")
            _draft.MonitorRefreshSecondsOverride = value == "inherit"
                ? null
                : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var monitorSeconds)
                    ? monitorSeconds
                    : _draft.MonitorRefreshSecondsOverride;
        else if (chip.GroupName == "ProcessesRefreshOverride")
            _draft.ProcessesRefreshSecondsOverride = value == "inherit"
                ? null
                : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var processSeconds)
                    ? processSeconds
                    : _draft.ProcessesRefreshSecondsOverride;
        else if (chip.GroupName == "ThresholdProfile") _draft.ThresholdProfile = value;
        else if (chip.GroupName == "StatusInterval" && int.TryParse(value, out var interval))
            _draft.StatusIntervalSeconds = interval;
        else if (chip.GroupName == "Background") _draft.BackgroundMode = value;

        MarkPending();
    }

        // ===================== Juegos =====================

    private void ConfigureFolders_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConfigureSyncFoldersWindow(_draftLibraryPaths, deferSave: true)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
        UpdateSyncFoldersSummary();
        MarkPending();
    }

    private void UpdateIgnoredSummary()
    {
        var count = GameLibraryService.Instance.IgnoredCount;
        IgnoredRow.Tag = count == 0
            ? "Ninguno. Los juegos que elimines de Steam/Epic no vuelven a aparecer al sincronizar."
            : $"{count} juego(s) ignorado(s) en la sincronizacion. Restaurar los hara reaparecer en la proxima.";
    }

    private void RestoreIgnored_Click(object sender, RoutedEventArgs e)
    {
        var library = GameLibraryService.Instance;
        var count = library.IgnoredCount;

        if (count == 0)
        {
            MessageBox.Show(Window.GetWindow(this), "No hay juegos eliminados que restaurar.",
                "Juegos eliminados", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        library.ClearIgnored();
        UpdateIgnoredSummary();

        MessageBox.Show(Window.GetWindow(this),
            $"Se restauraran {count} juego(s) en la proxima sincronizacion.",
            "Juegos eliminados", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenFolder(LogService.LogsFolder);

    private void UpdateSyncFoldersSummary()
    {
        var paths = _draftLibraryPaths;
        var steam = paths.HasSteamPath ? "configurada" : "sin configurar";
        var epic = paths.HasEpicPath ? "configurada" : "sin configurar";
        SyncFoldersRow.Tag = $"Steam: {steam}  ·  Epic: {epic}";
    }

    private void OpenCoversFolder_Click(object sender, RoutedEventArgs e) =>
        OpenFolder(CoverArtService.CoversFolder);

    private async void RepairCovers_Click(object sender, RoutedEventArgs e)
    {
        RepairCoversButton.IsEnabled = false;
        RepairCoversButton.Content = "Reparando...";

        try
        {
            var repaired = await GameLibraryService.Instance.RepairMissingCoversAsync();

            MessageBox.Show(Window.GetWindow(this),
                repaired > 0
                    ? $"Se han recuperado {repaired} portada(s)."
                    : "No habia portadas que reparar, o no se han encontrado resultados.",
                "Portadas", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch
        {
            MessageBox.Show(Window.GetWindow(this),
                "No se han podido reparar las portadas. Comprueba tu conexion.",
                "Portadas", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            RepairCoversButton.IsEnabled = true;
            RepairCoversButton.Content = "Reparar faltantes";
            UpdateDataSummaries();
        }
    }

    // ===================== Datos =====================

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e) =>
        OpenFolder(AppSettingsService.AppFolder);

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(Window.GetWindow(this),
            "Se prepararan los valores por defecto de config.json y se desactivara el inicio con Windows. Los cambios no se aplicaran hasta pulsar Guardar.",
            "Restablecer ajustes", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var modelsInput = new
        {
            InstallPath = ModelsInstallPathBox.Text,
            MaxDownloads = ModelsMaxDownloadsBox.Text,
            AutoConcurrency = ModelsAutoConcurrencyCheck.IsChecked,
            SpeedLimit = ModelsSpeedLimitBox.Text,
            Retries = ModelsRetriesBox.Text,
            Categories = ModelsCategoriesBox.Text,
            ApiKey = ModelsApiKeyBox.Password,
            ClearApiKey = ModelsClearApiKeyCheck.IsChecked
        };
        var modelsControlsDirty = _modelsControlsDirty;

        _draft = new AppSettingsService();
        _draftStartupEnabled = false;
        LoadFromState();

        ModelsInstallPathBox.Text = modelsInput.InstallPath;
        ModelsMaxDownloadsBox.Text = modelsInput.MaxDownloads;
        ModelsAutoConcurrencyCheck.IsChecked = modelsInput.AutoConcurrency;
        ModelsSpeedLimitBox.Text = modelsInput.SpeedLimit;
        ModelsRetriesBox.Text = modelsInput.Retries;
        ModelsCategoriesBox.Text = modelsInput.Categories;
        ModelsApiKeyBox.Password = modelsInput.ApiKey;
        ModelsClearApiKeyCheck.IsChecked = modelsInput.ClearApiKey;
        _modelsControlsDirty = modelsControlsDirty;
        MarkPending();
    }

    private void UpdateDataSummaries()
    {
        var covers = FormatSize(GetCoversSize());
        DataRow.Tag = AppSettingsService.AppFolder;
        CoversRow.Tag = $"Cache de portadas: {covers}";
    }

    private void ConfigureSteamAccount_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SteamAccountWindow(_draftLibraryPaths, deferSave: true)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
        UpdateSteamAccountSummary();
        MarkPending();
    }

    private void UpdateSteamAccountSummary()
    {
        var paths = _draftLibraryPaths;
        SteamAccountRow.Tag = paths.HasSteamApiKey
            ? "Clave configurada. Ya puedes ver los logros de Steam en la ficha de cada juego."
            : "Sin configurar. Hace falta una clave gratuita de la Web API de Steam para ver tus logros.";
    }

    // ===================== Utilidades =====================

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch
        {
            // Si el explorador no se puede abrir no hay mucho mas que hacer.
        }
    }

    private static long GetCoversSize()
    {
        try
        {
            if (!Directory.Exists(CoverArtService.CoversFolder)) return 0;
            return Directory.EnumerateFiles(CoverArtService.CoversFolder)
                .Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        var i = 0;
        while (size >= 1024 && i < units.Length - 1)
        {
            size /= 1024;
            i++;
        }
        return $"{size:0.#} {units[i]}";
    }

    private void MusicFolder_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        _draft.MusicFolder = MusicFolderBox.Text.Trim();
        MarkPending();
    }

    private void BrowseMusicFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Selecciona la carpeta de la biblioteca musical",
            SelectedPath = _draft.MusicFolder
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;
        MusicFolderBox.Text = dialog.SelectedPath;
    }

    private void ConfigureImageGen_Click(object sender, RoutedEventArgs e)
    {
        new Launcher.Modules.ImageGen.ImageGenSettingsWindow(_draftImageGen, deferSave: true)
        {
            Owner = Window.GetWindow(this)
        }.ShowDialog();
        UpdateImageGenSummary();
        MarkPending();
    }

    private void UpdateImageGenSummary()
    {
        var s = _draftImageGen;
        ImageGenRow.Tag = string.IsNullOrWhiteSpace(s.InstallPath) ? "Sin instalacion configurada." : s.InstallPath;
    }

    // ===================== Actualizaciones =====================

    private void RefreshUpdateRow()
    {
        var update = UpdateService.Instance;
        var current = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";

        UpdateRow.Tag = update.Status switch
        {
            UpdateStatus.Available => $"Tienes v{current}. Hay una nueva: v{update.AvailableVersion}.",
            UpdateStatus.UpToDate => $"v{current}. Estas al dia.",
            UpdateStatus.NotInstalled => $"v{current}. Esta copia no esta instalada con el instalador, asi que no se puede actualizar desde aqui.",
            UpdateStatus.NotConfigured => $"v{current}. Indica la carpeta de versiones.",
            UpdateStatus.Error => $"v{current}. No se pudo comprobar: {update.ErrorMessage}",
            _ => $"v{current}. Sin comprobar todavia."
        };

        ApplyUpdateButton.Visibility = update.Status == UpdateStatus.Available ? Visibility.Visible : Visibility.Collapsed;
        ApplyUpdateButton.IsEnabled = !update.IsBusy;
        CheckUpdateButton.IsEnabled = !update.IsBusy;
        UpdateSourceRow.Tag = _draft.UpdateSource;
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e) =>
        await UpdateService.Instance.CheckAsync();

    private async void ApplyUpdate_Click(object sender, RoutedEventArgs e) =>
        await UpdateService.Instance.ApplyAsync();

    private void ChangeUpdateSource_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Selecciona la carpeta donde se publican las versiones (la salida de publish.ps1)",
            SelectedPath = _draft.UpdateSource
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;

        _draft.UpdateSource = dialog.SelectedPath;
        MarkPending();
        RefreshUpdateRow();
    }

    private void ChooseBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecciona la imagen de fondo",
            Filter = "Imagenes (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp"
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        _draft.BackgroundImagePath = dialog.FileName;
        _draft.BackgroundMode = "image";
        _loading = true;
        SelectChip(BackgroundPanel, _draft.BackgroundMode);
        BackgroundImageRow.Tag = _draft.BackgroundImagePath;
        _loading = false;
        MarkPending();
    }

    private void BackgroundOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _draft.BackgroundOpacity = e.NewValue;
        MarkPending();
    }
    private async Task RefreshModelsAfterSettingsAsync()
    {
        try
        {
            Launcher.Modules.Models.DownloadQueueService.Instance.ApplySettings();
            await Launcher.Modules.Models.ModelCatalogService.Instance.ScanAsync();
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo actualizar Models tras guardar su configuracion", ex);
        }
    }

    private async void TestCivitai_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ModelsApiKeyBox.Password) || ModelsClearApiKeyCheck.IsChecked == true)
        {
            MessageBox.Show(Window.GetWindow(this), "Guarda o descarta primero la nueva credencial antes de probar la conexion.",
                "Civitai", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ModelsApiKeyStatus.Text = "Probando conexion...";
        try
        {
            var (ok, message) = await Launcher.Modules.Models.CivitaiService.Instance.TestConnectionAsync();
            Launcher.Modules.Models.ModelsStats.SetConnection(ok, message);
            ModelsApiKeyStatus.Text = message;
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la prueba de conexion con Civitai", ex);
            ModelsApiKeyStatus.Text = "No se pudo probar la conexion.";
        }
    }

}
