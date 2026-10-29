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

    private static readonly (SdFolder Kind, string Label)[] FolderDefs =
    {
        (SdFolder.Models, "Modelos (checkpoints)"),
        (SdFolder.Loras, "LoRAs / LyCORIS"),
        (SdFolder.Vae, "VAE"),
        (SdFolder.Embeddings, "Embeddings"),
        (SdFolder.ControlNet, "Modelos de ControlNet"),
        (SdFolder.Extensions, "Extensiones de A1111"),
        (SdFolder.Outputs, "Salida de A1111 (outputs)"),
        (SdFolder.Gallery, "Imagenes del Launcher (galeria)")
    };

    // Linea "set COMMANDLINE_ARGS=..." de webui-user.bat (la ultima asignacion es la que vale).
    private static readonly Regex ArgsLine =
        new(@"^\s*set\s+COMMANDLINE_ARGS\s*=(.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ImageGenSettings _settings = ImageGenSettings.Instance;
    private readonly Dictionary<SdFolder, TextBox> _folderBoxes = new();

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

        BuildFolderRows();
        RunCheck();
    }

    // ===================== Filas de carpetas =====================

    private void BuildFolderRows()
    {
        var textBoxStyle = (Style)System.Windows.Application.Current.FindResource("TextBoxStyle");
        var mono = (System.Windows.Media.FontFamily)FindResource("Font.Mono");

        foreach (var (kind, label) in FolderDefs)
        {
            var box = new TextBox
            {
                Style = textBoxStyle,
                FontFamily = mono,
                FontSize = 11,
                Padding = new Thickness(8, 6, 8, 6),
                Text = _settings.CustomFolders.GetValueOrDefault(kind.ToString()) ?? ""
            };
            box.LostFocus += (_, _) => RunCheck();
            _folderBoxes[kind] = box;

            var browse = new Button
            {
                Content = "...",
                Style = (Style)FindResource("GhostButton"),
                Width = 34,
                Margin = new Thickness(8, 0, 0, 0)
            };
            browse.Click += (_, _) => BrowseFolder(kind);

            var open = new Button
            {
                Content = "Abrir",
                Style = (Style)FindResource("GhostButton"),
                Margin = new Thickness(8, 0, 0, 0)
            };
            open.Click += (_, _) => OpenFolder(kind);

            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(browse, 1);
            Grid.SetColumn(open, 2);
            row.Children.Add(box);
            row.Children.Add(browse);
            row.Children.Add(open);

            FolderRows.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("FieldLabel") });
            FolderRows.Children.Add(row);
        }
    }

    /// <summary>La ruta que se usaria ahora: la escrita en la caja o, si esta vacia, la de la instalacion.</summary>
    private string? ResolveFolder(SdFolder kind)
    {
        var typed = _folderBoxes.TryGetValue(kind, out var box) ? box.Text.Trim() : "";
        if (typed.Length > 0) return typed;

        var install = InstallBox.Text.Trim();
        return _settings.FolderFor(kind, install.Length == 0 ? null : install, ignoreCustom: true);
    }

    /// <summary>Muestra como sugerencia (texto tenue) la ruta que se usara si la caja queda vacia.</summary>
    private void RefreshHints()
    {
        var install = InstallBox.Text.Trim();

        foreach (var (kind, box) in _folderBoxes)
        {
            box.Tag = _settings.FolderFor(kind, install.Length == 0 ? null : install, ignoreCustom: true)
                      ?? "(indica antes la carpeta de instalacion)";
        }
    }

    private void BrowseFolder(SdFolder kind)
    {
        var dialog = new OpenFolderDialog { Title = "Selecciona la carpeta" };

        var current = ResolveFolder(kind);
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current)) dialog.InitialDirectory = current;

        if (dialog.ShowDialog(this) != true) return;

        _folderBoxes[kind].Text = dialog.FolderName;
        RunCheck();
    }

    private void OpenFolder(SdFolder kind)
    {
        var folder = ResolveFolder(kind);

        if (string.IsNullOrWhiteSpace(folder))
        {
            MessageBox.Show(this, "Indica primero la carpeta de instalacion o escribe una ruta.",
                "Abrir carpeta", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            // La carpeta de imagenes del Launcher se crea si todavia no existe.
            if (kind == SdFolder.Gallery) Directory.CreateDirectory(folder);

            if (!Directory.Exists(folder))
            {
                MessageBox.Show(this, $"La carpeta no existe todavia:\n{folder}",
                    "Abrir carpeta", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo abrir la carpeta", ex);
        }
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
        RefreshHints();

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

        sb.AppendLine(CountLine("Modelos", SdFolder.Models));
        sb.AppendLine(CountLine("LoRAs / LyCORIS", SdFolder.Loras));
        sb.Append(CountLine("VAE", SdFolder.Vae));

        StatusText.Text = sb.ToString();
    }

    private string CountLine(string label, SdFolder kind)
    {
        var folder = ResolveFolder(kind);
        if (folder == null || !Directory.Exists(folder)) return $"[!] {label}: carpeta no encontrada";

        try
        {
            var count = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Count(f => ModelExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
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

    // ===================== Wildcards =====================

    private void BrowseWildcards_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Selecciona la carpeta de wildcards" };
        if (Directory.Exists(WildcardsBox.Text)) dialog.InitialDirectory = WildcardsBox.Text;

        if (dialog.ShowDialog(this) == true) WildcardsBox.Text = dialog.FolderName;
    }

    private void UseDynamicPrompts_Click(object sender, RoutedEventArgs e)
    {
        var extensions = ResolveFolder(SdFolder.Extensions);
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

        // Carpetas manuales: vacias se descartan; todas deben existir salvo la de imagenes del Launcher (se crea).
        var custom = new Dictionary<string, string>();
        foreach (var (kind, box) in _folderBoxes)
        {
            var path = box.Text.Trim();
            if (path.Length == 0) continue;

            if (!Directory.Exists(path))
            {
                if (kind == SdFolder.Gallery)
                {
                    try
                    {
                        Directory.CreateDirectory(path);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"No se ha podido crear la carpeta de imagenes:\n{ex.Message}",
                            "Imagenes IA", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    var label = FolderDefs.First(d => d.Kind == kind).Label;
                    MessageBox.Show(this, $"La carpeta \"{label}\" no existe:\n{path}",
                        "Imagenes IA", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            custom[kind.ToString()] = path;
        }

        // Se calcula ANTES de asignar la nueva instalacion.
        var installChanged = !string.Equals(
            install.TrimEnd('\\'), (_settings.InstallPath ?? "").TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

        _settings.Url = url;
        _settings.InstallPath = install.Length == 0 ? null : install;
        _settings.AutoStartServer = AutoStartCheck.IsChecked == true;
        _settings.WildcardsFolder = wildcards.Length == 0 ? null : wildcards;
        _settings.CustomFolders = custom;

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
