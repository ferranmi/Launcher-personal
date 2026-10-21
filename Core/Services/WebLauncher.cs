using System.Diagnostics;
using System.IO;
using System.Windows;
using Launcher.Core.Models;
using Microsoft.Playwright;
using Microsoft.Win32;

namespace Launcher.Core.Services;

/// <summary>
/// Abre las entradas "Web" del modulo Comandos en Brave.
///  - Sin inicio de sesion automatico: Brave normal con el perfil indicado (por defecto "Default") y, si se
///    pide, en modo incognito. Es tu Brave de siempre, con tus sesiones.
///  - Con inicio de sesion automatico: Playwright abre Brave con una carpeta de perfil PROPIA del Launcher
///    (%AppData%\Launcher\brave-profile; no el perfil Default, porque Chromium ya no deja controlar el perfil
///    principal) y rellena usuario y contrasena. En modo normal esa carpeta recuerda la sesion; en incognito
///    se usa un contexto aislado que no guarda nada.
/// Cualquier problema (falta la contrasena, contrasena erronea, Brave no encontrado...) se anota en el
/// Registro del dashboard y genera un aviso de Windows.
/// </summary>
public static class WebLauncher
{
    private enum LoginOutcome { Success, FormNotFound, NotAccepted }

    // Playwright y el navegador viven mientras viva el Launcher; si se cierra el Launcher, se cierran.
    private static readonly List<object> Sessions = new();

    private static string ProfileFolder => Path.Combine(AppSettingsService.AppFolder, "brave-profile");

    // ===================== Entrada =====================

    public static void Launch(CommandShortcut command)
    {
        var url = NormalizeUrl(command.Target);
        if (url == null)
        {
            Problem(StatusLevel.Warning, command.Name, $"la direccion web no es valida: {command.Target}");
            return;
        }

        if (!command.AutoLogin)
        {
            OpenInBrave(command, url);
            return;
        }

        _ = Task.Run(() => AutoLoginAsync(command, url));
    }

    private static string? NormalizeUrl(string text)
    {
        var raw = text.Trim();
        if (raw.Length == 0) return null;
        if (!raw.Contains("://", StringComparison.Ordinal)) raw = "https://" + raw;

        return Uri.TryCreate(raw, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.ToString()
            : null;
    }

    // ===================== Brave =====================

    public static string? FindBrave()
    {
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\brave.exe");
                if (key?.GetValue("") is string path && File.Exists(path)) return path;
            }
            catch
            {
                // sin acceso a esa clave: se prueban las rutas habituales
            }
        }

        var relative = Path.Combine("BraveSoftware", "Brave-Browser", "Application", "brave.exe");
        return new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), relative),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), relative),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), relative)
        }.FirstOrDefault(File.Exists);
    }

    private static void OpenInBrave(CommandShortcut command, string url)
    {
        var brave = FindBrave();
        if (brave == null)
        {
            Problem(StatusLevel.Warning, command.Name, "no se ha encontrado Brave: se abre con el navegador predeterminado");
            OpenWithDefaultBrowser(url);
            return;
        }

        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(command.BrowserProfile)) args.Add($"--profile-directory=\"{command.BrowserProfile}\"");
        if (command.Incognito) args.Add("--incognito");
        args.Add($"\"{url}\"");

        try
        {
            Process.Start(new ProcessStartInfo { FileName = brave, Arguments = string.Join(" ", args), UseShellExecute = true });
            command.LastUsed = DateTime.Now;
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo abrir Brave para '{command.Name}'", ex);
            Problem(StatusLevel.Defcon, command.Name, $"no se pudo abrir Brave: {ex.Message}");
        }
    }

    private static void OpenWithDefaultBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch (Exception ex) { LogService.Error("No se pudo abrir el navegador predeterminado", ex); }
    }

    // ===================== Inicio de sesion automatico =====================

    private static async Task AutoLoginAsync(CommandShortcut command, string url)
    {
        var name = command.Name;
        var brave = FindBrave();

        if (brave == null)
        {
            Problem(StatusLevel.Warning, name, "no se ha encontrado Brave: no se puede iniciar sesion automaticamente");
            OpenWithDefaultBrowser(url);
            return;
        }

        var credential = string.IsNullOrEmpty(command.CredentialKey) ? null : CredentialStore.Read(command.CredentialKey);
        if (credential == null || string.IsNullOrEmpty(credential.Value.Password))
        {
            Problem(StatusLevel.Warning, name, "falta la contrasena guardada: se abre la pagina sin iniciar sesion");
            OpenInBrave(command, url);
            return;
        }

        try
        {
            var playwright = await Playwright.CreateAsync();
            IBrowserContext context;

            if (command.Incognito)
            {
                var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    ExecutablePath = brave,
                    Headless = false
                });
                context = await browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = ViewportSize.NoViewport });
                lock (Sessions) Sessions.Add(browser);
            }
            else
            {
                Directory.CreateDirectory(ProfileFolder);
                context = await playwright.Chromium.LaunchPersistentContextAsync(ProfileFolder, new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = brave,
                    Headless = false,
                    ViewportSize = ViewportSize.NoViewport
                });
            }

            lock (Sessions)
            {
                Sessions.Add(playwright);
                Sessions.Add(context);
            }

            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            var outcome = await FillLoginAsync(page, credential.Value.User, credential.Value.Password);

            switch (outcome)
            {
                case LoginOutcome.Success:
                    Info(name, "sesion iniciada");
                    command.LastUsed = DateTime.Now;
                    break;

                case LoginOutcome.FormNotFound when !command.Incognito:
                    // Con el perfil propio de la app lo normal es que la sesion ya estuviera iniciada.
                    Info(name, "abierto: no hay formulario de inicio de sesion (sesion ya iniciada)");
                    break;

                case LoginOutcome.FormNotFound:
                    Problem(StatusLevel.Warning, name, "no se encontro el formulario de inicio de sesion en la pagina");
                    break;

                default:
                    Problem(StatusLevel.Warning, name, "no se pudo iniciar sesion: contrasena erronea o formulario distinto");
                    break;
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Inicio de sesion automatico de '{name}'", ex);
            Problem(StatusLevel.Defcon, name, $"error al iniciar sesion automaticamente: {ex.Message}");
        }
    }

    private static async Task<LoginOutcome> FillLoginAsync(IPage page, string user, string password)
    {
        const string passwordSelector = "input[type='password']:visible";
        const string userSelector =
            "input[autocomplete='username']:visible, input[type='email']:visible, input[name*='user' i]:visible, " +
            "input[name*='mail' i]:visible, input[name*='login' i]:visible, input[type='text']:visible";

        var passwordField = page.Locator(passwordSelector).First;
        var userField = page.Locator(userSelector).First;

        try
        {
            await passwordField.Or(userField).First.WaitForAsync(
                new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 12000 });
        }
        catch (TimeoutException)
        {
            return LoginOutcome.FormNotFound;
        }

        if (!await passwordField.IsVisibleAsync())
        {
            // Formulario en dos pasos: primero el usuario y, tras Enter, aparece la contrasena.
            await userField.FillAsync(user);
            await userField.PressAsync("Enter");

            try
            {
                await passwordField.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 12000 });
            }
            catch (TimeoutException)
            {
                return LoginOutcome.NotAccepted;
            }
        }
        else if (await userField.IsVisibleAsync())
        {
            await userField.FillAsync(user);
        }

        await passwordField.FillAsync(password);
        await passwordField.PressAsync("Enter");

        // Si el campo de contrasena sigue ahi unos segundos despues, el inicio de sesion no se completo.
        try
        {
            await passwordField.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 10000 });
            return LoginOutcome.Success;
        }
        catch (TimeoutException)
        {
            return LoginOutcome.NotAccepted;
        }
    }

    // ===================== Avisos (Registro del dashboard + notificacion de Windows) =====================

    private static void Problem(StatusLevel level, string source, string message) => Report(level, source, message);

    private static void Info(string source, string message) => Report(StatusLevel.Online, source, message);

    private static void Report(StatusLevel level, string source, string message)
    {
        LogService.Info($"[Web] {source}: {message}");

        // El StatusEvaluator se toca desde el hilo de la interfaz; de ahi sale tambien la notificacion.
        Application.Current?.Dispatcher.BeginInvoke(() =>
            SystemStatusHost.Instance.Evaluator.Report(level, source, message));
    }
}
