using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Launcher.Core.Services;

namespace Launcher.Modules.ImageGen.Autocomplete;

/// <summary>
/// Autocompletado para un TextBox (como TagComplete): al escribir, consulta a los proveedores
/// con el texto anterior al cursor y muestra la lista en un Popup junto al cursor.
///
/// Teclas con la lista abierta: Abajo/Arriba mueven la seleccion, Tab o Enter la aceptan,
/// Escape la cierra. Tambien se acepta con un clic. El Popup y su ListBox se comparten entre
/// varios TextBox (prompt y negativo): solo uno tiene la lista abierta a la vez.
/// </summary>
public sealed class PromptAutocomplete
{
    private const double PopupWidth = 400; // ancho del borde + margen de la sombra

    private readonly TextBox _box;
    private readonly Popup _popup;
    private readonly ListBox _list;
    private readonly Func<IEnumerable<IAutocompleteProvider>> _providers;

    private int _replaceLength;
    private bool _suppress; // evita reabrir la lista por los cambios que hacemos nosotros

    public PromptAutocomplete(
        TextBox box, Popup popup, ListBox list, Func<IEnumerable<IAutocompleteProvider>> providers)
    {
        _box = box;
        _popup = popup;
        _list = list;
        _providers = providers;

        _box.TextChanged += (_, _) => Update();
        _box.PreviewKeyDown += Box_PreviewKeyDown;
        _box.LostKeyboardFocus += (_, _) => Close();
        _list.PreviewMouseLeftButtonUp += List_PreviewMouseLeftButtonUp;
    }

    private bool IsOpen => _popup.IsOpen && ReferenceEquals(_popup.PlacementTarget, _box);

    // ===================== Consulta =====================

    private void Update()
    {
        // Los cambios hechos por codigo (cargar parametros, etc.) no deben abrir la lista.
        if (_suppress || !_box.IsKeyboardFocusWithin) return;

        var text = _box.Text;
        var caret = Math.Min(_box.CaretIndex, text.Length);
        if (caret == 0)
        {
            Close();
            return;
        }

        var before = text[..caret];

        foreach (var provider in _providers())
        {
            AutocompleteResult? result;
            try
            {
                result = provider.GetSuggestions(before);
            }
            catch (Exception ex)
            {
                LogService.Error("Fallo un proveedor de autocompletado", ex);
                continue;
            }

            if (result is { Items.Count: > 0 })
            {
                Show(result);
                return;
            }
        }

        Close();
    }

    private void Show(AutocompleteResult result)
    {
        _replaceLength = result.ReplaceLength;
        _list.ItemsSource = result.Items;
        _list.SelectedIndex = 0;
        _list.ScrollIntoView(result.Items[0]);

        // El Popup aparece justo bajo el cursor de escritura.
        var rect = _box.GetRectFromCharacterIndex(_box.CaretIndex);
        _popup.PlacementTarget = _box;
        _popup.Placement = PlacementMode.Relative;
        _popup.HorizontalOffset = rect.IsEmpty ? 0 : Math.Max(0, Math.Min(rect.X, _box.ActualWidth - PopupWidth));
        _popup.VerticalOffset = (rect.IsEmpty ? _box.ActualHeight : rect.Bottom) + 2;
        _popup.IsOpen = true;
    }

    private void Close()
    {
        if (IsOpen) _popup.IsOpen = false;
    }

    // ===================== Teclado y raton =====================

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsOpen) return;

        switch (e.Key)
        {
            case Key.Down:
                Move(+1);
                e.Handled = true;
                break;

            case Key.Up:
                Move(-1);
                e.Handled = true;
                break;

            case Key.Tab:
            case Key.Enter:
                if (_list.SelectedItem is Suggestion selected)
                {
                    Accept(selected);
                    e.Handled = true; // sin esto Enter metería un salto de linea
                }
                break;

            case Key.Escape:
                Close();
                e.Handled = true;
                break;

            case Key.Left:
            case Key.Right:
            case Key.Home:
            case Key.End:
            case Key.PageUp:
            case Key.PageDown:
                Close(); // el cursor se mueve: la lista ya no corresponde
                break;
        }
    }

    private void Move(int delta)
    {
        var count = _list.Items.Count;
        if (count == 0) return;

        _list.SelectedIndex = (_list.SelectedIndex + delta + count) % count;
        _list.ScrollIntoView(_list.SelectedItem);
    }

    private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsOpen) return;

        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(_list, source) is ListBoxItem { DataContext: Suggestion suggestion })
        {
            Accept(suggestion);
            e.Handled = true;
        }
    }

    // ===================== Aceptar =====================

    private void Accept(Suggestion suggestion)
    {
        var text = _box.Text;
        var caret = Math.Min(_box.CaretIndex, text.Length);
        var start = Math.Max(0, caret - _replaceLength);
        var after = text[caret..];

        // No duplicamos la coma si ya hay una justo despues del cursor.
        var suffix = suggestion.Suffix;
        if (suffix.Length > 0 && after.TrimStart().StartsWith(suffix.Trim(), StringComparison.Ordinal))
        {
            suffix = "";
        }

        var insert = suggestion.Insert + suffix;

        _suppress = true;
        try
        {
            // Seleccionar y reemplazar (y no asignar Text) conserva el historial de deshacer.
            _box.Select(start, caret - start);
            _box.SelectedText = insert;
            _box.CaretIndex = start + insert.Length;
        }
        finally
        {
            _suppress = false;
        }

        Close();

        // Si lo insertado deja una sintaxis a medias ("<lora:"), seguimos sugiriendo.
        if (suggestion.Insert.EndsWith(':')) Update();
    }
}
