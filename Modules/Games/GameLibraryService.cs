using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Launcher.Core.Interfaces;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.Games;

/// <summary>
/// Fuente de verdad de la biblioteca de juegos. Persiste en JSON en
/// %AppData%\Launcher\games.json y expone una ObservableCollection para que la
/// UI reaccione a altas/bajas al instante. SyncAsync fusiona lo detectado por los
/// proveedores (Steam, Epic) sin pisar ediciones manuales del usuario.
///
/// Es un singleton: GamesView y Configuracion comparten la misma instancia.
/// </summary>
public class GameLibraryService
{
    private static readonly string AppFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Launcher");

    private static readonly string LibraryPath = Path.Combine(AppFolder, "games.json");
    private static readonly string IgnoredPath = Path.Combine(AppFolder, "ignored-games.json");

    // Debe ir despues de los campos estaticos de arriba: se inicializan en orden.
    public static GameLibraryService Instance { get; } = new();

    public ObservableCollection<Game> Games { get; } = new();

    // Juegos de Steam/Epic que el usuario elimino: la sincronizacion no debe volver a anadirlos.
    private readonly HashSet<string> _ignored = new(StringComparer.OrdinalIgnoreCase);

    public int IgnoredCount => _ignored.Count;

    private GameLibraryService()
    {
        Directory.CreateDirectory(AppFolder);
        Load();
        LoadIgnored();
    }

    private static string KeyOf(GameSource source, string? externalId) => $"{source}:{externalId}";

    private void Load()
    {
        Games.Clear();
        if (!File.Exists(LibraryPath)) return;

        try
        {
            var json = File.ReadAllText(LibraryPath);
            var items = JsonSerializer.Deserialize<List<Game>>(json) ?? new List<Game>();
            foreach (var g in items) Games.Add(g);
        }
        catch (Exception ex)
        {
            // Biblioteca corrupta o ilegible: arrancamos vacios en vez de reventar la app.
            LogService.Error("No se pudo leer games.json", ex);
        }
    }

    private void LoadIgnored()
    {
        if (!File.Exists(IgnoredPath)) return;

        try
        {
            var keys = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(IgnoredPath)) ?? new List<string>();
            foreach (var key in keys) _ignored.Add(key);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer ignored-games.json", ex);
        }
    }

    private void SaveIgnored()
    {
        try
        {
            File.WriteAllText(IgnoredPath, JsonSerializer.Serialize(_ignored.ToList()));
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo guardar ignored-games.json", ex);
        }
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(Games.ToList(), options));
    }

    public void Add(Game game)
    {
        Games.Add(game);
        Save();
    }

    /// <summary>Game notifica sus propios cambios via INotifyPropertyChanged (la UI
    /// ya se entera sola); esto solo persiste a disco.</summary>
    public void Update(Game _) => Save();

    public void Remove(Game game)
    {
        // Importante: esto solo quita la entrada de la biblioteca. Nunca borra
        // el ejecutable ni los archivos del juego en disco.
        Games.Remove(game);

        // Si venia de Steam/Epic, lo recordamos para que la siguiente sincronizacion no lo reanada.
        if (game.Source != GameSource.Manual && !string.IsNullOrWhiteSpace(game.ExternalId))
        {
            if (_ignored.Add(KeyOf(game.Source, game.ExternalId))) SaveIgnored();
        }

        Save();
    }

    /// <summary>Olvida los juegos eliminados: volveran a aparecer en la proxima sincronizacion.</summary>
    public void ClearIgnored()
    {
        _ignored.Clear();
        SaveIgnored();
    }

    /// <summary>Fusiona los juegos detectados por los proveedores con la biblioteca
    /// local: anade los nuevos, no toca los que ya existen (respeta ediciones locales)
    /// y se salta los que el usuario elimino.</summary>
    public async Task SyncAsync(IEnumerable<IGameProvider> providers)
    {
        foreach (var provider in providers)
        {
            List<Game> detected;
            try
            {
                detected = await provider.DetectGamesAsync();
            }
            catch (Exception ex)
            {
                LogService.Error($"Proveedor {provider.Source} fallo al sincronizar", ex);
                continue; // un proveedor roto no debe tumbar la sincronizacion de los demas
            }

            foreach (var found in detected)
            {
                if (_ignored.Contains(KeyOf(found.Source, found.ExternalId))) continue;

                var exists = Games.Any(g => g.Source == found.Source && g.ExternalId == found.ExternalId);
                if (!exists) Games.Add(found);
            }
        }

        Save();
    }

    /// <summary>Vuelve a buscar la portada de los juegos que no tienen ninguna valida
    /// (archivo borrado, descarga fallida en su dia...). Devuelve cuantas ha recuperado.</summary>
    public async Task<int> RepairMissingCoversAsync()
    {
        var covers = new CoverArtService();
        var repaired = 0;

        foreach (var game in Games.Where(g => !g.HasCover).ToList())
        {
            string? path;

            if (game.Source == GameSource.Steam && int.TryParse(game.ExternalId, out var appId))
            {
                path = await covers.DownloadByAppIdAsync(appId, $"steam_{game.ExternalId}");
            }
            else if (game.Source == GameSource.Epic && !string.IsNullOrWhiteSpace(game.ExternalId))
            {
                path = await covers.SearchAndDownloadAsync(game.Name, $"epic_{game.ExternalId}");
            }
            else
            {
                path = await covers.SearchAndDownloadAsync(game.Name, game.Id);
            }

            if (path != null)
            {
                game.CoverImagePath = path;
                repaired++;
            }
        }

        if (repaired > 0) Save();
        return repaired;
    }

    /// <summary>Lanza el juego. Soporta una ruta de ejecutable normal o un protocolo
    /// (steam://..., com.epicgames.launcher://...). Devuelve false si no se pudo lanzar
    /// (ruta invalida o error); true tambien si el usuario cancela el aviso de administrador.</summary>
    public bool Launch(Game game, bool asAdmin = false)
    {
        var target = game.ExecutablePath;
        var isProtocol = target.Contains("://", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(target) || (!isProtocol && !File.Exists(target)))
        {
            LogService.Info($"Ruta no valida para '{game.Name}': {target}");
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        };

        if (!isProtocol)
        {
            startInfo.Arguments = game.Arguments;
            startInfo.WorkingDirectory = Path.GetDirectoryName(target);
            if (asAdmin) startInfo.Verb = "runas";
        }

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return true; // el usuario cancelo el aviso de UAC: no es un error
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo lanzar '{game.Name}'", ex);
            return false;
        }

        game.LastPlayed = DateTime.Now;
        Save();

        // Solo podemos medir el tiempo si tenemos el proceso del juego (no con steam:// ni protocolos).
        if (process != null && !isProtocol) TrackPlayTime(game, process);

        return true;
    }

    /// <summary>Espera en segundo plano a que el juego termine y suma el tiempo jugado.
    /// Sesiones de menos de un minuto se ignoran (suelen ser launchers que relanzan el juego).
    /// Si el Launcher se cierra mientras juegas, esa sesion no se cuenta.</summary>
    private void TrackPlayTime(Game game, Process process)
    {
        var started = DateTime.Now;

        _ = Task.Run(() =>
        {
            try
            {
                process.WaitForExit();
            }
            catch
            {
                return; // p.ej. proceso elevado al que no tenemos acceso
            }
            finally
            {
                process.Dispose();
            }

            var minutes = (int)Math.Round((DateTime.Now - started).TotalMinutes);
            if (minutes < 1) return;

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                game.PlayTimeMinutes += minutes;
                Save();
            });
        });
    }
}