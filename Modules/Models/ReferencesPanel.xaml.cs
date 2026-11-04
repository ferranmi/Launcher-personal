using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

/// <summary>
/// Referencias LoRA/LyCORIS de todos los wildcards cruzadas con la biblioteca. El analisis es manual (boton)
/// y de solo lectura. Las correcciones nunca se escriben solas: se preparan, se revisan en "Pendientes" y se
/// aplican con vista previa, confirmacion y copia de seguridad.
/// </summary>
public partial class ReferencesPanel : UserControl
{
    private readonly ModelCatalogService _catalog = ModelCatalogService.Instance;
    private readonly ModelsSettings _settings = ModelsSettings.Instance;

    private Dictionary<string, List<WildcardRefLocation>> _index = new(StringComparer.OrdinalIgnoreCase);
    private List<ReferenceItem> _all = new();
    private ReferenceItem? _selected;

    private string _filter = "all";
    private string _search = "";
    private bool _ready;
    private bool _analyzed;

    /// <summary>Pide a la ventana principal mostrar un modelo en la Biblioteca.</summary>
    public event Action<string>? ShowInLibraryRequested;

    /// <summary>Pide buscar una referencia ausente en Civitai (pestana Civitai).</summary>
    public event Action<string>? SearchInCivitaiRequested;

    /// <summary>Se han escrito wildcards: el explorador de Wildcards debe refrescarse.</summary>
    public event Action? WildcardsChanged;

    public ReferencesPanel()
    {
        InitializeComponent();

        ModelsEvents.PendingChanged += RefreshPendingUi;
        _catalog.Changed += (_, _) =>
        {
            if (_analyzed) InfoText.Text = "la biblioteca ha cambiado desde el ultimo analisis: pulsa Analizar para actualizar";
        };

        _ready = true;
        RefreshPendingUi();
        ApplyMode();
        ShowDetail(null);
    }

    private Window? Owner => Window.GetWindow(this);

    private bool PendingMode => _filter == "pending";

    // ===================== Analisis =====================

    private async void Analyze_Click(object sender, RoutedEventArgs e) => await AnalyzeAsync();

    public async Task AnalyzeAsync()
    {
        if (_catalog.Models.Count == 0)
        {
            MessageBox.Show(Owner,
                "Aun no hay inventario de modelos. Configura la carpeta de A1111 y reescanea la Biblioteca.",
                "Analizar referencias", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AnalyzeButton.IsEnabled = false;
        AnalyzeButton.Content = "Analizando...";

        try
        {
            _index = await WildcardService.Instance.GetReferenceIndexAsync(force: true);
            _analyzed = true;
            BuildItems();
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo el analisis de referencias", ex);
            InfoText.Text = "no se pudieron leer los wildcards";
        }
        finally
        {
            AnalyzeButton.IsEnabled = true;
            AnalyzeButton.Content = "Analizar referencias";
        }
    }

    private bool IsMainLocation(ModelEntry model)
    {
        var main = _settings.FoldersFor(model.Type).FirstOrDefault();
        if (main == null) return true;

        try
        {
            return string.Equals(Path.GetFullPath(main).TrimEnd('\\', '/'), model.RootFolder, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static int Rank(RefState state) => state switch
    {
        RefState.Missing => 0,
        RefState.Ambiguous => 1,
        RefState.OtherLocation => 2,
        RefState.Installed => 3,
        _ => 4
    };

    /// <summary>Construye la lista a partir del indice ya leido y de la biblioteca actual (no vuelve a leer wildcards).</summary>
    private void BuildItems()
    {
        var byName = _catalog.Models
            .Where(m => m.Type != ModelType.Checkpoint)
            .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var ignored = new HashSet<string>(_settings.IgnoredReferences, StringComparer.OrdinalIgnoreCase);

        var pending = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in _settings.PendingCorrections) pending[p.OldName] = p.NewName;

        _all = _index.Select(pair =>
        {
            var matches = byName.GetValueOrDefault(pair.Key) ?? new List<ModelEntry>();

            var state = ignored.Contains(pair.Key) ? RefState.Ignored
                : matches.Count == 0 ? RefState.Missing
                : matches.Count > 1 ? RefState.Ambiguous
                : IsMainLocation(matches[0]) ? RefState.Installed
                : RefState.OtherLocation;

            return new ReferenceItem
            {
                Name = pair.Key,
                State = state,
                Locations = pair.Value,
                Matches = matches,
                PendingNewName = pending.GetValueOrDefault(pair.Key)
            };
        })
        .OrderBy(i => Rank(i.State))
        .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

        ApplyFilter();
        RefreshPendingUi();
    }

    // ===================== Filtros =====================

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text.Trim();
        if (_ready) ApplyFilter();
    }

    private void Filter_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        _filter = (string)((RadioButton)sender).Tag;
        ApplyMode();
        ApplyFilter();
    }

    private void ApplyMode()
    {
        var pending = PendingMode;
        RefsList.Visibility = pending ? Visibility.Collapsed : Visibility.Visible;
        PendingList.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
        PendingActions.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyFilter()
    {
        var selectedName = _selected?.Name;

        IEnumerable<ReferenceItem> query = _all;

        query = _filter switch
        {
            "problems" => query.Where(i => i.State is RefState.Missing or RefState.Ambiguous),
            "missing" => query.Where(i => i.State == RefState.Missing),
            "ambiguous" => query.Where(i => i.State == RefState.Ambiguous),
            "ignored" => query.Where(i => i.State == RefState.Ignored),
            _ => query
        };

        if (_search.Length > 0) query = query.Where(i => i.Name.Contains(_search, StringComparison.OrdinalIgnoreCase));

        var list = query.ToList();
        RefsList.ItemsSource = list;

        var again = selectedName == null ? null : list.FirstOrDefault(i => string.Equals(i.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        if (again != null) RefsList.SelectedItem = again;
        else ShowDetail(null);

        UpdateInfo(list.Count);
    }

    private void UpdateInfo(int shown)
    {
        if (PendingMode)
        {
            EmptyText.Visibility = _settings.PendingCorrections.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyText.Text = "> no hay correcciones pendientes_";
            return;
        }

        if (!_analyzed)
        {
            EmptyText.Visibility = Visibility.Visible;
            EmptyText.Text = "> pulsa \"Analizar referencias\" para comparar los wildcards con la biblioteca_";
            InfoText.Text = "";
            return;
        }

        EmptyText.Visibility = shown == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = "> sin resultados_";

        InfoText.Text = $"{_all.Count} referencias unicas  \u00b7  {_all.Count(i => i.State == RefState.Missing)} ausentes  \u00b7  " +
                        $"{_all.Count(i => i.State == RefState.Ambiguous)} ambiguas  \u00b7  " +
                        $"{_all.Count(i => i.State == RefState.Ignored)} ignoradas";
    }

    // ===================== Detalle =====================

    private void RefsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RefsList.SelectedItem is ReferenceItem item) ShowDetail(item);
    }

    private void ShowDetail(ReferenceItem? item)
    {
        _selected = item;

        DetailEmpty.Visibility = item == null ? Visibility.Visible : Visibility.Collapsed;
        DetailScroll.Visibility = item == null ? Visibility.Collapsed : Visibility.Visible;
        if (item == null) return;

        DetailScroll.ScrollToTop();

        DetailName.Text = item.Name;
        DetailState.Text = item.StateLabel + (item.PendingNewName == null ? "" : $"   ({item.Name} -> {item.PendingNewName})");
        DetailState.Foreground = (Brush)FindResource(item.StateLevel switch
        {
            1 => "Brush.Accent",
            2 => "Brush.Warning",
            _ => "Brush.TextDim"
        });

        var sb = new StringBuilder();
        foreach (var location in item.Locations.Take(10))
        {
            sb.AppendLine($"{location.FileName}  (linea {location.LineNumber})");
        }
        if (item.Locations.Count > 10) sb.AppendLine($"... y {item.Locations.Count - 10} mas");
        DetailUsage.Text = sb.ToString().TrimEnd();

        var hasMatches = item.Matches.Count > 0;
        MatchesSection.Visibility = hasMatches ? Visibility.Visible : Visibility.Collapsed;
        MatchesList.ItemsSource = item.Matches;
        if (item.Matches.Count > 0) MatchesList.SelectedIndex = 0;
        AmbiguousHint.Visibility = item.State == RefState.Ambiguous ? Visibility.Visible : Visibility.Collapsed;

        var isMissing = item.State == RefState.Missing;
        SimilarSection.Visibility = isMissing ? Visibility.Visible : Visibility.Collapsed;
        if (isMissing)
        {
            var similar = SimilarNames.Find(item.Name, _catalog.Models);
            SimilarList.ItemsSource = similar;
            SimilarEmpty.Visibility = similar.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            PrepareFixButton.Visibility = similar.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (similar.Count > 0) SimilarList.SelectedIndex = 0;
        }

        CivitaiButton.Visibility = item.State == RefState.Missing ? Visibility.Visible : Visibility.Collapsed;

        IgnoreButton.Content = item.State == RefState.Ignored ? "Dejar de ignorar" : "Ignorar esta referencia";
    }

    // ===================== Acciones sobre una referencia =====================

    private void ShowInLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_selected != null) ShowInLibraryRequested?.Invoke(_selected.Name);
    }

    private async void RenameMatch_Click(object sender, RoutedEventArgs e)
    {
        if (MatchesList.SelectedItem is not ModelEntry entry) return;

        var renamed = await ModelRenameFlow.RunAsync(Owner, entry);
        if (renamed == null) return;

        await _catalog.ScanAsync();
        await AnalyzeAsync();
    }

    private void PrepareFix_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null || SimilarList.SelectedItem is not ModelEntry target) return;

        _settings.AddPending(_selected.Name, target.Name, "manual"); // dispara RefreshPendingUi

        _selected.PendingNewName = target.Name;
        ApplyFilter();
        InfoText.Text = $"correccion preparada: {_selected.Name} -> {target.Name}. Revisala y aplicala en Pendientes.";
    }

    private void Civitai_Click(object sender, RoutedEventArgs e)
    {
        if (_selected != null) SearchInCivitaiRequested?.Invoke(_selected.Name);
    }

    private void Ignore_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;

        var name = _selected.Name;
        var existing = _settings.IgnoredReferences.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

        if (existing != null) _settings.IgnoredReferences.Remove(existing);
        else _settings.IgnoredReferences.Add(name);

        _settings.Save();
        BuildItems();
    }

    // ===================== Correcciones pendientes =====================

    private void RefreshPendingUi()
    {
        PendingChip.Content = $"Pendientes ({_settings.PendingCorrections.Count})";

        foreach (var correction in _settings.PendingCorrections)
        {
            correction.Summary = !_analyzed
                ? "pulsa Analizar para ver cuantas referencias afecta"
                : _index.TryGetValue(correction.OldName, out var locations) && locations.Count > 0
                    ? $"{locations.Count} referencias en {locations.Select(l => l.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).Count()} archivos"
                    : "sin referencias (ya esta corregida)";
        }

        PendingList.ItemsSource = null;
        PendingList.ItemsSource = _settings.PendingCorrections.ToList();

        if (PendingMode) UpdateInfo(0);
    }

    private void DiscardPending_Click(object sender, RoutedEventArgs e)
    {
        var chosen = PendingList.SelectedItems.Cast<PendingCorrection>().ToList();
        if (chosen.Count == 0) return;

        var answer = MessageBox.Show(Owner,
            $"Se descartaran {chosen.Count} correccion(es) preparada(s). Los wildcards no se tocan. Continuar?",
            "Descartar correcciones", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        _settings.RemovePending(chosen);
        BuildItems();
    }

    private async void ReviewApply_Click(object sender, RoutedEventArgs e)
    {
        var chosen = PendingList.SelectedItems.Cast<PendingCorrection>().ToList();
        if (chosen.Count == 0) chosen = _settings.PendingCorrections.ToList();
        if (chosen.Count == 0) return;

        ReviewApplyButton.IsEnabled = false;

        try
        {
            var plans = await ReferenceFixService.BuildPlanAsync(chosen);

            if (plans.Count == 0)
            {
                MessageBox.Show(Owner,
                    "No hay ninguna referencia que corregir: los wildcards ya no usan esos nombres.",
                    "Aplicar correcciones", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var changes = new List<ChangeLine>();
            foreach (var plan in plans)
            {
                changes.Add(new ChangeLine("~", $"== {plan.WildcardName} =="));
                changes.AddRange(plan.Changes);
            }

            var dialog = new ConfirmChangesWindow($"{plans.Count} wildcards", changes,
                $"{chosen.Count} correccion(es) en {plans.Count} archivo(s)") { Owner = Owner };
            if (dialog.ShowDialog() != true) return;

            var result = await ReferenceFixService.ApplyAsync(plans);
            LogService.Info($"Correcciones de referencias aplicadas: {result.FilesSaved} archivo(s), {result.LinesChanged} linea(s), " +
                            $"{result.Failed.Count} fallo(s)");

            if (result.Failed.Count == 0) _settings.RemovePending(chosen);

            WildcardService.Instance.InvalidateReferenceIndex();
            WildcardsChanged?.Invoke();
            await AnalyzeAsync();

            // Comprobacion de huerfanas: nada deberia seguir usando los nombres antiguos.
            var orphans = chosen.Where(c => _index.TryGetValue(c.OldName, out var l) && l.Count > 0).ToList();

            var message = new StringBuilder();
            message.AppendLine($"Se han corregido {result.LinesChanged} linea(s) en {result.FilesSaved} archivo(s). " +
                               "Antes de cada uno se creo una copia de seguridad.");

            if (result.Failed.Count > 0)
            {
                message.AppendLine().AppendLine("No se pudieron corregir:");
                foreach (var failure in result.Failed) message.AppendLine("  " + failure);
                message.AppendLine("Las correcciones se conservan en Pendientes para reintentarlas.");
            }

            if (orphans.Count > 0)
            {
                message.AppendLine().AppendLine("Siguen apareciendo referencias a:");
                foreach (var orphan in orphans) message.AppendLine("  " + orphan.OldName);
            }
            else if (result.Failed.Count == 0)
            {
                message.AppendLine().AppendLine("Comprobado: no quedan referencias con los nombres antiguos.");
            }

            MessageBox.Show(Owner, message.ToString().TrimEnd(), "Aplicar correcciones",
                MessageBoxButton.OK, result.Failed.Count > 0 || orphans.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la aplicacion de correcciones", ex);
            MessageBox.Show(Owner, $"No se pudieron aplicar las correcciones:\n{ex.Message}", "Aplicar correcciones",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ReviewApplyButton.IsEnabled = true;
        }
    }
}
