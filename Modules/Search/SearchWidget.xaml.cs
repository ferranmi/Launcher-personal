using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Launcher.Core.Services;

namespace Launcher.Modules.Search;

/// <summary>
/// Buscador del Dashboard: una barra compacta (cabe en una caja pequena) cuyos resultados salen en un
/// desplegable. Usa el mismo SearchService que el modulo Buscar. Flechas para moverse, Enter o clic para
/// abrir (Ctrl+Enter o Ctrl+clic: mostrar en su carpeta), Esc para cerrar el desplegable (y, con el desplegable cerrado, para limpiar la barra).
/// </summary>
public partial class SearchWidget : UserControl
{
    private readonly SearchService _service = new();
    private readonly ObservableCollection<SearchResult> _results = new();
    private CancellationTokenSource? _cts;

    public SearchWidget()
    {
        InitializeComponent();
        ResultsList.ItemsSource = _results;

        // El Dashboard se saca del arbol visual al cambiar de modulo: no dejar busquedas ni el desplegable abiertos.
        Unloaded += (_, _) =>
        {
            _cts?.Cancel();
            ResultsPopup.IsOpen = false;
        };
    }

    private async void QueryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var query = QueryBox.Text.Trim();
        if (query.Length == 0)
        {
            _results.Clear();
            ResultsPopup.IsOpen = false;
            return;
        }

        try
        {
            await Task.Delay(150, ct); // debounce: no lanzar una busqueda por cada tecla
            var found = await _service.SearchAsync(query, ct);
            if (ct.IsCancellationRequested) return;

            _results.Clear();
            foreach (var r in found) _results.Add(r);

            if (_results.Count > 0) ResultsList.SelectedIndex = 0;
            ResultsPopup.IsOpen = _results.Count > 0 && QueryBox.IsKeyboardFocusWithin;
        }
        catch (OperationCanceledException)
        {
            // Llego una busqueda mas nueva: esta se descarta.
        }
    }

    private void QueryBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                ExecuteSelected(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)); // Ctrl+Enter: mostrar en carpeta
                e.Handled = true;
                break;
            case Key.Escape:
                if (ResultsPopup.IsOpen) ResultsPopup.IsOpen = false;
                else QueryBox.Clear();
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_results.Count == 0) return;

        if (!ResultsPopup.IsOpen) ResultsPopup.IsOpen = true;

        var next = Math.Clamp(ResultsList.SelectedIndex + delta, 0, _results.Count - 1);
        ResultsList.SelectedIndex = next;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    /// <summary>Un solo clic sobre un resultado lo abre (el desplegable no tiene doble clic).</summary>
    private void ResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source != null && source is not ListBoxItem) source = VisualTreeHelper.GetParent(source);

        if (source is ListBoxItem { DataContext: SearchResult result })
        {
            ResultsList.SelectedItem = result;
            ExecuteSelected(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            e.Handled = true;
        }
    }

    private void ExecuteSelected(bool reveal = false)
    {
        if (ResultsList.SelectedItem is not SearchResult result) return;

        ResultsPopup.IsOpen = false;

        try
        {
            (reveal && result.Reveal != null ? result.Reveal : result.Execute)();
            QueryBox.Clear(); // listo para la siguiente busqueda
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo ejecutar el resultado '{result.Title}'", ex);
            MessageBox.Show(Window.GetWindow(this), $"No se ha podido abrir \"{result.Title}\".",
                "Buscar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
