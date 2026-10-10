using Velopack;

namespace Launcher;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Debe ser lo primero: Velopack gestiona aqui los pasos de instalar,
        // actualizar y desinstalar antes de que WPF arranque.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}