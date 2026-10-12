using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Launcher.Core.Services;

namespace Launcher.Modules.Games;

public partial class SteamAccountWindow : Window
{
    private const string ApiKeyPageUrl = "https://steamcommunity.com/dev/apikey";

    /// <summary>True si el usuario guardo.</summary>
    public bool Saved { get; private set; }

    public SteamAccountWindow()
    {
        InitializeComponent();

        var paths = LibraryPathsService.Instance;
        ApiKeyBox.Text = paths.SteamApiKey ?? "";

        // Si aun no hay SteamID guardado, se propone el de la instalacion local.
        SteamIdBox.Text = paths.SteamId ?? SteamInfoService.TryDetectSteamId64() ?? "";
    }

    private void GetKey_Click(object sender, RoutedEventArgs e)
    {
        if (!GameLinkLauncher.OpenUrl(ApiKeyPageUrl))
        {
            MessageBox.Show(this, $"No se ha podido abrir el navegador. Abre a mano:\n{ApiKeyPageUrl}",
                "Cuenta de Steam", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Detect_Click(object sender, RoutedEventArgs e)
    {
        var id = SteamInfoService.TryDetectSteamId64();
        if (id == null)
        {
            MessageBox.Show(this,
                "No se ha podido detectar tu SteamID64. Puedes copiarlo desde tu perfil (steamid.io lo calcula a partir de la URL).",
                "Cuenta de Steam", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SteamIdBox.Text = id;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Text.Trim();
        var steamId = SteamIdBox.Text.Trim();

        if (steamId.Length > 0 && !Regex.IsMatch(steamId, @"^\d{17}$"))
        {
            MessageBox.Show(this, "El SteamID64 son 17 digitos. Usa \"Detectar\" o copialo de tu perfil.",
                "Falta informacion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (key.Length > 0 && !Regex.IsMatch(key, "^[0-9A-Fa-f]{32}$"))
        {
            var proceed = MessageBox.Show(this,
                "La clave no tiene el formato habitual (32 caracteres hexadecimales). Guardarla igualmente?",
                "Revisa la clave", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (proceed != MessageBoxResult.Yes) return;
        }

        var paths = LibraryPathsService.Instance;
        paths.SteamApiKey = key.Length == 0 ? null : key;
        paths.SteamId = steamId.Length == 0 ? null : steamId;
        paths.Save();

        Saved = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
