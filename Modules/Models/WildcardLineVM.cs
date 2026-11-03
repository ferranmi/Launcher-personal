using Launcher.Core.Mvvm;

namespace Launcher.Modules.Models;

/// <summary>Una linea del editor. Guarda el texto original: asi se sabe que cambio, y una linea
/// que no se toca se escribe exactamente igual que estaba.</summary>
public class WildcardLineVM : ViewModelBase
{
    private string _text;
    private int _number;
    private LineCheck? _check;

    public WildcardLineVM(string text, int? originalIndex, string? originalText)
    {
        _text = text;
        OriginalIndex = originalIndex;
        OriginalText = originalText;
    }

    /// <summary>Posicion (0-based) en el archivo cargado; null si la linea es nueva.</summary>
    public int? OriginalIndex { get; }
    public string? OriginalText { get; }

    public bool IsNew => OriginalIndex == null;
    public bool IsModified => IsNew || !string.Equals(_text, OriginalText, StringComparison.Ordinal);
    public bool IsComment => _text.TrimStart().StartsWith('#');

    public int Number { get => _number; set => SetField(ref _number, value); }

    public string Text
    {
        get => _text;
        set
        {
            if (!SetField(ref _text, value)) return;

            // Una linea editada ya no es la que se comprobo.
            Check = null;
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(IsComment));
        }
    }

    public LineCheck? Check
    {
        get => _check;
        set
        {
            if (!SetField(ref _check, value)) return;
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(StatusLevel));
            OnPropertyChanged(nameof(StatusDetail));
            OnPropertyChanged(nameof(NeedsAttention));
        }
    }

    public string StatusLabel
    {
        get
        {
            if (_check == null) return "";

            var label = _check.Status switch
            {
                LineStatus.Ok => "OK",
                LineStatus.Missing => "AUSENTE",
                LineStatus.Ambiguous => "AMBIGUO",
                LineStatus.Malformed => "ERROR",
                LineStatus.Unknown => "?",
                _ => ""
            };

            if (_check.IsDuplicate) label = label.Length == 0 ? "DUP" : label + " DUP";
            return label;
        }
    }

    /// <summary>0 = neutro, 1 = correcto, 2 = aviso, 3 = error. Lo usa la vista para el color.</summary>
    public int StatusLevel
    {
        get
        {
            if (_check == null) return 0;

            var level = _check.Status switch
            {
                LineStatus.Ok => 1,
                LineStatus.Missing or LineStatus.Ambiguous => 2,
                LineStatus.Malformed => 3,
                _ => 0
            };

            return _check.IsDuplicate ? Math.Max(level, 2) : level;
        }
    }

    public string? StatusDetail
    {
        get
        {
            if (_check == null) return null;
            if (!_check.IsDuplicate) return _check.Detail;
            return string.IsNullOrEmpty(_check.Detail) ? "Linea duplicada." : _check.Detail + " Linea duplicada.";
        }
    }

    public bool NeedsAttention =>
        _check != null && (_check.IsDuplicate ||
                           _check.Status is LineStatus.Missing or LineStatus.Ambiguous or LineStatus.Malformed);
}
