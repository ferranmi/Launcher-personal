using System.Windows;
using System.Windows.Input;

namespace Launcher.Modules.Models;

/// <summary>Una linea del resumen de cambios: "+" anadida/nueva, "-" eliminada/antigua.</summary>
public record ChangeLine(string Kind, string Text);

/// <summary>Vista previa obligatoria antes de escribir un wildcard: nada se guarda hasta pulsar "Guardar cambios".</summary>
public partial class ConfirmChangesWindow : Window
{
    private const int MaxShown = 600;

    public ConfirmChangesWindow(string wildcardName, IReadOnlyList<ChangeLine> changes)
    {
        InitializeComponent();

        var added = changes.Count(c => c.Kind == "+");
        var removed = changes.Count(c => c.Kind == "-");
        SummaryText.Text = $"__{wildcardName}__   \u00b7   {added} lineas con \"+\"   \u00b7   {removed} lineas con \"-\"";

        var shown = changes.Take(MaxShown).ToList();
        if (changes.Count > MaxShown)
        {
            shown.Add(new ChangeLine("~", $"... y {changes.Count - MaxShown} cambios mas (se guardaran igualmente)"));
        }

        ChangesList.ItemsSource = shown;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
