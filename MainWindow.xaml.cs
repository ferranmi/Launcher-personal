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


namespace Launcher;

public partial class MainWindow : Window
{
    // Cache de vistas ya creadas, para no reconstruirlas cada vez que se cambia de modulo.
    private readonly Dictionary<string, UserControl> _viewCache = new();
    private readonly List<IModule> _modules;

    public MainWindow()
    {
        InitializeComponent();

        // Bandeja, atajo global, tamano/posicion recordados y arranque minimizado.
        WindowBehaviorService.Attach(this);
        VersionText.Text = $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0"}";

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