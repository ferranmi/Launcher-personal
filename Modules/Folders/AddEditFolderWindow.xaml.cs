using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Launcher.Core.Models;
using Microsoft.Win32;

namespace Launcher.Modules.Folders;

public partial class AddEditFolderWindow : Window
{
    /// <summary>Paleta reducida de iconos disponibles para una carpeta. No hace
    /// falta que cubra todos los casos: el usuario puede seguir usando el icono
    /// generico si no encuentra uno mas especifico.</summary>
    private static readonly (string Icon, string Label)[] IconOptions =
    {
        ("\uE8B7", "Carpeta"),
        ("\uE7F4", "Escritorio"),
        ("\uE8A5", "Documentos"),
        ("\uE896", "Descargas"),
        ("\uEB9F", "Imagenes"),
        ("\uEC4F", "Musica"),
        ("\uE714", "Videos"),
        ("\uE7FC", "Juegos"),
        ("\uE943", "Codigo"),
        ("\uE7B8", "Archivo"),
        ("\uE753", "Nube"),
        ("\uE734", "Favorito"),
    };

    private readonly FolderShortcut? _editingFolder;
    private string _selectedIcon = "\uE8B7";

    /// <summary>Rellenado con el resultado (nueva FolderShortcut o la misma
    /// actualizada) si el usuario guarda.</summary>
    public FolderShortcut? Result { get; private set; }

    /// <summary>True si el usuario pulso "Eliminar" en vez de "Guardar". Quien
    /// abrio el dialogo es responsable de confirmar y borrar de verdad.</summary>
    public bool DeleteRequested { get; private set; }

    /// <summary>Constructor para anadir una carpeta nueva.</summary>
    public AddEditFolderWindow()
    {
        InitializeComponent();
        BuildIconPicker();
    }

    /// <summary>Constructor para editar una carpeta existente.</summary>
    public AddEditFolderWindow(FolderShortcut folder) : this()
    {
        _editingFolder = folder;
        HeaderText.Text = "> Editar carpeta";

        NameBox.Text = folder.Name;
        PathBox.Text = folder.Path;
        SelectIcon(folder.Icon);

        // Las carpetas del sistema (sembradas al primer arranque) no se pueden
        // eliminar desde aqui, solo renombrar/cambiar de ruta o icono.
        DeleteButton.Visibility = folder.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible;
    }

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
            button.Checked += (_, _) => SelectIcon(icon);
            IconPanel.Children.Add(button);
        }

        SelectIcon(_selectedIcon);
    }

    private void SelectIcon(string icon)
    {
        _selectedIcon = icon;
        foreach (var toggle in IconPanel.Children.OfType<ToggleButton>())
        {
            // Se asigna sin disparar Checked de nuevo salvo que cambie el valor
            // (evita un bucle SelectIcon -> Checked -> SelectIcon).
            var isMatch = (string)toggle.Tag == icon;
            if (toggle.IsChecked != isMatch) toggle.IsChecked = isMatch;
        }
    }

    private void BrowsePath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Selecciona la carpeta" };

        if (!string.IsNullOrWhiteSpace(PathBox.Text) && Directory.Exists(PathBox.Text))
        {
            dialog.InitialDirectory = PathBox.Text;
        }

        if (dialog.ShowDialog(this) == true)
        {
            PathBox.Text = dialog.FolderName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.Text = Path.GetFileName(dialog.FolderName.TrimEnd(Path.DirectorySeparatorChar));
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, "La carpeta necesita un nombre.", "Falta informacion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(PathBox.Text))
        {
            MessageBox.Show(this, "Indica la ruta de la carpeta.", "Falta informacion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var folder = _editingFolder ?? new FolderShortcut();
        folder.Name = NameBox.Text.Trim();
        folder.Path = PathBox.Text.Trim();
        folder.Icon = _selectedIcon;

        Result = folder;
        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested = true;
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
