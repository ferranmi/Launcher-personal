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
    private readonly AppSettingsService _settings = AppSettingsService.Instance;
    private AppSettingsService _draft = new();
    private Launcher.Core.Services.LibraryPathsService _draftLibraryPaths = new();
    private Launcher.Modules.ImageGen.ImageGenSettings _draftImageGen = new();
    private bool _draftStartupEnabled;

    // Evita que asignar IsChecked desde codigo dispare los handlers de usuario.
    private bool _loading = true;

    public SettingsView()
    {
        InitializeComponent();
        _draft = CloneObject(_settings);
        _draftLibraryPaths = CloneObject(LibraryPathsService.Instance);
        _draftImageGen = CloneObject(Launcher.Modules.ImageGen.ImageGenSettings.Instance);
        _draftStartupEnabled = StartupService.IsEnabled();
        BuildStartModuleChips();
        BuildAccentChips();
        LoadFromState();
        MarkPending();
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
        SelectChip(BackgroundPanel, _draft.BackgroundMode);
        BackgroundOpacitySlider.Value = _draft.BackgroundOpacity;
        BackgroundImageRow.Tag = _draft.BackgroundImagePath ?? "Sin imagen seleccionada";


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

    private bool HasPendingChanges()
    {
        return !string.Equals(JsonSerializer.Serialize(_draft), JsonSerializer.Serialize(_settings), StringComparison.Ordinal) ||
               !string.Equals(JsonSerializer.Serialize(_draftLibraryPaths), JsonSerializer.Serialize(LibraryPathsService.Instance), StringComparison.Ordinal) ||
               !string.Equals(JsonSerializer.Serialize(_draftImageGen), JsonSerializer.Serialize(Launcher.Modules.ImageGen.ImageGenSettings.Instance), StringComparison.Ordinal) ||
               _draftStartupEnabled != StartupService.IsEnabled();
    }

    private void MarkPending()
    {
        var pending = HasPendingChanges();
        PendingStatusText.Text = pending ? "Hay cambios sin guardar" : "Sin cambios pendientes";
        PendingStatusText.Foreground = (Brush)FindResource(pending ? "Brush.Accent" : "Brush.TextDim");
        SaveChangesButton.IsEnabled = pending;
        DiscardChangesButton.IsEnabled = pending;
    }

    private void DiscardChanges_Click(object sender, RoutedEventArgs e)
    {
        if (HasPendingChanges())
        {
            var result = MessageBox.Show(Window.GetWindow(this),
                "Se perderan todos los cambios pendientes. Continuar?",
                "Descartar cambios", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }

        _draft = CloneObject(_settings);
        _draftLibraryPaths = CloneObject(LibraryPathsService.Instance);
        _draftImageGen = CloneObject(Launcher.Modules.ImageGen.ImageGenSettings.Instance);
        _draftStartupEnabled = StartupService.IsEnabled();
        LoadFromState();
        MarkPending();
    }

    private void SaveChanges_Click(object sender, RoutedEventArgs e)
    {
        if (!HasPendingChanges()) return;

        var previousApp = CloneObject(_settings);
        var previousLibraries = CloneObject(LibraryPathsService.Instance);
        var previousImageGen = CloneObject(Launcher.Modules.ImageGen.ImageGenSettings.Instance);
        var previousStartup = StartupService.IsEnabled();

        CopyObject(_draft, _settings);
        CopyObject(_draftLibraryPaths, LibraryPathsService.Instance);
        CopyObject(_draftImageGen, Launcher.Modules.ImageGen.ImageGenSettings.Instance);

        if (!_settings.TrySave(notifyChanged: false) ||
            !LibraryPathsService.Instance.TrySave() ||
            !Launcher.Modules.ImageGen.ImageGenSettings.Instance.TrySave())
        {
            RestorePreviousConfiguration(previousApp, previousLibraries, previousImageGen, previousStartup);
            MessageBox.Show(Window.GetWindow(this),
                "No se ha podido guardar uno de los archivos de configuracion. Los cambios siguen pendientes.",
                "Guardar configuracion", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var hotkeyOk = WindowBehaviorService.Instance?.ApplyHotkey() ?? true;
        if (!hotkeyOk)
        {
            RestorePreviousConfiguration(previousApp, previousLibraries, previousImageGen, previousStartup);
            MessageBox.Show(Window.GetWindow(this),
                "Ese atajo ya lo esta usando otra aplicacion. Se han restaurado los valores guardados.",
                "Atajo global", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!StartupService.SetEnabled(_draftStartupEnabled, _draft.StartMinimized))
        {
            RestorePreviousConfiguration(previousApp, previousLibraries, previousImageGen, previousStartup);
            MessageBox.Show(Window.GetWindow(this),
                "No se ha podido modificar el inicio con Windows. Los cambios siguen pendientes.",
                "Guardar configuracion", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _settings.NotifyChanged();
        _settings.ApplyToWindow(Window.GetWindow(this)!);
        WindowBehaviorService.Instance?.ApplyTray();
        ThemeService.ApplyAccent(_settings.AccentColor);

        _draft = CloneObject(_settings);
        _draftLibraryPaths = CloneObject(LibraryPathsService.Instance);
        _draftImageGen = CloneObject(Launcher.Modules.ImageGen.ImageGenSettings.Instance);
        _draftStartupEnabled = StartupService.IsEnabled();
        LoadFromState();
        MarkPending();
    }

    private void RestorePreviousConfiguration(
        AppSettingsService previousApp,
        LibraryPathsService previousLibraries,
        Launcher.Modules.ImageGen.ImageGenSettings previousImageGen,
        bool previousStartup)
    {
        CopyObject(previousApp, _settings);
        CopyObject(previousLibraries, LibraryPathsService.Instance);
        CopyObject(previousImageGen, Launcher.Modules.ImageGen.ImageGenSettings.Instance);

        _settings.TrySave(notifyChanged: false);
        LibraryPathsService.Instance.TrySave();
        Launcher.Modules.ImageGen.ImageGenSettings.Instance.TrySave();
        StartupService.SetEnabled(previousStartup, previousApp.StartMinimized);

        WindowBehaviorService.Instance?.ApplyHotkey();
        _settings.ApplyToWindow(Window.GetWindow(this)!);
        WindowBehaviorService.Instance?.ApplyTray();
        ThemeService.ApplyAccent(_settings.AccentColor);
    }

    private void NavigateSection_Click(object sender, RoutedEventArgs e)
    {
        var id = (string?)((Button)sender).Tag;
        FrameworkElement? target = id switch
        {
            "startup" => StartupSection,
            "window" => WindowSection,
            "monitor" => MonitorSection,
            "background" => BackgroundSection,
            "games" => GamesSection,
            "imagegen" => ImageGenSection,
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
            ("background", BackgroundSection), ("games", GamesSection), ("imagegen", ImageGenSection),
            ("updates", UpdatesSection), ("dashboard", DashboardSection), ("data", DataSection),
            ("about", AboutSection)
        };

        var active = sections[0].Id;
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
            catch (InvalidOperationException)
            {
                // El elemento aun no esta conectado al visor.
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
            "Se prepararan los valores por defecto. Los cambios no se aplicaran hasta pulsar Guardar.",
            "Restablecer ajustes", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        _draft = new AppSettingsService();
        _draftStartupEnabled = false;
        LoadFromState();
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
        LoadFromState();
        MarkPending();
    }

    private void BackgroundOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _draft.BackgroundOpacity = e.NewValue;
        MarkPending();
    }
}
