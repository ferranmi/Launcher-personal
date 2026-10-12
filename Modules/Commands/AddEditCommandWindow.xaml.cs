using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Launcher.Core.Converters;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace Launcher.Modules.Commands;

public partial class AddEditCommandWindow : Window
{
    /// <summary>Glifos de Segoe MDL2/Fluent ofrecidos para un comando. Si el usuario
    /// quiere otro, puede elegir una imagen o usar el icono de la propia app.</summary>
    private static readonly (string Glyph, string Label)[] GlyphOptions =
    {
        ("\uE756", "Terminal"),
        ("\uE713", "Ajustes"),
        ("\uE774", "Navegador / web"),
        ("\uE8B7", "Carpeta"),
        ("\uE71D", "Aplicaciones"),
        ("\uE943", "Codigo"),
        ("\uE9D9", "Diagnostico"),
        ("\uEDA2", "Disco"),
        ("\uE7F4", "Pantalla"),
        ("\uE7FC", "Juegos"),
        ("\uE8D6", "Musica"),
        ("\uE714", "Video"),
        ("\uE720", "Microfono"),
        ("\uE767", "Volumen"),
        ("\uE7F6", "Auriculares"),
        ("\uE765", "Teclado"),
        ("\uE701", "Wi-Fi"),
        ("\uE702", "Bluetooth"),
        ("\uE88E", "USB"),
        ("\uE753", "Nube"),
        ("\uE72E", "Seguridad"),
        ("\uE734", "Favorito"),
    };

    private readonly CommandShortcut? _editingCommand;
    private readonly string _iconId;

    private string _selectedGlyph = "\uE756";
    private string? _iconImagePath;

    /// <summary>True cuando el usuario ha tocado la casilla de incognito: a partir de ahi
    /// ya no se marca sola al escribir un navegador en un comando nuevo.</summary>
    private bool _incognitoTouched;

    public CommandShortcut? Result { get; private set; }

    /// <summary>Constructor para anadir un comando nuevo.</summary>
    public AddEditCommandWindow()
    {
        InitializeComponent();
        _iconId = Guid.NewGuid().ToString("N");
        BuildGlyphPicker();
        RefreshIconUi();
    }

    /// <summary>Constructor para editar un comando existente.</summary>
    public AddEditCommandWindow(CommandShortcut command) : this()
    {
        _editingCommand = command;
        _iconId = command.Id;
        HeaderText.Text = "> Editar comando";

        NameBox.Text = command.Name;
        CategoryCombo.SelectedIndex = (int)command.Category;
        TargetBox.Text = command.Target; // dispara TargetBox_TextChanged: muestra o no la casilla
        ArgumentsBox.Text = command.Arguments;
        WorkingDirBox.Text = command.WorkingDirectory ?? "";
        TerminalCombo.SelectedIndex = (int)command.Terminal;
        RunAsAdminCheck.IsChecked = command.RunAsAdmin;
        IncognitoCheck.IsChecked = command.Incognito;

        _selectedGlyph = command.Icon;
        _iconImagePath = command.HasImageIcon ? command.IconImagePath : null;
        RefreshIconUi();
    }

    // ===================== Incognito =====================

    private void TargetBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateIncognitoUi();

    private void IncognitoCheck_Click(object sender, RoutedEventArgs e) => _incognitoTouched = true;

    /// <summary>La casilla solo se ofrece si el destino es un navegador conocido. En un
    /// comando nuevo se marca sola la primera vez que se reconoce un navegador.</summary>
    private void UpdateIncognitoUi()
    {
        var flag = CommandsService.GetIncognitoArgument(TargetBox.Text);

        if (flag == null)
        {
            IncognitoPanel.Visibility = Visibility.Collapsed;
            return;
        }

        IncognitoPanel.Visibility = Visibility.Visible;
        IncognitoHint.Text = $"Anade {flag} al abrir el navegador.";

        if (_editingCommand == null && !_incognitoTouched)
        {
            IncognitoCheck.IsChecked = true;
        }
    }

    // ===================== Icono =====================

    private void BuildGlyphPicker()
    {
        foreach (var (glyph, label) in GlyphOptions)
        {
            var button = new ToggleButton
            {
                Tag = glyph,
                Content = glyph,
                ToolTip = label,
                Margin = new Thickness(0, 0, 6, 6),
                Style = (Style)FindResource("IconOptionStyle")
            };

            // Click y no Checked: solo reacciona a una eleccion del usuario, no a que
            // RefreshIconUi marque/desmarque botones por codigo.
            button.Click += (_, _) =>
            {
                _selectedGlyph = glyph;
                _iconImagePath = null; // elegir un glifo sustituye a la imagen
                IconStatus.Text = "";
                RefreshIconUi();
            };

            IconPanel.Children.Add(button);
        }
    }

    /// <summary>Refleja el estado actual (glifo o imagen) en los botones y la vista previa.</summary>
    private void RefreshIconUi()
    {
        var usingImage = _iconImagePath != null;

        foreach (var toggle in IconPanel.Children.OfType<ToggleButton>())
        {
            toggle.IsChecked = !usingImage && (string)toggle.Tag == _selectedGlyph;
        }

        IconPreviewBorder.Visibility = usingImage ? Visibility.Visible : Visibility.Collapsed;
        IconPreview.Source = usingImage
            ? new PathToBitmapConverter().Convert(_iconImagePath, typeof(ImageSource), null, CultureInfo.InvariantCulture) as ImageSource
            : null;
    }

    private void UseAppIcon_Click(object sender, RoutedEventArgs e)
    {
        // ResolveExecutable tambien encuentra navegadores que solo estan en "App Paths".
        var resolved = CommandsService.ResolveExecutable(TargetBox.Text);
        if (resolved == null)
        {
            IconStatus.Text = "no se encuentra un ejecutable del que sacar el icono (los protocolos no tienen)";
            return;
        }

        var saved = IconExtractionService.ExtractAsCover(resolved, $"cmd_{_iconId}");
        if (saved == null)
        {
            IconStatus.Text = "no se pudo extraer el icono de ese archivo";
            return;
        }

        _iconImagePath = saved;
        IconStatus.Text = "icono de la app_";
        RefreshIconUi();
    }

    private void BrowseIconImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona el icono",
            Filter = "Imagenes (*.ico;*.png;*.jpg;*.jpeg)|*.ico;*.png;*.jpg;*.jpeg|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            // Se copia a la carpeta de datos: si el usuario mueve o borra el original,
            // el icono del comando sigue funcionando.
            Directory.CreateDirectory(CoverArtService.CoversFolder);
            var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            var destination = Path.Combine(CoverArtService.CoversFolder, $"cmd_{_iconId}_custom{extension}");
            File.Copy(dialog.FileName, destination, overwrite: true);

            _iconImagePath = destination;
            IconStatus.Text = "imagen elegida_";
            RefreshIconUi();
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo copiar el icono del comando", ex);
            IconStatus.Text = "no se pudo usar esa imagen";
        }
    }

    // ===================== Resto del formulario =====================

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
                NameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
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

        var command = _editingCommand ?? new CommandShortcut { Id = _iconId };
        command.Name = NameBox.Text.Trim();
        command.Category = (CommandCategory)CategoryCombo.SelectedIndex;
        command.Target = target;
        command.Arguments = ArgumentsBox.Text.Trim();
        command.WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirBox.Text) ? null : WorkingDirBox.Text.Trim();
        command.Icon = _selectedGlyph;
        command.IconImagePath = _iconImagePath;
        command.Terminal = (TerminalHost)TerminalCombo.SelectedIndex;
        command.RunAsAdmin = RunAsAdminCheck.IsChecked == true;
        command.Incognito = IncognitoCheck.IsChecked == true && CommandsService.GetIncognitoArgument(target) != null;

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
