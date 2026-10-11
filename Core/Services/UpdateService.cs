using System.IO;
using Velopack;

namespace Launcher.Core.Services;

public enum UpdateStatus { Unknown, NotConfigured, NotInstalled, UpToDate, Available, Error }

/// <summary>
/// Busca y aplica actualizaciones con Velopack. Solo funciona en una copia instalada con
/// el instalador (Setup.exe); ejecutando desde Visual Studio el estado es NotInstalled.
/// Cada comprobacion deja en el registro (Configuracion > Datos > Registro de errores)
/// que carpeta mira, que archivos hay y que ha encontrado.
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
        LogService.Info($"[Actualizaciones] Buscando en: '{source}'");

        if (string.IsNullOrWhiteSpace(source))
        {
            LogService.Info("[Actualizaciones] No hay carpeta de versiones configurada.");
            SetStatus(UpdateStatus.NotConfigured);
            Notify();
            return;
        }

        IsChecking = true;
        Notify();

        try
        {
            LogSourceDiagnostics(source);

            _manager = new UpdateManager(source);

            LogService.Info(
                $"[Actualizaciones] Instalada con Velopack: {_manager.IsInstalled}; " +
                $"version actual: {_manager.CurrentVersion?.ToString() ?? "(ninguna)"}");

            if (!_manager.IsInstalled)
            {
                LogService.Info("[Actualizaciones] Esta copia no esta instalada con el instalador: no se puede actualizar.");
                _available = null;
                SetStatus(UpdateStatus.NotInstalled);
            }
            else
            {
                _available = await _manager.CheckForUpdatesAsync();

                if (_available == null)
                {
                    LogService.Info("[Actualizaciones] Sin version nueva en esa carpeta.");
                    SetStatus(UpdateStatus.UpToDate);
                }
                else
                {
                    var version = _available.TargetFullRelease.Version.ToString();
                    LogService.Info($"[Actualizaciones] Version nueva encontrada: {version}");
                    SetStatus(UpdateStatus.Available, version);
                }
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

    /// <summary>Anota en el registro desde donde se ejecuta la app y que archivos hay en la
    /// carpeta de versiones. Nunca lanza: es solo informacion para diagnosticar.</summary>
    private static void LogSourceDiagnostics(string source)
    {
        try
        {
            LogService.Info($"[Actualizaciones] Ejecutable: {Environment.ProcessPath}");

            if (Directory.Exists(source))
            {
                var files = Directory.GetFiles(source).Select(Path.GetFileName);
                LogService.Info($"[Actualizaciones] Archivos en la carpeta: {string.Join(", ", files)}");
            }
            else
            {
                LogService.Info("[Actualizaciones] La carpeta de versiones no existe (o no es una carpeta local).");
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo inspeccionar la carpeta de versiones", ex);
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
            LogService.Info($"[Actualizaciones] Descargando {_available.TargetFullRelease.Version}...");
            await _manager.DownloadUpdatesAsync(_available);

            // Quitamos el icono de la bandeja antes de salir: si no, queda un icono fantasma.
            WindowBehaviorService.Instance?.Cleanup();

            LogService.Info("[Actualizaciones] Aplicando y reiniciando.");
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