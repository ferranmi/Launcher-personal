using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
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

    // Evita que asignar IsChecked desde codigo dispare los handlers de usuario.
    private bool _loading = true;

    public SettingsView()
    {
        InitializeComponent();
        BuildStartModuleChips();
        BuildAccentChips();
        LoadFromState();
    }

    /// <summary>Refleja el estado real (registro + config.json) en los controles.</summary>
    private void LoadFromState()
    {
        // Antes de tocar ningun control: asignar IsChecked dispara Toggle_Changed.
        _loading = true;

        CheckUpdatesToggle.IsChecked = _settings.CheckUpdatesOnStartup;
        RefreshUpdateRow();

        var startup = StartupService.IsEnabled();
        StartWithWindowsToggle.IsChecked = startup;
        StartMinimizedToggle.IsChecked = _settings.StartMinimized;
        StartMinimizedToggle.IsEnabled = startup;

        AlwaysOnTopToggle.IsChecked = _settings.AlwaysOnTop;
        CloseToTrayToggle.IsChecked = _settings.CloseToTray;
        RememberBoundsToggle.IsChecked = _settings.RememberWindowBounds;
        ConfirmKillToggle.IsChecked = _settings.ConfirmKillProcess;
        SyncOnStartupToggle.IsChecked = _settings.SyncOnStartup;
        ConfirmDeleteToggle.IsChecked = _settings.ConfirmDelete;

        SelectChip(HotkeyPanel, _settings.Hotkey);
        SelectChip(CardSizePanel, _settings.CardSize);
        SelectChip(StartModulePanel, _settings.StartModule);
        SelectChip(AccentPanel, _settings.AccentColor);
        SelectChip(RefreshPanel, _settings.RefreshSeconds.ToString(CultureInfo.InvariantCulture));

        UpdateSyncFoldersSummary();
        UpdateIgnoredSummary();
        UpdateDataSummaries();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AboutRow.Tag = $"v{version?.ToString(3) ?? "0.1.0"}  ·  .NET {Environment.Version.Major}";

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

    // ===================== Interruptores =====================

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var on = ((ToggleButton)sender).IsChecked == true;

        if (sender == StartWithWindowsToggle)
        {
            if (!StartupService.SetEnabled(on, _settings.StartMinimized))
            {
                MessageBox.Show(Window.GetWindow(this),
                    "No se ha podido modificar el arranque con Windows.",
                    "Configuracion", MessageBoxButton.OK, MessageBoxImage.Warning);
                LoadFromState(); // volver al estado real
                return;
            }

            StartMinimizedToggle.IsEnabled = on;
            return; // vive en el registro, no en config.json
        }

        if (sender == StartMinimizedToggle)
        {
            _settings.StartMinimized = on;

            // El argumento --minimized vive en el comando del registro: reescribirlo.
            if (StartupService.IsEnabled()) StartupService.SetEnabled(true, on);
        }
        else if (sender == AlwaysOnTopToggle)
        {
            _settings.AlwaysOnTop = on;
            var window = Window.GetWindow(this);
            if (window != null) _settings.ApplyToWindow(window);
        }
        else if (sender == CloseToTrayToggle)
        {
            _settings.CloseToTray = on;
            _settings.Save();
            WindowBehaviorService.Instance?.ApplyTray();
            return;
        }
        else if (sender == RememberBoundsToggle)
        {
            _settings.RememberWindowBounds = on;
        }
        else if (sender == ConfirmKillToggle)
        {
            _settings.ConfirmKillProcess = on;
        }
        else if (sender == SyncOnStartupToggle)
        {
            _settings.SyncOnStartup = on;
        }
        else if (sender == ConfirmDeleteToggle)
        {
            _settings.ConfirmDelete = on;
        }
        else if (sender == CheckUpdatesToggle)
        {
            _settings.CheckUpdatesOnStartup = on;
        }

        _settings.Save();
    }

    // ===================== Chips (atajo, tamano de tarjetas, modulo, acento, refresco) =====================

    private void Chip_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var chip = (RadioButton)sender;
        var value = (string)chip.Tag;

        if (chip.GroupName == "Hotkey")
        {
            var previous = _settings.Hotkey;
            _settings.Hotkey = value;

            var ok = WindowBehaviorService.Instance?.ApplyHotkey() ?? true;
            if (!ok)
            {
                MessageBox.Show(Window.GetWindow(this),
                    "Ese atajo ya lo esta usando otra aplicacion. Prueba con otro.",
                    "Atajo global", MessageBoxButton.OK, MessageBoxImage.Warning);

                _settings.Hotkey = previous;
                WindowBehaviorService.Instance?.ApplyHotkey();
                LoadFromState();
                return;
            }
        }
        else if (chip.GroupName == "CardSize")
        {
            _settings.CardSize = value;
        }
        else if (chip.GroupName == "StartModule")
        {
            _settings.StartModule = value;
        }
        else if (chip.GroupName == "Accent")
        {
            _settings.AccentColor = value;
            ThemeService.ApplyAccent(value);
        }
        else if (chip.GroupName == "Refresh")
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                _settings.RefreshSeconds = seconds;
            }
        }

        _settings.Save();
    }

    // ===================== Juegos =====================

    private void ConfigureFolders_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConfigureSyncFoldersWindow { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        UpdateSyncFoldersSummary();
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
        var paths = LibraryPathsService.Instance;
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
            "Se restableceran los ajustes y se quitara el arranque con Windows. Tus juegos, carpetas y comandos no se tocan.",
            "Restablecer ajustes", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        _settings.Reset();
        StartupService.SetEnabled(false, false);
        WindowBehaviorService.Instance?.ApplyAll();
        ThemeService.ApplyAccent(_settings.AccentColor);

        LoadFromState();
    }

    private void UpdateDataSummaries()
    {
        var covers = FormatSize(GetCoversSize());
        DataRow.Tag = AppSettingsService.AppFolder;
        CoversRow.Tag = $"Cache de portadas: {covers}";
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
        UpdateSourceRow.Tag = _settings.UpdateSource;
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
            SelectedPath = _settings.UpdateSource
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;

        _settings.UpdateSource = dialog.SelectedPath;
        _settings.Save();
        RefreshUpdateRow();
    }
}
