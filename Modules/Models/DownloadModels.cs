using Launcher.Core.Mvvm;

namespace Launcher.Modules.Models;

public enum DownloadState { Queued, Downloading, Verifying, Paused, Completed, Failed, Cancelled }

/// <summary>Una descarga de la cola. Solo se crea tras la confirmacion del usuario (ConfirmDownloadWindow).</summary>
public class DownloadItem : ViewModelBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string VersionName { get; set; } = "";
    public string TypeLabel { get; set; } = "";
    public long ModelId { get; set; }
    public long VersionId { get; set; }
    public string Url { get; set; } = "";

    /// <summary>Ruta final del archivo. Nunca se sobrescribe un archivo existente.</summary>
    public string Destination { get; set; } = "";

    public string? Sha256 { get; set; }
    public List<string> TrainedWords { get; set; } = new();

    /// <summary>De donde viene la peticion (p.ej. la referencia ausente de un wildcard).</summary>
    public string SourceNote { get; set; } = "";

    public DateTime Created { get; set; } = DateTime.Now;

    private DownloadState _state = DownloadState.Queued;
    private long _total;
    private long _received;
    private double _speed;
    private string? _error;

    public DownloadState State
    {
        get => _state;
        set
        {
            if (!SetField(ref _state, value)) return;
            OnPropertyChanged(nameof(StateLabel));
            OnPropertyChanged(nameof(StateLevel));
            OnPropertyChanged(nameof(CanPause));
            OnPropertyChanged(nameof(CanResume));
            OnPropertyChanged(nameof(CanRetry));
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(CanMoveToEnd));
            OnPropertyChanged(nameof(CanRemove));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(SpeedText));
        }
    }

    public long TotalBytes
    {
        get => _total;
        set { if (SetField(ref _total, value)) { OnPropertyChanged(nameof(ProgressPercent)); OnPropertyChanged(nameof(ProgressText)); } }
    }

    public long ReceivedBytes
    {
        get => _received;
        set { if (SetField(ref _received, value)) { OnPropertyChanged(nameof(ProgressPercent)); OnPropertyChanged(nameof(ProgressText)); } }
    }

    public double SpeedBps
    {
        get => _speed;
        set { if (SetField(ref _speed, value)) OnPropertyChanged(nameof(SpeedText)); }
    }

    public string? Error
    {
        get => _error;
        set { if (SetField(ref _error, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(_error);

    public double ProgressPercent => _total > 0 ? Math.Clamp(_received * 100.0 / _total, 0, 100) : 0;

    public string ProgressText => _total > 0
        ? $"{ModelEntry.FormatSize(_received)} / {ModelEntry.FormatSize(_total)}"
        : ModelEntry.FormatSize(_received);

    public string SpeedText => _state == DownloadState.Downloading && _speed > 0
        ? $"{ModelEntry.FormatSize((long)_speed)}/s"
        : "";

    public string StateLabel => _state switch
    {
        DownloadState.Queued => "EN COLA",
        DownloadState.Downloading => "DESCARGANDO",
        DownloadState.Verifying => "VERIFICANDO",
        DownloadState.Paused => "PAUSADA",
        DownloadState.Completed => "COMPLETADA",
        DownloadState.Failed => "ERROR",
        _ => "CANCELADA"
    };

    /// <summary>0 = neutro, 1 = bien, 2 = aviso, 3 = error. Color en la vista.</summary>
    public int StateLevel => _state switch
    {
        DownloadState.Downloading or DownloadState.Verifying or DownloadState.Completed => 1,
        DownloadState.Paused => 2,
        DownloadState.Failed => 3,
        _ => 0
    };

    public bool CanPause => _state is DownloadState.Queued or DownloadState.Downloading;
    public bool CanResume => _state == DownloadState.Paused;
    public bool CanRetry => _state is DownloadState.Failed or DownloadState.Cancelled;
    public bool CanCancel => _state is not (DownloadState.Completed or DownloadState.Cancelled);
    public bool CanMoveToEnd => _state is DownloadState.Queued or DownloadState.Paused;
    public bool CanRemove => _state is DownloadState.Completed or DownloadState.Failed or DownloadState.Cancelled or DownloadState.Paused;
    public bool IsCompleted => _state == DownloadState.Completed;
}

/// <summary>Lo que se pide a la cola tras confirmar.</summary>
public record DownloadRequest(CivitaiModel Model, CivitaiVersion Version, CivitaiFile File, string Destination, string SourceNote);
