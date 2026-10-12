using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Launcher.Core.Converters;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Microsoft.Win32;

namespace Launcher.Modules.Games;

/// <summary>
/// Ficha de un juego, como la pagina de un juego en la biblioteca de Steam o Epic pero
/// editable: informacion (descargada y corregible), logros de Steam, enlaces web que se abren
/// con el navegador elegido (incluido el modo privado), documentos locales y notas.
/// Todo se guarda con GameInfoStore. La informacion se descarga sola la primera vez que se abre.
/// </summary>
public partial class GameDetailsWindow : Window
{
    private sealed record BrowserOption(string Key, string Name);

    private readonly Game _game;
    private readonly GameInfo _info;
    private readonly ObservableCollection<GameLink> _links;
    private readonly ObservableCollection<GameDocument> _documents;

    private GameLink? _editingLink;
    private bool _ready;
    private bool _busy;

    /// <summary>Ultimo aviso de la descarga de logros (progreso no disponible, error...).</summary>
    private string? _achievementsNote;

    public GameDetailsWindow(Game game)
    {
        InitializeComponent();

        _game = game;
        _info = GameInfoStore.Load(game.Id);

        // En un juego de Steam el id es el suyo: no hace falta buscarlo por nombre.
        if (_info.SteamAppId == null && game.Source == GameSource.Steam &&
            int.TryParse(game.ExternalId, out var steamAppId))
        {
            _info.SteamAppId = steamAppId;
        }

        _links = new ObservableCollection<GameLink>(_info.Links);
        _documents = new ObservableCollection<GameDocument>(_info.Documents);
        LinksList.ItemsSource = _links;
        DocumentsList.ItemsSource = _documents;
        _links.CollectionChanged += (_, _) => UpdateEmptyStates();
        _documents.CollectionChanged += (_, _) => UpdateEmptyStates();

        BuildBrowserCombo();
        UpdateIncognitoUi();
        PopulateHero();
        PopulateInfoFields();
        NotesBox.Text = _info.Notes;
        RenderAchievements();
        UpdateEmptyStates();

        // Lo que el usuario escriba (informacion, notas) se guarda al cerrar.
        Closing += (_, _) => SaveAll();

        _ready = true;
        Loaded += async (_, _) => await AutoRefreshAsync();
    }

    // ===================== Ventana =====================

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not RadioButton { Tag: string tag }) return;

        InfoPanel.Visibility = tag == "Info" ? Visibility.Visible : Visibility.Collapsed;
        AchievementsPanel.Visibility = tag == "Achievements" ? Visibility.Visible : Visibility.Collapsed;
        LinksPanel.Visibility = tag == "Links" ? Visibility.Visible : Visibility.Collapsed;
        NotesPanel.Visibility = tag == "Notes" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowStatus(string message) => StatusText.Text = message;

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        RefreshInfoButton.IsEnabled = !busy;
        RefreshAchievementsButton.IsEnabled = !busy;
        StatusText.Text = message ?? "";
    }

    // ===================== Cabecera =====================

    private static string Effective(string? edited, string? fetched) => edited ?? fetched ?? "";

    private void PopulateHero()
    {
        TitleText.Text = _game.Name;

        var parts = new List<string> { _game.Source == GameSource.Manual ? "Manual" : _game.Source.ToString() };

        var developers = Effective(_info.Overrides.Developers, _info.Fetched.Developers);
        if (!string.IsNullOrWhiteSpace(developers)) parts.Add(developers);

        var release = Effective(_info.Overrides.ReleaseDate, _info.Fetched.ReleaseDate);
        if (!string.IsNullOrWhiteSpace(release)) parts.Add(release);

        SubtitleText.Text = string.Join("  \u00b7  ", parts);

        var played = _game.PlayTimeMinutes <= 0
            ? "sin jugar"
            : _game.PlayTimeMinutes < 60 ? $"{_game.PlayTimeMinutes} min" : $"{_game.PlayTimeMinutes / 60.0:0.#} h";

        StatsText.Text = _game.LastPlayed is { } last
            ? $"Tiempo jugado: {played}  \u00b7  Ultima vez: {last:dd/MM/yyyy}"
            : $"Tiempo jugado: {played}";

        if (_game.HasCover)
        {
            CoverImage.Source = new PathToBitmapConverter()
                .Convert(_game.CoverImagePath, typeof(ImageSource), null, CultureInfo.InvariantCulture) as ImageSource;
            CoverInitial.Text = "";
        }
        else
        {
            CoverImage.Source = null;
            CoverInitial.Text = string.IsNullOrWhiteSpace(_game.Name) ? "?" : _game.Name.Trim()[..1].ToUpperInvariant();
        }

        StoreButton.Visibility = _info.SteamAppId != null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (!GameLibraryService.Instance.Launch(_game))
        {
            ShowStatus("No se ha podido lanzar el juego. Comprueba la ruta del ejecutable (Juegos > Editar).");
        }
    }

    private void OpenStore_Click(object sender, RoutedEventArgs e)
    {
        var url = _info.Fetched.StoreUrl ?? (_info.SteamAppId is { } id ? $"https://store.steampowered.com/app/{id}" : null);
        if (!GameLinkLauncher.OpenUrl(url)) ShowStatus("No se ha podido abrir la tienda.");
    }

    // ===================== Informacion =====================

    private void PopulateInfoFields()
    {
        DescriptionBox.Text = Effective(_info.Overrides.Description, _info.Fetched.Description);
        GenresBox.Text = Effective(_info.Overrides.Genres, _info.Fetched.Genres);
        DevelopersBox.Text = Effective(_info.Overrides.Developers, _info.Fetched.Developers);
        PublishersBox.Text = Effective(_info.Overrides.Publishers, _info.Fetched.Publishers);
        ReleaseBox.Text = Effective(_info.Overrides.ReleaseDate, _info.Fetched.ReleaseDate);
        SteamIdBox.Text = _info.SteamAppId?.ToString(CultureInfo.InvariantCulture) ?? "";

        ScoreText.Text = _info.Fetched.MetacriticScore is { } score ? $"{score}/100 (Metacritic)" : "sin datos";

        InfoSourceText.Text = _info.Fetched.FetchedAt is { } fetchedAt
            ? $"Informacion de la Steam Store, actualizada el {fetchedAt:dd/MM/yyyy HH:mm}. Lo que edites aqui se conserva al actualizar."
            : "Todavia no hay informacion descargada.";

        ScreenshotsList.ItemsSource = _info.Fetched.Screenshots;
        ScreenshotsLabel.Visibility = _info.Fetched.Screenshots.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Un campo solo cuenta como editado si difiere de lo descargado.</summary>
    private static string? Diff(string text, string? fetched)
    {
        var edited = text.Trim();
        return edited == (fetched ?? "").Trim() ? null : edited;
    }

    /// <summary>Vuelca los campos de la pantalla en la ficha (sin guardar a disco).</summary>
    private void CollectFields()
    {
        _info.Overrides.Description = Diff(DescriptionBox.Text, _info.Fetched.Description);
        _info.Overrides.Genres = Diff(GenresBox.Text, _info.Fetched.Genres);
        _info.Overrides.Developers = Diff(DevelopersBox.Text, _info.Fetched.Developers);
        _info.Overrides.Publishers = Diff(PublishersBox.Text, _info.Fetched.Publishers);
        _info.Overrides.ReleaseDate = Diff(ReleaseBox.Text, _info.Fetched.ReleaseDate);
        _info.Notes = NotesBox.Text;

        var idText = SteamIdBox.Text.Trim();
        int? newId = int.TryParse(idText, out var parsed) && parsed > 0 ? parsed : null;

        if (newId != _info.SteamAppId)
        {
            _info.SteamAppId = newId;

            // Los logros guardados eran de otro id: ya no valen.
            _info.Achievements = new List<AchievementInfo>();
            _info.AchievementsFetchedAt = null;
            _info.AchievementsHavePlayerData = false;
            _achievementsNote = null;
        }
    }

    private void SaveAll()
    {
        CollectFields();
        _info.Links = _links.ToList();
        _info.Documents = _documents.ToList();
        GameInfoStore.Save(_info);
    }

    /// <summary>Guarda enlaces y documentos al momento (sin tocar lo que se este escribiendo).</summary>
    private void PersistLists()
    {
        _info.Links = _links.ToList();
        _info.Documents = _documents.ToList();
        GameInfoStore.Save(_info);
    }

    private async void RefreshInfo_Click(object sender, RoutedEventArgs e) => await RefreshInfoAsync();

    private async Task RefreshInfoAsync()
    {
        if (_busy) return;
        SetBusy(true, "Buscando informacion...");

        try
        {
            // Primero se recoge lo escrito: no se pierde al refrescar, y el ID manual se respeta.
            CollectFields();

            var appId = _info.SteamAppId;
            if (appId == null)
            {
                appId = await SteamInfoService.FindAppIdAsync(_game.Name);
                _info.SteamAppId = appId;
            }

            if (appId == null)
            {
                SetBusy(false, "No se ha encontrado este juego en Steam. Si esta, escribe su ID en el campo de arriba.");
                PopulateHero();
                PopulateInfoFields();
                return;
            }

            var fetched = await SteamInfoService.FetchDetailsAsync(appId.Value);
            if (fetched == null)
            {
                SetBusy(false, "Steam no ha devuelto informacion de ese juego. Comprueba el ID o tu conexion.");
                PopulateInfoFields();
                return;
            }

            _info.Fetched = fetched;
            GameInfoStore.Save(_info);

            PopulateHero();
            PopulateInfoFields();
            RenderAchievements();
            SetBusy(false, "Informacion actualizada.");
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo al actualizar la informacion del juego", ex);
            SetBusy(false, "No se ha podido actualizar la informacion.");
        }
    }

    private void Screenshot_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ScreenshotInfo shot) GameLinkLauncher.OpenUrl(shot.FullUrl);
    }

    /// <summary>Al abrir la ficha por primera vez descarga la informacion sola; los logros solo
    /// si ya hay clave y nunca se habian descargado (despues, con el boton).</summary>
    private async Task AutoRefreshAsync()
    {
        if (_info.Fetched.FetchedAt == null) await RefreshInfoAsync();

        if (_info.AchievementsFetchedAt == null &&
            _info.SteamAppId != null &&
            LibraryPathsService.Instance.HasSteamApiKey)
        {
            await RefreshAchievementsAsync();
        }
    }

    // ===================== Logros =====================

    private async void RefreshAchievements_Click(object sender, RoutedEventArgs e) => await RefreshAchievementsAsync();

    private async void ConfigureSteam_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SteamAccountWindow { Owner = this };
        dialog.ShowDialog();

        if (dialog.Saved && LibraryPathsService.Instance.HasSteamApiKey && _info.SteamAppId != null)
        {
            await RefreshAchievementsAsync();
        }
        else
        {
            RenderAchievements();
        }
    }

    private async Task RefreshAchievementsAsync()
    {
        if (_busy) return;

        CollectFields(); // por si el usuario acaba de cambiar el ID de Steam

        var paths = LibraryPathsService.Instance;

        if (_info.SteamAppId is not int appId || !paths.HasSteamApiKey)
        {
            RenderAchievements();
            return;
        }

        if (string.IsNullOrWhiteSpace(paths.SteamId))
        {
            var detected = SteamInfoService.TryDetectSteamId64();
            if (detected != null)
            {
                paths.SteamId = detected;
                paths.Save();
            }
        }

        if (string.IsNullOrWhiteSpace(paths.SteamId))
        {
            _achievementsNote = "No se ha podido detectar tu SteamID64. Indicalo en la configuracion de la cuenta de Steam.";
            RenderAchievements(needsConfig: true);
            return;
        }

        SetBusy(true, "Descargando logros...");

        try
        {
            var result = await SteamInfoService.FetchAchievementsAsync(appId, paths.SteamApiKey!, paths.SteamId!);

            if (result.Items.Count > 0)
            {
                _info.Achievements = result.Items;
                _info.AchievementsFetchedAt = DateTime.Now;
                _info.AchievementsHavePlayerData = result.HasPlayerData;
                GameInfoStore.Save(_info);
            }

            _achievementsNote = result.Error ?? result.PlayerWarning;
            RenderAchievements();
            SetBusy(false, result.Items.Count > 0 ? "Logros actualizados." : "");
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo al descargar los logros", ex);
            _achievementsNote = "No se han podido descargar los logros.";
            RenderAchievements();
            SetBusy(false);
        }
    }

    private void RenderAchievements(bool needsConfig = false)
    {
        var items = _info.Achievements;
        var paths = LibraryPathsService.Instance;

        if (items.Count == 0)
        {
            AchievementsSummaryPanel.Visibility = Visibility.Collapsed;
            AchievementsList.ItemsSource = null;

            var prefix = _game.Source == GameSource.Epic
                ? "Epic no permite leer logros desde otras aplicaciones. "
                : "";

            string message;
            if (_info.SteamAppId == null)
            {
                message = prefix + (_game.Source == GameSource.Epic
                    ? "Si el juego tambien esta en Steam, escribe su ID en la pestana Informacion."
                    : "Este juego no esta enlazado con Steam. Si esta en Steam, escribe su ID en la pestana Informacion.");
            }
            else if (!paths.HasSteamApiKey)
            {
                needsConfig = true;
                message = prefix + "Para ver los logros hace falta una clave gratuita de la Web API de Steam.";
            }
            else
            {
                message = _achievementsNote ?? (prefix + "Pulsa \"Actualizar logros\" para descargarlos.");
            }

            AchievementsMessage.Text = message;
            ConfigureSteamButton.Visibility = needsConfig ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        var haveProgress = _info.AchievementsHavePlayerData;
        foreach (var item in items) item.ProgressUnknown = !haveProgress;

        // Primero los desbloqueados (los mas recientes arriba); despues los demas, de mas a menos comunes.
        var sorted = items
            .OrderByDescending(a => a.Achieved)
            .ThenByDescending(a => a.UnlockTime ?? DateTime.MinValue)
            .ThenByDescending(a => a.GlobalPercent ?? -1)
            .ToList();

        AchievementsList.ItemsSource = sorted;

        if (haveProgress)
        {
            var unlocked = items.Count(a => a.Achieved);
            AchievementsSummary.Text = $"{unlocked} / {items.Count} desbloqueados ({unlocked * 100.0 / items.Count:0} %)";
            AchievementsBar.Value = unlocked * 100.0 / items.Count;
        }
        else
        {
            AchievementsSummary.Text = $"{items.Count} logros (progreso no disponible)";
            AchievementsBar.Value = 0;
        }

        AchievementsSummaryPanel.Visibility = Visibility.Visible;
        AchievementsMessage.Text = _achievementsNote ?? "";
        ConfigureSteamButton.Visibility = Visibility.Collapsed;
    }

    // ===================== Enlaces =====================

    private void BuildBrowserCombo()
    {
        var options = new List<BrowserOption> { new("", "Predeterminado del sistema") };
        options.AddRange(GameLinkLauncher.InstalledBrowsers().Select(b => new BrowserOption(b.Key, b.Name)));

        BrowserCombo.ItemsSource = options;
        BrowserCombo.SelectedIndex = 0;
    }

    private string SelectedBrowserKey() => (BrowserCombo.SelectedItem as BrowserOption)?.Key ?? "";

    private void BrowserCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) UpdateIncognitoUi();
    }

    /// <summary>La casilla de ventana privada solo se ofrece si el navegador elegido (o el
    /// predeterminado) tiene modo privado conocido.</summary>
    private void UpdateIncognitoUi()
    {
        var flag = GameLinkLauncher.GetIncognitoFlag(SelectedBrowserKey());

        if (flag == null)
        {
            IncognitoPanel.Visibility = Visibility.Collapsed;
            LinkIncognitoCheck.IsChecked = false;
            return;
        }

        IncognitoPanel.Visibility = Visibility.Visible;
        IncognitoHint.Text = $"Anade {flag} al abrir el enlace.";
    }

    private void SaveLink_Click(object sender, RoutedEventArgs e)
    {
        var url = LinkUrlBox.Text.Trim();
        if (url.Length == 0)
        {
            ShowStatus("Escribe la direccion del enlace.");
            return;
        }

        if (!url.Contains("://", StringComparison.Ordinal)) url = "https://" + url;

        if (!GameLinkLauncher.TryParseWebUrl(url, out var uri))
        {
            ShowStatus("La direccion no es valida (solo http o https).");
            return;
        }

        var browser = SelectedBrowserKey();
        var title = LinkTitleBox.Text.Trim();

        var link = new GameLink
        {
            Id = _editingLink?.Id ?? Guid.NewGuid().ToString("N"),
            Title = title.Length > 0 ? title : uri.Host,
            Url = uri.AbsoluteUri,
            Browser = browser,
            Incognito = LinkIncognitoCheck.IsChecked == true && GameLinkLauncher.GetIncognitoFlag(browser) != null
        };

        if (_editingLink != null)
        {
            var index = _links.IndexOf(_editingLink);
            if (index >= 0) _links[index] = link; else _links.Add(link);
        }
        else
        {
            _links.Add(link);
        }

        ResetLinkForm();
        PersistLists();
        ShowStatus("Enlace guardado.");
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e) => ResetLinkForm();

    private void ResetLinkForm()
    {
        _editingLink = null;
        LinkFormTitle.Text = "Anadir enlace";
        SaveLinkButton.Content = "Anadir";
        CancelEditButton.Visibility = Visibility.Collapsed;

        LinkTitleBox.Text = "";
        LinkUrlBox.Text = "";
        LinkIncognitoCheck.IsChecked = false;
        BrowserCombo.SelectedIndex = 0;
        UpdateIncognitoUi();
    }

    private void LinkOpen_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not GameLink link) return;

        if (!GameLinkLauncher.TryOpen(link, out var error))
        {
            ShowStatus($"No se ha podido abrir el enlace: {error}");
        }
    }

    private void LinkEdit_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not GameLink link) return;

        _editingLink = link;
        LinkFormTitle.Text = "Editar enlace";
        SaveLinkButton.Content = "Guardar cambios";
        CancelEditButton.Visibility = Visibility.Visible;

        LinkTitleBox.Text = link.Title;
        LinkUrlBox.Text = link.Url;

        var options = (IEnumerable<BrowserOption>)BrowserCombo.ItemsSource;
        BrowserCombo.SelectedItem = options.FirstOrDefault(o => o.Key == link.Browser) ?? options.First();

        UpdateIncognitoUi();
        LinkIncognitoCheck.IsChecked = link.Incognito && IncognitoPanel.Visibility == Visibility.Visible;
    }

    private void LinkRemove_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not GameLink link) return;

        var result = MessageBox.Show(this, $"Se quitara el enlace \"{link.Title}\" de la ficha.",
            "Quitar enlace", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        if (ReferenceEquals(_editingLink, link)) ResetLinkForm();
        _links.Remove(link);
        PersistLists();
    }

    // ===================== Documentos =====================

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Selecciona un archivo", Filter = "Todos los archivos (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true) AddDocument(dialog.FileName);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Selecciona una carpeta" };
        if (dialog.ShowDialog(this) == true) AddDocument(dialog.FolderName);
    }

    private void AddDocument(string path)
    {
        if (_documents.Any(d => string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            ShowStatus("Ese archivo o carpeta ya esta anadido.");
            return;
        }

        var title = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        _documents.Add(new GameDocument { Title = string.IsNullOrWhiteSpace(title) ? path : title, Path = path });
        PersistLists();
    }

    private void DocOpen_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not GameDocument document) return;

        if (!document.Exists)
        {
            ShowStatus("Ese archivo o carpeta ya no existe en esa ruta.");
            return;
        }

        if (!GameLinkLauncher.TryOpenPath(document.Path, out var error))
        {
            ShowStatus($"No se ha podido abrir: {error}");
        }
    }

    private void DocRemove_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not GameDocument document) return;

        var result = MessageBox.Show(this,
            $"Se quitara \"{document.Title}\" de la ficha. Esto no borra el archivo ni la carpeta.",
            "Quitar documento", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        _documents.Remove(document);
        PersistLists();
    }

    private void UpdateEmptyStates()
    {
        LinksEmpty.Visibility = _links.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DocumentsEmpty.Visibility = _documents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
