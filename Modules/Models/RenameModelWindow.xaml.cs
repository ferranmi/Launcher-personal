using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Launcher.Modules.Models;

/// <summary>Dialogo de renombrado con simulacion. No cambia nada: solo valida el nuevo nombre, construye el
/// plan (archivo + acompanantes) y muestra que lineas de wildcards quedarian afectadas.</summary>
public partial class RenameModelWindow : Window
{
    private const int MaxSampleRefs = 6;

    private readonly ModelEntry _entry;
    private readonly IReadOnlyList<ModelEntry> _models;
    private readonly List<WildcardRefLocation> _refs;
    private readonly Dictionary<string, string[]> _fileLines = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _isCheckpoint;
    private readonly bool _othersKeepOldName;

    public RenamePlan? Plan { get; private set; }
    public string NewName { get; private set; } = "";
    public bool PrepareCorrections => PrepareCheck.IsEnabled && PrepareCheck.IsChecked == true;

    public RenameModelWindow(ModelEntry entry, List<WildcardRefLocation> refs, IReadOnlyList<ModelEntry> models)
    {
        InitializeComponent();

        _entry = entry;
        _models = models;
        _isCheckpoint = entry.Type == ModelType.Checkpoint;
        _refs = _isCheckpoint ? new List<WildcardRefLocation>() : refs;

        // Si otro archivo conserva el nombre antiguo, las etiquetas siguen resolviendo a ese: no hay nada que corregir.
        _othersKeepOldName = !_isCheckpoint && models.Any(m =>
            m.Type != ModelType.Checkpoint &&
            !string.Equals(m.FullPath, entry.FullPath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(m.Name, entry.Name, StringComparison.OrdinalIgnoreCase));

        CurrentNameText.Text = $"{entry.Name}{Path.GetExtension(entry.FullPath)}   ({entry.TypeLabel})";
        ExtText.Text = Path.GetExtension(entry.FullPath);

        if (_refs.Count == 0)
        {
            PrepareCheck.IsEnabled = false;
            PrepareCheck.IsChecked = false;
            NoteText.Text = _isCheckpoint
                ? "Los checkpoints no se llaman con etiquetas en los wildcards."
                : "Ningun wildcard hace referencia a este modelo.";
        }
        else if (_othersKeepOldName)
        {
            PrepareCheck.IsEnabled = false;
            PrepareCheck.IsChecked = false;
            NoteText.Text = "Otro archivo conserva el nombre actual, asi que las referencias seguiran funcionando con ese.";
        }
        else
        {
            NoteText.Text = "No se modifica ningun wildcard ahora: la correccion queda en Referencias > Pendientes " +
                            "para que la revises y la apliques tu.";
        }

        foreach (var path in _refs.Select(r => r.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxSampleRefs))
        {
            try { _fileLines[path] = WildcardService.ReadLines(path); }
            catch { /* sin muestra de esa linea */ }
        }

        NewNameBox.Text = entry.Name;
        NewNameBox.Focus();
        NewNameBox.SelectAll();

        UpdatePreview();
    }

    private string? Validate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Escribe un nombre.";
        if (text != text.Trim()) return "El nombre no puede empezar ni acabar con espacios.";
        if (text.EndsWith('.')) return "El nombre no puede acabar en punto.";

        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '|', '{', '}', '"', ',' }).Distinct().ToArray();
        if (text.IndexOfAny(invalid) >= 0) return "Caracteres no permitidos: \\ / : * ? \" < > | { } ,";

        if (string.Equals(text, _entry.Name, StringComparison.Ordinal)) return "Es el mismo nombre que ya tiene.";

        var clash = _models.FirstOrDefault(m =>
            !string.Equals(m.FullPath, _entry.FullPath, StringComparison.OrdinalIgnoreCase) &&
            (m.Type == ModelType.Checkpoint) == _isCheckpoint &&
            string.Equals(m.Name, text, StringComparison.OrdinalIgnoreCase));

        return clash == null
            ? null
            : $"Ya hay otro modelo llamado \"{text}\" ({clash.DisplayFolder}): una etiqueta seria ambigua.";
    }

    private void UpdatePreview()
    {
        var text = NewNameBox.Text;
        var error = Validate(text);

        Plan = null;
        PlanList.Items.Clear();

        if (error == null)
        {
            var plan = ModelRenamer.BuildPlan(_entry, text);
            if (plan.Error != null) error = plan.Error;
            else Plan = plan;
        }

        ErrorText.Text = error ?? "";
        RenameButton.IsEnabled = Plan != null;
        RenameButton.Opacity = Plan != null ? 1.0 : 0.45;

        if (Plan != null)
        {
            foreach (var move in Plan.Moves)
            {
                PlanList.Items.Add($"{(move.IsMain ? "Archivo      " : "Acompanante  ")}{Path.GetFileName(move.From)}   ->   {Path.GetFileName(move.To)}");
            }
        }

        if (_refs.Count > 0)
        {
            var files = _refs.Select(r => r.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            PlanList.Items.Add("");
            PlanList.Items.Add($"Referencias en wildcards: {_refs.Count} en {files} archivo(s). " +
                               (_othersKeepOldName
                                   ? "(siguen resolviendo al otro archivo con el nombre actual)"
                                   : "Con el nombre nuevo quedarian sin resolver hasta corregirlas."));

            if (Plan != null && !_othersKeepOldName)
            {
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [_entry.Name] = text };

                foreach (var reference in _refs.Take(MaxSampleRefs))
                {
                    if (!_fileLines.TryGetValue(reference.FullPath, out var lines) ||
                        reference.LineNumber < 1 || reference.LineNumber > lines.Length) continue;

                    var line = lines[reference.LineNumber - 1];
                    PlanList.Items.Add($"{reference.FileName}  L{reference.LineNumber}:   {line}");
                    PlanList.Items.Add($"        corregida:   {ReferenceRewriter.Rewrite(line, map)}");
                }

                if (_refs.Count > MaxSampleRefs) PlanList.Items.Add($"... y {_refs.Count - MaxSampleRefs} referencias mas");
            }
        }
    }

    private void NewNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PlanList != null) UpdatePreview(); // se dispara durante InitializeComponent
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (Plan == null) return;

        NewName = NewNameBox.Text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
