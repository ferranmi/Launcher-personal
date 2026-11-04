using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Launcher.Core.Mvvm;
using Microsoft.Win32;

namespace Launcher.Modules.Models;

/// <summary>Reglas de destino de las descargas: por tipo (carpeta principal configurada), por categoria y, solo
/// en Personajes, por origen. Nunca se crean carpetas por coleccion.</summary>
public static class DownloadPlanner
{
    public const string CharactersCategory = "Personajes";

    public static ModelType? TypeOf(CivitaiModel model) => model.Type switch
    {
        "LORA" or "DoRA" => ModelType.Lora,
        "LoCon" => ModelType.LyCoris,
        "Checkpoint" => ModelType.Checkpoint,
        _ => null
    };

    private static string Safe(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Trim().TrimEnd('.');
    }

    public static string? ResolveFolder(ModelType type, string category, string origin)
    {
        var root = ModelsSettings.Instance.FoldersFor(type).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(root)) return null;

        if (type == ModelType.Checkpoint) return root; // los checkpoints van planos, sin categorias

        var folder = root;
        if (!string.IsNullOrWhiteSpace(category))
        {
            folder = Path.Combine(folder, Safe(category));

            if (string.Equals(category, CharactersCategory, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(origin))
            {
                folder = Path.Combine(folder, Safe(origin));
            }
        }

        return folder;
    }

    public static string SuggestCategory(CivitaiModel model, IReadOnlyList<string> categories)
    {
        string? Find(string wanted) => categories.FirstOrDefault(c => string.Equals(c, wanted, StringComparison.OrdinalIgnoreCase));

        var tags = string.Join(' ', model.Tags).ToLowerInvariant();

        if (tags.Contains("character")) return Find("Personajes") ?? categories.FirstOrDefault() ?? "";
        if (tags.Contains("pose")) return Find("Poses") ?? categories.FirstOrDefault() ?? "";
        if (tags.Contains("clothing") || tags.Contains("outfit")) return Find("Ropa") ?? categories.FirstOrDefault() ?? "";
        if (tags.Contains("style")) return Find("Estilos") ?? categories.FirstOrDefault() ?? "";
        if (tags.Contains("action")) return Find("Acciones") ?? categories.FirstOrDefault() ?? "";

        return Find("Otros") ?? categories.LastOrDefault() ?? "";
    }
}

/// <summary>Una fila de la confirmacion: un modelo, la version elegida, su archivo, su destino y su estado.</summary>
public class DownloadCandidate : ViewModelBase
{
    private readonly ConfirmDownloadWindow _window;
    private CivitaiVersion? _version;
    private bool _include;
    private string _finalPath = "";
    private string _statusText = "";
    private int _statusLevel;
    private bool _canDownload;

    public CivitaiModel Model { get; }
    public List<CivitaiVersion> Versions { get; }

    /// <summary>Carpeta elegida a mano con "Cambiar..." (sustituye a la calculada).</summary>
    public string? OverrideFolder { get; set; }

    public DownloadCandidate(ConfirmDownloadWindow window, CivitaiModel model, long? preferredVersionId)
    {
        _window = window;
        Model = model;
        Versions = model.ModelVersions.Where(v => v.Files.Count > 0).ToList();
        if (Versions.Count == 0) Versions = model.ModelVersions.ToList();

        _version = Versions.FirstOrDefault(v => v.Id == preferredVersionId) ?? Versions.FirstOrDefault();
        Recompute(resetInclude: true);
    }

    public string ModelName => Model.Name;
    public string TypeLabel => Model.TypeLabel;
    public CivitaiFile? File => _version?.PrimaryFile;
    public CivitaiVersion? Version => _version;

    public CivitaiVersion? SelectedVersion
    {
        get => _version;
        set
        {
            if (!SetField(ref _version, value)) return;
            OnPropertyChanged(nameof(FileDisplay));
            Recompute(resetInclude: true);
        }
    }

    public bool Include { get => _include; set => SetField(ref _include, value); }
    public string FinalPath => _finalPath;
    public string DestinationText => _finalPath.Length == 0 ? "(sin destino)" : _finalPath;
    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }
    public int StatusLevel { get => _statusLevel; private set => SetField(ref _statusLevel, value); }
    public bool CanDownload { get => _canDownload; private set => SetField(ref _canDownload, value); }

    public string FileDisplay => File == null
        ? "esta version no tiene archivos descargables"
        : $"{File.Display}" + (string.IsNullOrEmpty(_version?.BaseModel) ? "" : $"   \u00b7   base: {_version!.BaseModel}");

    private void SetResult(string path, string status, int level, bool can, bool? include)
    {
        _finalPath = path;
        OnPropertyChanged(nameof(FinalPath));
        OnPropertyChanged(nameof(DestinationText));
        StatusText = status;
        StatusLevel = level;
        CanDownload = can;
        if (include.HasValue) Include = include.Value;
        else if (!can) Include = false;
    }

    public void Recompute(bool resetInclude)
    {
        var file = File;
        var url = file?.DownloadUrl ?? _version?.DownloadUrl;

        if (file == null || string.IsNullOrEmpty(url))
        {
            SetResult("", "SIN ARCHIVO DESCARGABLE: Civitai no ofrece esta version (archivada o retirada).", 3, false, false);
            return;
        }

        var type = DownloadPlanner.TypeOf(Model);
        if (type == null)
        {
            SetResult("", $"TIPO NO SOPORTADO ({Model.Type}): el Launcher solo gestiona LoRA, LyCORIS y Checkpoints.", 3, false, false);
            return;
        }

        var folder = OverrideFolder ?? DownloadPlanner.ResolveFolder(type.Value, _window.Category, _window.Origin);
        if (folder == null)
        {
            SetResult("", $"CONFIGURA LA CARPETA de {Model.TypeLabel} en Configuracion antes de descargar.", 3, false, false);
            return;
        }

        var installed = ModelCatalogService.Instance.Models.FirstOrDefault(m =>
            string.Equals(m.FileName, file.Name, StringComparison.OrdinalIgnoreCase));

        // El tamano de la API viene en KB: se tolera el redondeo.
        if (installed != null && Math.Abs(installed.SizeBytes - file.SizeBytes) < 2048)
        {
            SetResult(installed.FullPath,
                $"YA INSTALADA (misma version) en {installed.DisplayFolder}: se mantiene donde esta, no se descarga de nuevo.",
                1, false, false);
            return;
        }

        var path = Path.Combine(folder, file.Name);
        var conflict = System.IO.File.Exists(path) || installed != null;

        if (conflict)
        {
            var baseName = Path.GetFileNameWithoutExtension(file.Name);
            var extension = Path.GetExtension(file.Name);
            path = Path.Combine(folder, $"{baseName}_{_version!.Id}{extension}");

            for (var n = 2; System.IO.File.Exists(path); n++)
            {
                path = Path.Combine(folder, $"{baseName}_{_version.Id}_{n}{extension}");
            }

            SetResult(path,
                $"YA EXISTE un archivo con ese nombre (distinto): se conservan ambos y este se guardara como \"{Path.GetFileName(path)}\".",
                2, true, resetInclude ? true : null);
            return;
        }

        SetResult(path, "NUEVA: no existe en la biblioteca.", 1, true, resetInclude ? true : null);
    }
}

public partial class ConfirmDownloadWindow : Window
{
    private readonly ObservableCollection<DownloadCandidate> _items = new();

    public string Category => CategoryCombo.SelectedItem as string ?? "";
    public string Origin => OriginBox.Text.Trim();

    /// <summary>Filas que el usuario ha confirmado (marcadas y descargables).</summary>
    public List<DownloadCandidate> Chosen { get; private set; } = new();

    public ConfirmDownloadWindow(IReadOnlyList<(CivitaiModel Model, long? PreferredVersion)> models, string sourceNote)
    {
        InitializeComponent();

        SourceText.Text = string.IsNullOrWhiteSpace(sourceNote)
            ? "Origen: busqueda en Civitai"
            : $"Origen: {sourceNote}";
        KeyWarning.Visibility = CivitaiService.Instance.HasApiKey ? Visibility.Collapsed : Visibility.Visible;

        var categories = ModelsSettings.Instance.Categories;
        foreach (var category in categories) CategoryCombo.Items.Add(category);
        CategoryCombo.SelectedItem = models.Count > 0
            ? DownloadPlanner.SuggestCategory(models[0].Model, categories)
            : categories.FirstOrDefault();

        foreach (var (model, preferred) in models)
        {
            var candidate = new DownloadCandidate(this, model, preferred);
            candidate.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DownloadCandidate.Include)) UpdateConfirm(); };
            _items.Add(candidate);
        }

        CandidatesList.ItemsSource = _items;
        UpdateConfirm();
    }

    private void UpdateConfirm()
    {
        var count = _items.Count(c => c.Include && c.CanDownload);
        ConfirmButton.Content = $"Confirmar descarga ({count})";
        ConfirmButton.IsEnabled = count > 0;
        ConfirmButton.Opacity = count > 0 ? 1.0 : 0.45;
    }

    private void RefreshAll()
    {
        foreach (var candidate in _items) candidate.Recompute(resetInclude: false);
        UpdateConfirm();
    }

    private void Destination_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_items.Count > 0) RefreshAll();
    }

    private void OriginBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_items.Count > 0) RefreshAll();
    }

    private static DownloadCandidate? Of(object sender) => (sender as FrameworkElement)?.DataContext as DownloadCandidate;

    private void OpenCivitai_Click(object sender, RoutedEventArgs e)
    {
        if (Of(sender) is { } candidate)
        {
            Process.Start(new ProcessStartInfo(candidate.Model.Url) { UseShellExecute = true });
        }
    }

    private void Ignore_Click(object sender, RoutedEventArgs e)
    {
        if (Of(sender) is { } candidate) _items.Remove(candidate);
        UpdateConfirm();
    }

    private void ChangeDestination_Click(object sender, RoutedEventArgs e)
    {
        if (Of(sender) is not { } candidate) return;

        var dialog = new OpenFolderDialog { Title = "Selecciona la carpeta de destino" };
        if (dialog.ShowDialog(this) != true) return;

        candidate.OverrideFolder = dialog.FolderName;
        candidate.Recompute(resetInclude: false);
        UpdateConfirm();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Chosen = _items.Where(c => c.Include && c.CanDownload && c.File != null).ToList();
        if (Chosen.Count == 0) return;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
