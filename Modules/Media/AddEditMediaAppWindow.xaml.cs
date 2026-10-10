using System.IO;
using System.Windows;
using System.Windows.Input;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Microsoft.Win32;

namespace Launcher.Modules.Media;

public partial class AddEditMediaAppWindow : Window
{
    private readonly MediaAppShortcut? _editingApp;
    private readonly string _iconId;

    /// <summary>Rellenado con el resultado (nueva MediaAppShortcut o la misma
    /// actualizada) si el usuario guarda.</summary>
    public MediaAppShortcut? Result { get; private set; }

    /// <summary>Constructor para anadir una app nueva.</summary>
    public AddEditMediaAppWindow()
    {
        InitializeComponent();
        _iconId = Guid.NewGuid().ToString("N");
    }

    /// <summary>Constructor para editar una app existente.</summary>
    public AddEditMediaAppWindow(MediaAppShortcut app) : this()
    {
        _editingApp = app;
        _iconId = app.Id;
        HeaderText.Text = "> Editar app";

        NameBox.Text = app.Name;
        PathBox.Text = app.ExecutablePath;
        ArgumentsBox.Text = app.Arguments;
        IconBox.Text = app.IconImagePath ?? "";
    }

    private void BrowseExecutable_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona el ejecutable",
            Filter = "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            PathBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    /// <summary>Igual que en Juegos: busca el .exe principal en la carpeta
    /// elegida y extrae su icono, para no tener que localizar el ejecutable a
    /// mano en apps instaladas en carpetas con muchos archivos.</summary>
    private void DetectFromFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Selecciona la carpeta de la app" };
        if (dialog.ShowDialog(this) != true) return;

        var folderPath = dialog.FolderName;
        var exePath = IconExtractionService.FindLikelyExecutable(folderPath);

        if (exePath == null)
        {
            FolderDetectStatus.Text = "no se encontro ningun .exe en la carpeta, indicalo a mano";
            return;
        }

        PathBox.Text = exePath;

        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameBox.Text = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
        }

        var iconPath = IconExtractionService.ExtractAsCover(exePath, _iconId);
        if (iconPath != null)
        {
            IconBox.Text = iconPath;
            FolderDetectStatus.Text = "ejecutable e icono detectados_";
        }
        else
        {
            FolderDetectStatus.Text = "ejecutable detectado (sin icono extraible)";
        }
    }

    private void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona el icono",
            Filter = "Imagenes (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            IconBox.Text = dialog.FileName;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, "La app necesita un nombre.", "Falta informacion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(PathBox.Text))
        {
            MessageBox.Show(this, "Indica la ruta del ejecutable.", "Falta informacion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var app = _editingApp ?? new MediaAppShortcut { Id = _iconId };
        app.Name = NameBox.Text.Trim();
        app.ExecutablePath = PathBox.Text.Trim();
        app.Arguments = ArgumentsBox.Text.Trim();
        app.IconImagePath = string.IsNullOrWhiteSpace(IconBox.Text) ? null : IconBox.Text.Trim();

        Result = app;
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
