using System.Windows;
using System.Windows.Input;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.Commands;

/// <summary>Dialogo para anadir/editar una web del modulo Comandos (Brave, incognito, inicio de sesion automatico).</summary>
public partial class WebShortcutWindow : Window
{
    private readonly CommandShortcut _command;
    private readonly bool _editing;

    public CommandShortcut? Result { get; private set; }

    /// <summary>Anadir una web nueva.</summary>
    public WebShortcutWindow() : this(null) { }

    /// <summary>Editar una web existente (o anadir una nueva si command es null).</summary>
    public WebShortcutWindow(CommandShortcut? command)
    {
        InitializeComponent();

        _editing = command != null;
        _command = command ?? new CommandShortcut { Category = CommandCategory.Web, Icon = "\uE774" };

        if (_editing)
        {
            HeaderText.Text = "> Editar web";
            NameBox.Text = _command.Name;
            UrlBox.Text = _command.Target;
            ProfileBox.Text = _command.BrowserProfile;
            IncognitoCheck.IsChecked = _command.Incognito;
            AutoLoginCheck.IsChecked = _command.AutoLogin;

            if (!string.IsNullOrEmpty(_command.CredentialKey) && CredentialStore.Read(_command.CredentialKey) is { } stored)
            {
                UserBox.Text = stored.User;
            }
        }
        else
        {
            ProfileBox.Text = "Default";
        }

        UpdatePasswordHint();
        AutoLogin_Changed(this, new RoutedEventArgs());
    }

    private bool HasStoredPassword =>
        !string.IsNullOrEmpty(_command.CredentialKey) &&
        CredentialStore.Read(_command.CredentialKey) is { Password.Length: > 0 };

    private void UpdatePasswordHint() =>
        PasswordHint.Text = HasStoredPassword
            ? "Ya hay una contrasena guardada. Dejalo en blanco para conservarla."
            : "";

    private void AutoLogin_Changed(object sender, RoutedEventArgs e)
    {
        if (LoginPanel == null) return;
        LoginPanel.Visibility = AutoLoginCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            Warn("La web necesita un nombre.");
            return;
        }

        var url = UrlBox.Text.Trim();
        if (url.Length == 0)
        {
            Warn("Indica la direccion de la web.");
            return;
        }

        var autoLogin = AutoLoginCheck.IsChecked == true;
        var user = UserBox.Text.Trim();
        var password = PasswordBox.Password;

        if (autoLogin)
        {
            if (user.Length == 0)
            {
                Warn("Indica el usuario para el inicio de sesion automatico.");
                return;
            }

            if (password.Length == 0 && !HasStoredPassword)
            {
                Warn("Indica la contrasena para el inicio de sesion automatico.");
                return;
            }
        }

        _command.Name = NameBox.Text.Trim();
        _command.Category = CommandCategory.Web;
        _command.Target = url;
        _command.BrowserProfile = string.IsNullOrWhiteSpace(ProfileBox.Text) ? "Default" : ProfileBox.Text.Trim();
        _command.Incognito = IncognitoCheck.IsChecked == true;
        _command.AutoLogin = autoLogin;

        if (autoLogin)
        {
            _command.CredentialKey ??= $"Launcher/web/{_command.Id}";

            // Contrasena en blanco = conservar la guardada (solo se actualiza el usuario).
            var finalPassword = password.Length > 0
                ? password
                : CredentialStore.Read(_command.CredentialKey)?.Password ?? "";

            if (!CredentialStore.Save(_command.CredentialKey, user, finalPassword))
            {
                Warn("No se ha podido guardar la credencial en el Administrador de credenciales de Windows.");
                return;
            }
        }

        Result = _command;
        DialogResult = true;
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "Falta informacion", MessageBoxButton.OK, MessageBoxImage.Warning);

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
