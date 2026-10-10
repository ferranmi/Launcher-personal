using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Launcher.Core.Interfaces;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Modules.Games;
using Launcher.Modules.Folders;
using Launcher.UI.Controls;
using Launcher.Modules.Media;
using System.Windows.Media;


namespace Launcher;

public partial class MainWindow : Window
{
    // Cache de vistas ya creadas, para no reconstruirlas cada vez que se cambia de modulo.
    private readonly Dictionary<string, UserControl> _viewCache = new();
    private string _baseVersionText = "";
    private readonly List<IModule> _modules;

    public MainWindow()
    {
        InitializeComponent();

        // Bandeja, atajo global, tamano/posicion recordados y arranque minimizado.
        WindowBehaviorService.Attach(this);
        VersionText.Text = $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0"}";

        _baseVersionText = VersionText.Text;
        UpdateService.Instance.Changed += (_, _) => RefreshUpdateIndicator();
        VersionText.MouseLeftButtonUp += async (_, _) => await PromptUpdateAsync();
        Loaded += async (_, _) => await StartUpdateChecksAsync();

        // === Registro de modulos ===
        // Cuando implementes un modulo de verdad (p.ej. Juegos), sustituye su
        // PlaceholderModule por tu propia clase (GamesModule : IModule) aqui.
        _modules = new List<IModule>
        {
            new GamesModule(),
            new FoldersModule(),
            new Launcher.Modules.Media.MediaModule(),
            new PlaceholderModule("music",     "Musica",        "\uEC4F", 3),
            new PlaceholderModule("search",    "Buscar",        "\uE721", 4),
            new Launcher.Modules.Commands.CommandsModule(),
            new Launcher.Modules.Monitor.MonitorModule(),
            new Launcher.Modules.Processes.ProcessesModule(),
            new Launcher.Modules.Settings.SettingsModule(),
        };

        // Configuracion necesita la lista para ofrecer "Modulo inicial".
        ModuleCatalog.Register(_modules);

        BuildSidebar();
        SelectStartModule();
    }

    private void BuildSidebar()
    {
        foreach (var module in _modules.OrderBy(m => m.Order))
        {
            var navButton = new NavButton
            {
                Content = module.DisplayName,
                Icon = module.Icon,
                GroupName = "MainNav",
                Tag = module.Id
            };
            navButton.Checked += (_, _) => NavigateTo(module);
            NavPanel.Children.Add(navButton);
        }
    }

    /// <summary>Abre el modulo elegido en Configuracion (o el ultimo usado). Si ese id
    /// ya no existe, cae al primero del sidebar.</summary>
    private void SelectStartModule()
    {
        var settings = AppSettingsService.Instance;
        var wanted = settings.StartModule == "last" ? settings.LastModule : settings.StartModule;

        var buttons = NavPanel.Children.OfType<NavButton>().ToList();
        var target = buttons.FirstOrDefault(b => (string?)b.Tag == wanted) ?? buttons.FirstOrDefault();

        if (target != null) target.IsChecked = true;
    }

    private void NavigateTo(IModule module)
    {
        if (!_viewCache.TryGetValue(module.Id, out var view))
        {
            view = module.CreateView();
            _viewCache[module.Id] = view;
        }

        ContentArea.Content = view;

        // Recordamos el ultimo modulo para la opcion "Ultimo usado".
        // Configuracion no cuenta: arrancar siempre en ajustes no tendria sentido.
        var settings = AppSettingsService.Instance;
        if (module.Id != "settings" && settings.LastModule != module.Id)
        {
            settings.LastModule = module.Id;
            settings.Save();
        }
    }

        // ===================== Actualizaciones =====================

    /// <summary>Si hay version nueva, el texto de version del sidebar se vuelve verde y clicable.</summary>
    private void RefreshUpdateIndicator()
    {
        var update = UpdateService.Instance;

        if (update.IsApplying)
        {
            VersionText.Text = "descargando...";
        }
        else if (update.Status == UpdateStatus.Available)
        {
            VersionText.Text = $"v{update.AvailableVersion} disponible";
            VersionText.Foreground = (Brush)FindResource("Brush.Accent");
            VersionText.Cursor = Cursors.Hand;
            VersionText.ToolTip = $"Estas en {_baseVersionText}. Clic para actualizar.";
        }
        else
        {
            VersionText.Text = _baseVersionText;
            VersionText.ClearValue(TextBlock.ForegroundProperty);
            VersionText.ClearValue(CursorProperty);
            VersionText.ToolTip = null;
        }
    }

    private async Task PromptUpdateAsync()
    {
        var update = UpdateService.Instance;
        if (update.Status != UpdateStatus.Available || update.IsBusy) return;

        var result = MessageBox.Show(this,
            $"Hay una version nueva del Launcher (v{update.AvailableVersion}).\n\n" +
            "La aplicacion se reiniciara para instalarla. Quieres actualizar ahora?",
            "Actualizacion", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes) await update.ApplyAsync();
    }

    private async Task StartUpdateChecksAsync()
    {
        if (!AppSettingsService.Instance.CheckUpdatesOnStartup) return;

        await Task.Delay(TimeSpan.FromSeconds(8)); // no competir con el arranque
        await UpdateService.Instance.CheckAsync();

        // Y despues, cada 6 horas mientras la app siga abierta.
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        timer.Tick += async (_, _) =>
        {
            if (AppSettingsService.Instance.CheckUpdatesOnStartup) await UpdateService.Instance.CheckAsync();
        };
        timer.Start();
    }

    // ===================== Controles de ventana =====================

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        // Close() y no Application.Shutdown(): Shutdown ignora Cancel en Closing y
        // rompe "Cerrar a la bandeja". Con la bandeja desactivada, cerrar la unica
        // ventana termina la app igualmente.
        Close();
    }
}