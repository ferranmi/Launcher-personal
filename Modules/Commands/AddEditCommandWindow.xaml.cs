using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Launcher.Core.Converters;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace Launcher.Modules.Commands;

public partial class AddEditCommandWindow : Window
{
    /// <summary>Paleta de iconos (glifos de Segoe MDL2 Assets). Si hace falta uno que no
    /// este aqui, siempre se puede usar el icono de la propia aplicacion.</summary>
    private static readonly (string Icon, string Label)[] IconOptions =
    {
        ("\uE756", "Terminal"),
        ("\uE71D", "Aplicaciones"),
        ("\uE713", "Ajustes"),
        ("\uE774", "Web"),
        ("\uE7FC", "Juegos"),
        ("\uE943", "Codigo"),
        ("\uE8B7", "Carpeta"),
        ("\uE7F4", "Pantalla"),
        ("\uE8D6", "Musica"),
        ("\uE714", "Video"),
        ("\uEB9F", "Imagenes"),
        ("\uE8A5", "Documentos"),
        ("\uE765", "Teclado"),
        ("\uE772", "Dispositivos"),
        ("\uE7E8", "Energia"),
        ("\uE9D9", "Monitor"),
        ("\uE767", "Volumen"),
        ("\uE753", "Nube"),
        ("\uE721", "Buscar"),
        ("\uE734", "Favorito"),
    };

    private readonly CommandShortcut? _editingCommand;
    private readonly string _iconId;

    private string _selectedIcon = "\uE756";
    private string? _iconImagePath;

    public CommandShortcut? Result { get; private set; }

    /// <summary>Constructor para anadir un comando nuevo.</summary>
    public AddEditCommandWindow()
    {
        InitializeComponent();
        _iconId = Guid.NewGuid().ToString("N");
        BuildIconPicker();
        UpdateIncognitoHint();
    }

    /// <summary>Constructor para editar un comando existente.</summary>
    public AddEditCommandWindow(CommandShortcut command) : this()
    {
        _editingCommand = command;
        _iconId = command.Id;
        HeaderText.Text = "> Editar comando";

        NameBox.Text = command.Name;
        CategoryCombo.SelectedIndex = (int)command.Category;
        TargetBox.Text = command.Target;
        ArgumentsBox.Text = command.Arguments;
        WorkingDirBox.Text = command.WorkingDirectory ?? "";

        SelectIcon(command.Icon);
        if (command.HasIconImage)
        {
            _iconImagePath = command.IconImagePath;
            UncheckTiles();
            ShowAppIcon(_iconImagePath);
            AppIconStatus.Text = "icono de la app aplicado_";
        }

        TerminalCombo.SelectedIndex = (int)command.Terminal;
        RunAsAdminCheck.IsChecked = command.RunAsAdmin;
        IncognitoCheck.IsChecked = command.Incognito;
        UpdateIncognitoHint();
    }

    // ===================== Selector de icono =====================

    private void BuildIconPicker()
    {
        foreach (var (icon, label) in IconOptions)
        {
            var button = new ToggleButton
            {
                Tag = icon,
                Content = icon,
                ToolTip = label,
                Margin = new Thickness(0, 0, 8, 8),
                Style = (Style)FindResource("IconOptionStyle")
            };

            // Click (y no Checked): solo se dispara por una accion del usuario, asi que
            // marcar fichas por codigo no borra el icono de la aplicacion.
            button.Click += (_, _) =>
            {
                ClearAppIcon();
                SelectIcon(icon);
            };

            IconPanel.Children.Add(button);
        }

        SelectIcon(_selectedIcon);
    }

    private void SelectIcon(string icon)
    {
        _selectedIcon = icon;
        foreach (var toggle in IconPanel.Children.OfType<ToggleButton>())
        {
            var isMatch = (string)toggle.Tag == icon;
            if (toggle.IsChecked != isMatch) toggle.IsChecked = isMatch;
        }
    }

    private void UncheckTiles()
    {
        foreach (var toggle in IconPanel.Children.OfType<ToggleButton>())
        {
            toggle.IsChecked = false;
        }
    }

    private void ClearAppIcon()
    {
        _iconImagePath = null;
        ShowAppIcon(null);
        AppIconStatus.Text = "";
    }

    private void ShowAppIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AppIconPreview.Source = null;
            AppIconPreview.Visibility = Visibility.Collapsed;
            return;
        }

        AppIconPreview.Source = new PathToBitmapConverter().Convert(
            path, typeof(System.Windows.Media.ImageSource), null, CultureInfo.InvariantCulture)
            as System.Windows.Media.ImageSource;

        AppIconPreview.Visibility = AppIconPreview.Source == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UseAppIcon_Click(object sender, RoutedEventArgs e)
    {
        var target = TargetBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            AppIconStatus.Text = "indica primero el comando o la ruta";
            return;
        }

        var exePath = CommandsService.ResolveExecutable(target);
        if (exePath == null)
        {
            AppIconStatus.Text = "no se encontro el ejecutable en este equipo";
            return;
        }

        var iconPath = IconExtractionService.ExtractAsCover(exePath, _iconId);
        if (iconPath == null)
        {
            AppIconStatus.Text = "no se pudo extraer el icono";
            return;
        }

        _iconImagePath = iconPath;
        UncheckTiles();
        ShowAppIcon(iconPath);
        AppIconStatus.Text = "icono de la app aplicado_ (elige un glifo para quitarlo)";
    }

    // ===================== Incognito =====================

    private void TargetBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateIncognitoHint();

    private void UpdateIncognitoHint()
    {
        if (IncognitoHint == null || TargetBox == null) return;

        var flag = CommandsService.GetIncognitoArgument(TargetBox.Text);
        IncognitoHint.Text = flag != null
            ? $"Navegador reconocido: al abrirlo se anadira {flag}."
            : "Solo se aplica a navegadores reconocidos (Chrome, Edge, Firefox, Brave, Vivaldi, Opera).";
    }

    // ===================== Examinar =====================

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona el ejecutable",
            Filter = "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            TargetBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void BrowseWorkingDir_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog { Description = "Selecciona la carpeta de trabajo" };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            WorkingDirBox.Text = dialog.SelectedPath;
        }
    }

    // ===================== Guardar =====================

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, "El comando necesita un nombre.", "Falta informacion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var target = TargetBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(target))
        {
            MessageBox.Show(this, "Indica el comando o la ruta del ejecutable.", "Falta informacion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!CommandsService.TargetExists(target))
        {
            var proceed = MessageBox.Show(
                this,
                "No se ha encontrado ese comando o ruta en este equipo. ¿Guardarlo igualmente?",
                "No se ha podido verificar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (proceed != MessageBoxResult.Yes) return;
        }

        // El id del icono extraido es el id del comando, asi el archivo queda ligado a el.
        var command = _editingCommand ?? new CommandShortcut { Id = _iconId };
        command.Name = NameBox.Text.Trim();
        command.Category = (CommandCategory)CategoryCombo.SelectedIndex;
        command.Target = target;
        command.Arguments = ArgumentsBox.Text.Trim();
        command.WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirBox.Text) ? null : WorkingDirBox.Text.Trim();
        command.Icon = _selectedIcon;
        command.IconImagePath = _iconImagePath;
        command.Terminal = (TerminalHost)TerminalCombo.SelectedIndex;
        command.RunAsAdmin = RunAsAdminCheck.IsChecked == true;
        command.Incognito = IncognitoCheck.IsChecked == true;

        Result = command;
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
