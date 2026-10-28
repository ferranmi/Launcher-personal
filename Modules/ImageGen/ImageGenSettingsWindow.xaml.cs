using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Launcher.Core.Services;
using Microsoft.Win32;

namespace Launcher.Modules.ImageGen;

public partial class ImageGenSettingsWindow : Window
{
    private static readonly string[] ModelExtensions = { ".safetensors", ".ckpt", ".pt", ".pth", ".bin" };

    // Linea "set COMMANDLINE_ARGS=..." de webui-user.bat (la ultima asignacion es la que vale).
    private static readonly Regex ArgsLine =
        new(@"^\s*set\s+COMMANDLINE_ARGS\s*=(.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ImageGenSettings _settings = ImageGenSettings.Instance;

    /// <summary>True si el usuario guardo (para que quien abrio la ventana vuelva a conectar).</summary>
    public bool Saved { get; private set; }

    public ImageGenSettingsWindow()
    {
        InitializeComponent();

        UrlBox.Text = _settings.Url;
        InstallBox.Text = _settings.InstallPath ?? "";
        AutoStartCheck.IsChecked = _settings.AutoStartServer;
        WildcardsBox.Text = _settings.WildcardsFolder ?? "";
        WildcardsHint.Text = $"Vacio = la del Launcher ({Extensions.WildcardsExtension.DefaultFolder}). " +
                             "Con la de Dynamic Prompts se reutilizan tus wildcards .txt existentes.";

        RunCheck();
    }

    // ===================== Carpeta de instalacion =====================

    private void BrowseInstall_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Selecciona la carpeta de instalacion de A1111" };
        if (Directory.Exists(InstallBox.Text)) dialog.InitialDirectory = InstallBox.Text;

        if (dialog.ShowDialog(this) != true) return;

        InstallBox.Text = dialog.FolderName;
        RunCheck();
    }

    private void InstallBox_LostFocus(object sender, RoutedEventArgs e) => RunCheck();

    private void Check_Click(object sender, RoutedEventArgs e) => RunCheck();

    private async void AutoDetect_Click(object sender, RoutedEventArgs e)
    {
        AutoDetectButton.IsEnabled = false;
        StatusText.Text = "buscando...";

        try
        {
            string? found = null;

            // 1) Si A1111 esta en marcha, el mismo servidor dice donde esta instalado (data_dir).
            var sd = new StableDiffusionService { BaseUrl = UrlBox.Text.Trim() };
            var flags = await sd.GetCmdFlagsAsync();
            if (flags.TryGetValue("data_dir", out var dataDir) && HasLauncherBat(dataDir)) found = dataDir;

            // 2) Si no, las ubicaciones habituales.
            found ??= Candidates().FirstOrDefault(HasLauncherBat);

            if (found == null)
            {
                StatusText.Text = "[!] no se ha encontrado ninguna instalacion. Indica la carpeta con \"...\".";
                return;
            }

            InstallBox.Text = found;
            RunCheck();
        }
        finally
        {
            AutoDetectButton.IsEnabled = true;
        }
    }

    private static bool HasLauncherBat(string folder) =>
        !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "webui-user.bat"));

    private static IEnumerable<string> Candidates()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new[]
        {
            profile,
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(profile, "Desktop"),
            @"C:\", @"D:\", @"E:\"
        };
        var names = new[] { "stable-diffusion-webui", "stable-diffusion-webui-forge", "sd-webui", "automatic1111" };

        foreach (var root in roots)
        {
            foreach (var name in names) yield return Path.Combine(root, name);
        }
    }

    // ===================== Comprobacion de la instalacion =====================

    private void RunCheck()
    {
        var install = InstallBox.Text.Trim();

        if (install.Length == 0)
        {
            StatusText.Text = "[ ] sin carpeta de instalacion. Auto-detectar o elige la carpeta con \"...\".";
            return;
        }

        if (!Directory.Exists(install))
        {
            StatusText.Text = "[!] esa carpeta no existe.";
            return;
        }

        var sb = new StringBuilder();
        var bat = Path.Combine(install, "webui-user.bat");

        if (File.Exists(bat))
        {
            sb.AppendLine("[ok] webui-user.bat encontrado");
            sb.AppendLine(HasApiFlag(bat)
                ? "[ok] COMMANDLINE_ARGS incluye --api"
                : "[!] falta --api en COMMANDLINE_ARGS (pulsa \"Anadir --api\")");
        }
        else
        {
            sb.AppendLine("[!] no hay webui-user.bat en esa carpeta");
        }

        sb.AppendLine(CountLine("Modelos", SdFolder.Models, install, ModelExtensions));
        sb.AppendLine(CountLine("LoRAs / LyCORIS", SdFolder.Loras, install, ModelExtensions));
        sb.Append(CountLine("VAE", SdFolder.Vae, install, ModelExtensions));

        StatusText.Text = sb.ToString();
    }

    private string CountLine(string label, SdFolder kind, string install, string[] extensions)
    {
        var folder = _settings.FolderFor(kind, install);
        if (folder == null || !Directory.Exists(folder)) return $"[!] {label}: carpeta no encontrada";

        try
        {
            var count = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Count(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
            return $"[ok] {label}: {count} archivo(s)";
        }
        catch
        {
            return $"[!] {label}: no se pudo leer la carpeta";
        }
    }

    // ===================== --api en webui-user.bat =====================

    private static string? LastArgs(string batPath)
    {
        string? args = null;
        foreach (var line in File.ReadAllLines(batPath))
        {
            var match = ArgsLine.Match(line);
            if (match.Success) args = match.Groups[1].Value;
        }

        return args;
    }

    private static bool HasApiFlag(string batPath)
    {
        try
        {
            return LastArgs(batPath)?.Contains("--api", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }

    private void AddApi_Click(object sender, RoutedEventArgs e)
    {
        var bat = Path.Combine(InstallBox.Text.Trim(), "webui-user.bat");

        if (!File.Exists(bat))
        {
            MessageBox.Show(this, "No se ha encontrado webui-user.bat en la carpeta indicada.",
                "Anadir --api", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (HasApiFlag(bat))
        {
            MessageBox.Show(this, "COMMANDLINE_ARGS ya incluye --api.",
                "Anadir --api", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(this,
            "Se anadira --api a COMMANDLINE_ARGS en webui-user.bat y se guardara una copia como webui-user.bat.bak. " +
            "Tendras que reiniciar A1111 para que surta efecto. Continuar?",
            "Anadir --api", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var backup = bat + ".bak";
            if (!File.Exists(backup)) File.Copy(bat, backup);

            var lines = File.ReadAllLines(bat).ToList();
            var edited = false;

            // La ultima asignacion es la que cuenta: se edita esa.
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                var match = ArgsLine.Match(lines[i]);
                if (!match.Success) continue;

                var args = match.Groups[1].Value.Trim();
                lines[i] = $"set COMMANDLINE_ARGS={(args.Length > 0 ? args + " " : "")}--api";
                edited = true;
                break;
            }

            if (!edited)
            {
                // Sin la linea: se inserta justo antes de "call webui.bat".
                const string newLine = "set COMMANDLINE_ARGS=--api";
                var callIndex = lines.FindIndex(l => l.TrimStart().StartsWith("call webui.bat", StringComparison.OrdinalIgnoreCase));
                if (callIndex >= 0) lines.Insert(callIndex, newLine);
                else lines.Add(newLine);
            }

            File.WriteAllLines(bat, lines);
            RunCheck();
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo editar webui-user.bat", ex);
            MessageBox.Show(this, $"No se ha podido modificar webui-user.bat: {ex.Message}",
                "Anadir --api", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ===================== Carpetas rapidas =====================

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !Enum.TryParse<SdFolder>(tag, out var kind)) return;

        var folder = _settings.FolderFor(kind, InstallBox.Text.Trim());
        if (folder == null || !Directory.Exists(folder))
        {
            MessageBox.Show(this,
                folder == null
                    ? "Indica primero la carpeta de instalacion."
                    : $"La carpeta no existe todavia:\n{folder}",
                "Abrir carpeta", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        OpenInExplorer(folder);
    }

    private static void OpenInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo abrir la carpeta", ex);
        }
    }

    // ===================== Wildcards =====================

    private void BrowseWildcards_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Selecciona la carpeta de wildcards" };
        if (Directory.Exists(WildcardsBox.Text)) dialog.InitialDirectory = WildcardsBox.Text;

        if (dialog.ShowDialog(this) == true) WildcardsBox.Text = dialog.FolderName;
    }

    private void UseDynamicPrompts_Click(object sender, RoutedEventArgs e)
    {
        var extensions = _settings.FolderFor(SdFolder.Extensions, InstallBox.Text.Trim());
        var path = extensions == null ? null : Path.Combine(extensions, "sd-dynamic-prompts", "wildcards");

        if (path == null || !Directory.Exists(path))
        {
            MessageBox.Show(this,
                "No se ha encontrado la carpeta de wildcards de Dynamic Prompts (extensions\\sd-dynamic-prompts\\wildcards).",
                "Wildcards", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        WildcardsBox.Text = path;
    }

    private void DefaultWildcards_Click(object sender, RoutedEventArgs e) => WildcardsBox.Text = "";

    // ===================== Guardar =====================

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlBox.Text.Trim().TrimEnd('/');
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            MessageBox.Show(this, "La direccion del servidor no es valida (p.ej. http://127.0.0.1:7860).",
                "Imagenes IA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var install = InstallBox.Text.Trim();
        if (install.Length > 0 && !Directory.Exists(install))
        {
            MessageBox.Show(this, "La carpeta de instalacion no existe.",
                "Imagenes IA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var wildcards = WildcardsBox.Text.Trim();
        if (wildcards.Length > 0 && !Directory.Exists(wildcards))
        {
            MessageBox.Show(this, "La carpeta de wildcards no existe.",
                "Imagenes IA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var installChanged = !string.Equals(
            install.TrimEnd('\\'), (_settings.InstallPath ?? "").TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

        _settings.Url = url;
        _settings.InstallPath = install.Length == 0 ? null : install;
        _settings.AutoStartServer = AutoStartCheck.IsChecked == true;
        _settings.WildcardsFolder = wildcards.Length == 0 ? null : wildcards;

        // Si cambio la instalacion, las carpetas aprendidas de la anterior ya no valen:
        // se volveran a aprender al conectar.
        if (installChanged)
        {
            _settings.CkptDir = _settings.LoraDir = _settings.VaeDir = _settings.EmbeddingsDir = _settings.ControlNetDir = null;
        }

        _settings.Save();
        Saved = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
