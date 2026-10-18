using System.IO;
using System.Text.Json;
using System.Windows;

namespace Launcher.Core.Services;

/// <summary>
/// Ajustes generales de la aplicacion, persistidos en %AppData%\Launcher\config.json.
/// "Iniciar con Windows" no se guarda aqui: la fuente de verdad es el registro
/// (ver StartupService), asi nunca se desincroniza.
/// </summary>
public class AppSettingsService
{
    public static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Launcher");

    private static readonly string ConfigPath = Path.Combine(AppFolder, "config.json");

    /// <summary>Color de acento por defecto (el verde fosforo del tema).</summary>
    public const string DefaultAccent = "#3ECF7E";

    /// <summary>Carpeta (o URL) por defecto donde estan las versiones empaquetadas con vpk.</summary>
    public static readonly string DefaultUpdateSource = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "LauncherReleases");

    private static AppSettingsService? _instance;
    public static AppSettingsService Instance => _instance ??= Load();

    /// <summary>Se dispara tras cada Save() para que las vistas ya creadas (p.ej. Juegos)
    /// reaccionen al instante a un cambio de ajuste.</summary>
    public event EventHandler? Changed;

    // ----- Arranque -----
    public bool StartMinimized { get; set; }

    /// <summary>Id del modulo que se abre al arrancar, o "last" para el ultimo usado.</summary>
    public string StartModule { get; set; } = "games";

    /// <summary>Ultimo modulo visitado (se usa cuando StartModule es "last").</summary>
    public string? LastModule { get; set; }

    // ----- Ventana -----
    public bool AlwaysOnTop { get; set; }
    public bool CloseToTray { get; set; }
    public bool RememberWindowBounds { get; set; } = true;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }

    /// <summary>"None", "Ctrl+Alt+L", "Ctrl+Alt+Space" o "Ctrl+Shift+Space".</summary>
    public string Hotkey { get; set; } = "None";

    /// <summary>Color de acento en formato #RRGGBB (ver ThemeService).</summary>
    public string AccentColor { get; set; } = DefaultAccent;

    // ----- Dashboard -----
    /// <summary>"relaxed", "normal" o "strict".</summary>
    public string ThresholdProfile { get; set; } = "normal";

    /// <summary>Segundos entre lecturas con el dashboard visible (1, 2 o 5).</summary>
    public int StatusIntervalSeconds { get; set; } = 1;

    public bool DashboardEffects { get; set; } = true;
    public bool BootSequence { get; set; } = true;
    public bool WindowsEventLog { get; set; } = true;

    // ----- Ventana -----
    public bool SidebarCollapsed { get; set; }

    // ----- Fondo -----
    /// <summary>"none", "gradient", "animated" o "image".</summary>
    public string BackgroundMode { get; set; } = "none";
    public string? BackgroundImagePath { get; set; }

    /// <summary>0.05 - 1.0. Baja = mas sutil, el contenido se lee mejor.</summary>
    public double BackgroundOpacity { get; set; } = 0.35;

    // ----- Disposicion de contenedores (ver LayoutPersistence) -----
    public Dictionary<string, double[]> Layouts { get; set; } = new();

    // ----- Monitor y Procesos -----

    /// <summary>Intervalo de refresco en segundos. 0 = automatico (1 s en Monitor,
    /// 1,5 s en Procesos). Usa GetRefreshSeconds para leerlo con el valor automatico.</summary>
    public double RefreshSeconds { get; set; }

    /// <summary>Pedir confirmacion antes de finalizar un proceso o un grupo.</summary>
    public bool ConfirmKillProcess { get; set; } = true;

    // ----- Juegos -----
    public bool SyncOnStartup { get; set; }
    public bool ConfirmDelete { get; set; } = true;

    /// <summary>"small", "medium" o "large".</summary>
    public string CardSize { get; set; } = "medium";

    // ----- Actualizaciones -----

    /// <summary>Carpeta (o URL) donde estan las versiones empaquetadas con vpk.</summary>
    public string UpdateSource { get; set; } = DefaultUpdateSource;

    /// <summary>Busca versiones nuevas al abrir y cada 6 horas.</summary>
    public bool CheckUpdatesOnStartup { get; set; } = true;

    /// <summary>Si hay version nueva, la descarga e instala sin preguntar (reinicia la app).</summary>
    public bool AutoApplyUpdates { get; set; } = true;

    /// <summary>Devuelve el intervalo elegido, o autoSeconds si esta en "automatico".</summary>
    public double GetRefreshSeconds(double autoSeconds) => RefreshSeconds > 0 ? RefreshSeconds : autoSeconds;

    private static AppSettingsService Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<AppSettingsService>(json);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // config.json corrupto: seguimos con valores por defecto en vez de reventar.
        }

        return new AppSettingsService();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // No critico: en la proxima sesion se usaran los valores anteriores.
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Vuelve a los valores por defecto y guarda.</summary>
    public void Reset()
    {
        StartMinimized = false;
        StartModule = "games";
        AlwaysOnTop = false;
        CloseToTray = false;
        RememberWindowBounds = true;
        WindowLeft = WindowTop = WindowWidth = WindowHeight = null;
        Hotkey = "None";
        SyncOnStartup = false;
        ConfirmDelete = true;
        CardSize = "medium";
        UpdateSource = DefaultUpdateSource;
        CheckUpdatesOnStartup = true;
        RefreshSeconds = 0; // 0 = automatico
        AutoApplyUpdates = true;
        ThresholdProfile = "normal";
        StatusIntervalSeconds = 1;
        DashboardEffects = true;
        BootSequence = true;
        WindowsEventLog = true;
        SidebarCollapsed = false;
        AccentColor = DefaultAccent;
        BackgroundMode = "none";
        BackgroundImagePath = null;
        BackgroundOpacity = 0.35;
        Layouts.Clear();
        Save();
    }

    /// <summary>Aplica a una ventana los ajustes que le afectan directamente.</summary>
    public void ApplyToWindow(Window window)
    {
        window.Topmost = AlwaysOnTop;
    }
}
