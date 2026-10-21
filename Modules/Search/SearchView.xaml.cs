using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Launcher.Core.Services;

namespace Launcher.Modules.Search;

public partial class SearchView : UserControl
{
    private readonly SearchService _service = new();
    private readonly ObservableCollection<SearchResult> _results = new();
    private CancellationTokenSource? _cts;

    /// <summary>En el modulo Buscar la barra recibe el foco al entrar; como widget del Dashboard se pone a false.</summary>
    public bool FocusOnLoad { get; set; } = true;

    public SearchView()
    {
        InitializeComponent();
        ResultsList.ItemsSource = _results;

        // Loaded se dispara cada vez que se vuelve a este modulo: foco directo a la barra.
        Loaded += (_, _) => { if (FocusOnLoad) { QueryBox.Focus(); QueryBox.SelectAll(); } };
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
            HintText.Text = "> escribe para buscar_";
            HintText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            await Task.Delay(150, ct); // debounce: no lanzar una busqueda por cada tecla
            var found = await _service.SearchAsync(query, ct);
            if (ct.IsCancellationRequested) return;

            _results.Clear();
            foreach (var r in found) _results.Add(r);

            HintText.Text = "> sin resultados_";
            HintText.Visibility = _results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_results.Count > 0) ResultsList.SelectedIndex = 0;
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
                ExecuteSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                QueryBox.Clear();
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_results.Count == 0) return;
        var next = Math.Clamp(ResultsList.SelectedIndex + delta, 0, _results.Count - 1);
        ResultsList.SelectedIndex = next;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ExecuteSelected();

    private void ExecuteSelected()
    {
        if (ResultsList.SelectedItem is not SearchResult result) return;

        try
        {
            result.Execute();
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo ejecutar el resultado '{result.Title}'", ex);
            MessageBox.Show(Window.GetWindow(this), $"No se ha podido abrir \"{result.Title}\".",
                "Buscar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
