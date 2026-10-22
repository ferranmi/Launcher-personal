using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Launcher.Core.Models;
using Microsoft.Win32;

namespace Launcher.Modules.Music;

public partial class EditTrackWindow : Window
{
    private string? _newCoverPath;
    private bool _removeCover;

    // Resultado: se leen desde fuera solo si ShowDialog() devolvio true.
    public string TrackTitle { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public string Album { get; private set; } = "";
    public uint Year { get; private set; }

    /// <summary>Ruta de la imagen elegida, o null si no se ha cambiado la caratula.</summary>
    public string? NewCoverPath => _newCoverPath;

    /// <summary>True si el usuario ha pulsado "Quitar caratula".</summary>
    public bool RemoveCover => _removeCover;

    public EditTrackWindow(MusicTrack track)
    {
        InitializeComponent();

        FileText.Text = Path.GetFileName(track.FilePath);
        TitleBox.Text = track.Title;
        ArtistBox.Text = track.Artist;
        AlbumBox.Text = track.Album;
        YearBox.Text = track.Year > 0 ? track.Year.ToString() : "";
        CoverImage.Source = track.Cover;

        UpdateCoverState();
    }

    private void UpdateCoverState()
    {
        NoCoverText.Visibility = CoverImage.Source == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ChooseCover_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona la caratula",
            Filter = "Imagenes (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 320;
            bitmap.UriSource = new Uri(dialog.FileName, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            CoverImage.Source = bitmap;
            _newCoverPath = dialog.FileName;
            _removeCover = false;
            UpdateCoverState();
        }
        catch
        {
            MessageBox.Show(this, "No se ha podido leer esa imagen.", "Caratula",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RemoveCover_Click(object sender, RoutedEventArgs e)
    {
        CoverImage.Source = null;
        _newCoverPath = null;
        _removeCover = true;
        UpdateCoverState();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show(this, "La cancion necesita un titulo.", "Falta informacion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        uint year = 0;
        if (!string.IsNullOrWhiteSpace(YearBox.Text) && !uint.TryParse(YearBox.Text.Trim(), out year))
        {
            MessageBox.Show(this, "El ano debe ser un numero (por ejemplo, 2019).", "Dato no valido",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TrackTitle = TitleBox.Text.Trim();
        Artist = ArtistBox.Text.Trim();
        Album = AlbumBox.Text.Trim();
        Year = year;

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
