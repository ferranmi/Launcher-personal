using System.IO;
using System.Text.Json;
using Launcher.Core.Services;

namespace Launcher.Modules.Models;

/// <summary>
/// Inventario local de modelos. SOLO LECTURA: examina las carpetas configuradas y nunca mueve,
/// renombra ni borra nada. models-index.json es una cache para mostrar algo al instante al abrir;
/// si se borra o queda desactualizada, ScanAsync la reconstruye desde el disco.
/// </summary>
public sealed class ModelCatalogService
{
    public static ModelCatalogService Instance { get; } = new();

    private static readonly string IndexPath = Path.Combine(AppSettingsService.AppFolder, "models-index.json");

    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { ".safetensors", ".ckpt", ".pt" };

    private List<ModelEntry> _models = new();

    public IReadOnlyList<ModelEntry> Models => _models;
    public DateTime? LastScan { get; private set; }
    public bool IsScanning { get; private set; }

    /// <summary>Se dispara en el hilo desde el que se llamo a LoadCache/ScanAsync (el de la UI).</summary>
    public event EventHandler? Changed;

    private ModelCatalogService() { }

    /// <summary>Carga la ultima foto guardada (sin tocar el disco de modelos).</summary>
    public void LoadCache()
    {
        try
        {
            if (!File.Exists(IndexPath)) return;

            var items = JsonSerializer.Deserialize<List<ModelEntry>>(File.ReadAllText(IndexPath));
            if (items == null) return;

            MarkDuplicates(items);
            _models = items;
            LastScan = File.GetLastWriteTime(IndexPath);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer models-index.json (se reconstruira)", ex);
        }
    }

    public async Task ScanAsync(CancellationToken ct = default)
    {
        if (IsScanning) return;
        IsScanning = true;

        try
        {
            var settings = ModelsSettings.Instance;
            var result = await Task.Run(() => ScanDisk(settings, ct), ct);

            MarkDuplicates(result);
            var before = _models.Count;
            _models = result;
            LastScan = DateTime.Now;
            SaveCache();
            Changed?.Invoke(this, EventArgs.Empty);

            if (before > 0 && before != result.Count)
            {
                ActivityLog.Add("biblioteca", $"Biblioteca: {before} -> {result.Count} modelos");
            }
        }
        catch (OperationCanceledException)
        {
            // Escaneo cancelado: se conserva el inventario anterior.
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo el escaneo de modelos", ex);
        }
        finally
        {
            IsScanning = false;
        }
    }

    private static List<ModelEntry> ScanDisk(ModelsSettings settings, CancellationToken ct)
    {
        // Clave = ruta completa. Si una carpeta esta dentro de otra (p.ej. LyCORIS dentro de Lora) el
        // mismo archivo aparece dos veces: gana la raiz mas especifica (la mas larga).
        var found = new Dictionary<string, (ModelEntry Entry, int RootLength)>(StringComparer.OrdinalIgnoreCase);

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };

        foreach (var type in Enum.GetValues<ModelType>())
        {
            foreach (var rawRoot in settings.FoldersFor(type).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string root;
                try { root = Path.GetFullPath(rawRoot).TrimEnd('\\', '/'); }
                catch { continue; }

                if (!Directory.Exists(root)) continue;

                foreach (var path in Directory.EnumerateFiles(root, "*", options))
                {
                    ct.ThrowIfCancellationRequested();

                    if (!Extensions.Contains(Path.GetExtension(path))) continue;
                    if (found.TryGetValue(path, out var existing) && existing.RootLength >= root.Length) continue;

                    FileInfo info;
                    try { info = new FileInfo(path); }
                    catch { continue; }

                    var folder = Path.GetDirectoryName(path) ?? root;
                    var relative = Path.GetRelativePath(root, folder);

                    found[path] = (new ModelEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(path),
                        FileName = info.Name,
                        FullPath = path,
                        RootFolder = root,
                        RelativeFolder = relative == "." ? "" : relative,
                        Type = type,
                        SizeBytes = info.Length,
                        Modified = info.LastWriteTime,
                        PreviewPath = ModelEntry.FindPreview(path)
                    }, root.Length);
                }
            }
        }

        return found.Values.Select(v => v.Entry).ToList();
    }

    private static void MarkDuplicates(List<ModelEntry> list)
    {
        // Los checkpoints no se referencian con etiquetas: solo compiten entre si. LoRA y LyCORIS comparten
        // espacio de nombres (ambos se llaman con <lora:nombre>).
        foreach (var group in list.GroupBy(m => $"{(m.Type == ModelType.Checkpoint ? "c" : "l")}:{m.Name}",
                     StringComparer.OrdinalIgnoreCase))
        {
            var duplicated = group.Count() > 1;
            foreach (var model in group) model.IsDuplicateName = duplicated;
        }
    }

    private void SaveCache()
    {
        try
        {
            Directory.CreateDirectory(AppSettingsService.AppFolder);
            var temp = IndexPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_models));
            File.Move(temp, IndexPath, overwrite: true);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo guardar models-index.json", ex);
        }
    }

    /// <summary>Todos los modelos con ese nombre de archivo (sin extension). Puede haber varios:
    /// quien llame decide que hacer con una coincidencia ambigua, nunca se elige en silencio.</summary>
    public List<ModelEntry> FindAllByName(string name) =>
        _models.Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
}
