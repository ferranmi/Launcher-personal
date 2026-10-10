using System.IO;
using System.Windows;
using System.Windows.Input;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Microsoft.Win32;

namespace Launcher.Modules.Games;

public partial class AddEditGameWindow : Window
{
    private readonly Game? _editingGame;
    private readonly string _coverId;
    private readonly CoverArtService _coverArtService = new();

    /// <summary>Rellenado con el resultado (nuevo Game o el mismo actualizado) si el usuario guarda.</summary>
    public Game? Result { get; private set; }

    /// <summary>Constructor para anadir un juego nuevo.</summary>
    public AddEditGameWindow()
    {
        InitializeComponent();
        _coverId = Guid.NewGuid().ToString("N");
    }

    /// <summary>Constructor para editar un juego existente.</summary>
    public AddEditGameWindow(Game game) : this()
    {
        _editingGame = game;
        _coverId = game.Id;
        HeaderText.Text = "> Editar juego";

        NameBox.Text = game.Name;
        PathBox.Text = game.ExecutablePath;
        ArgumentsBox.Text = game.Arguments;
        CoverBox.Text = game.CoverImagePath ?? "";
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

    /// <summary>Alternativa a "Examinar" en el ejecutable: el usuario elige la
    /// carpeta de instalacion del juego y aqui se busca el .exe principal en su
    /// raiz (descartando instaladores/redistribuibles conocidos) y se extrae su
    /// icono para usarlo como portada provisional.</summary>
    private void DetectFromFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Selecciona la carpeta del juego" };
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

        var coverPath = IconExtractionService.ExtractAsCover(exePath, _coverId);
        if (coverPath != null)
        {
            CoverBox.Text = coverPath;
            FolderDetectStatus.Text = "ejecutable e icono detectados_";
        }
        else
        {
            FolderDetectStatus.Text = "ejecutable detectado (sin icono extraible)";
        }
    }

    private void BrowseCover_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona la portada",
            Filter = "Imagenes (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            CoverBox.Text = dialog.FileName;
        }
    }

    private async void AutoCover_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            AutoCoverStatus.Text = "escribe primero el nombre";
            return;
        }

        AutoCoverButton.IsEnabled = false;
        AutoCoverStatus.Text = "buscando...";

        var path = await _coverArtService.SearchAndDownloadAsync(NameBox.Text.Trim(), _coverId, force: true);

        if (path != null)
        {
            CoverBox.Text = path;
            AutoCoverStatus.Text = "encontrada_";
        }
        else
        {
            AutoCoverStatus.Text = "sin resultados, prueba a poner la ruta a mano";
        }

        AutoCoverButton.IsEnabled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, "El juego necesita un nombre.", "Falta informacion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(PathBox.Text))
        {
            MessageBox.Show(this, "Indica la ruta del ejecutable.", "Falta informacion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var game = _editingGame ?? new Game { Id = _coverId };
        game.Name = NameBox.Text.Trim();
        game.ExecutablePath = PathBox.Text.Trim();
        game.Arguments = ArgumentsBox.Text.Trim();
        game.CoverImagePath = string.IsNullOrWhiteSpace(CoverBox.Text) ? null : CoverBox.Text.Trim();

        Result = game;
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
