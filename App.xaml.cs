using System.Windows;
using Launcher.Core.Services;

namespace Launcher;

public partial class App : Application
{
    #if DEBUG
        private const string MutexName = "Launcher.SingleInstance.Mutex.Debug";
        private const string ShowEventName = "Launcher.SingleInstance.Show.Debug";
    #else
        private const string MutexName = "Launcher.SingleInstance.Mutex";
        private const string ShowEventName = "Launcher.SingleInstance.Show";
    #endif
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private DateTime _lastErrorDialog = DateTime.MinValue;

    protected override void OnStartup(StartupEventArgs e)
    {
        // El evento se crea (o se abre) antes que el mutex: asi la segunda instancia
        // siempre encuentra algo que senalar.
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _mutex = new Mutex(true, MutexName, out var createdNew);

        if (!createdNew)
        {
            // Ya hay un Launcher abierto (quiza oculto en la bandeja): le pedimos que se muestre.
            _showEvent.Set();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        LogService.CleanOld();
        RegisterErrorHandlers();
        StartShowListener();

        // El acento guardado se aplica antes de crear la ventana para que no haya parpadeo de color.
        ThemeService.ApplyAccent(AppSettingsService.Instance.AccentColor);

        new MainWindow().Show();
    }

    private void StartShowListener()
    {
        var thread = new Thread(() =>
        {
            try
            {
                while (_showEvent!.WaitOne())
                {
                    Dispatcher.BeginInvoke(() => WindowBehaviorService.Instance?.ShowWindow());
                }
            }
            catch
            {
                // Al cerrar la app el handle desaparece: salimos sin ruido.
            }
        })
        {
            IsBackground = true,
            Name = "SingleInstanceListener"
        };
        thread.Start();
    }

    private void RegisterErrorHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            LogService.Error("Excepcion no controlada (UI)", args.Exception);

            // Maximo un aviso cada 30 s: un timer que falle en bucle no debe llenar la pantalla de dialogos.
            if (DateTime.Now - _lastErrorDialog > TimeSpan.FromSeconds(30))
            {
                _lastErrorDialog = DateTime.Now;
                MessageBox.Show(
                    $"Ha ocurrido un error inesperado:\n\n{args.Exception.Message}\n\n" +
                    "El detalle se ha guardado en el registro (Configuracion > Datos > Registro de errores).",
                    "Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString());
            LogService.Error("Excepcion no controlada (fatal)", ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogService.Error("Tarea con excepcion no observada", args.Exception);
            args.SetObserved();
        };
    }
}
