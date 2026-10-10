using Velopack;

namespace Launcher.Core.Services;

public enum UpdateStatus { Unknown, NotConfigured, NotInstalled, UpToDate, Available, Error }

/// <summary>
/// Busca y aplica actualizaciones con Velopack. Solo funciona en una copia instalada con
/// el instalador (Setup.exe); ejecutando desde Visual Studio el estado es NotInstalled.
/// </summary>
public sealed class UpdateService
{
    public static UpdateService Instance { get; } = new();

    /// <summary>Se dispara en el hilo de la UI cada vez que cambia el estado.</summary>
    public event EventHandler? Changed;

    public UpdateStatus Status { get; private set; } = UpdateStatus.Unknown;
    public string? AvailableVersion { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool IsChecking { get; private set; }
    public bool IsApplying { get; private set; }
    public bool IsBusy => IsChecking || IsApplying;

    private UpdateManager? _manager;
    private UpdateInfo? _available;

    private UpdateService() { }

    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);

    private void SetStatus(UpdateStatus status, string? version = null, string? error = null)
    {
        Status = status;
        AvailableVersion = version;
        ErrorMessage = error;
    }

    public async Task CheckAsync()
    {
        if (IsBusy) return;

        var source = AppSettingsService.Instance.UpdateSource;
        if (string.IsNullOrWhiteSpace(source))
        {
            SetStatus(UpdateStatus.NotConfigured);
            Notify();
            return;
        }

        IsChecking = true;
        Notify();

        try
        {
            _manager = new UpdateManager(source);

            if (!_manager.IsInstalled)
            {
                _available = null;
                SetStatus(UpdateStatus.NotInstalled);
            }
            else
            {
                _available = await _manager.CheckForUpdatesAsync();

                if (_available == null)
                    SetStatus(UpdateStatus.UpToDate);
                else
                    SetStatus(UpdateStatus.Available, _available.TargetFullRelease.Version.ToString());
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la busqueda de actualizaciones", ex);
            _available = null;
            SetStatus(UpdateStatus.Error, error: ex.Message);
        }
        finally
        {
            IsChecking = false;
            Notify();
        }
    }

    /// <summary>Descarga la version nueva, cierra la app, la instala y la vuelve a abrir.</summary>
    public async Task ApplyAsync()
    {
        if (IsBusy || _manager == null || _available == null) return;

        IsApplying = true;
        Notify();

        try
        {
            await _manager.DownloadUpdatesAsync(_available);

            // Quitamos el icono de la bandeja antes de salir: si no, queda un icono fantasma.
            WindowBehaviorService.Instance?.Cleanup();

            _manager.ApplyUpdatesAndRestart(_available.TargetFullRelease);
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo al aplicar la actualizacion", ex);
            SetStatus(UpdateStatus.Error, error: ex.Message);
            IsApplying = false;
            Notify();
        }
    }
}