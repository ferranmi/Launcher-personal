using System.Text.Json.Serialization;

namespace Launcher.Modules.Models;

public enum RefState { Installed, OtherLocation, Missing, Ambiguous, Ignored }

/// <summary>Una referencia unica (nombre de modelo tal como aparece en las etiquetas) y su estado.</summary>
public class ReferenceItem
{
    public string Name { get; set; } = "";
    public RefState State { get; set; }
    public List<WildcardRefLocation> Locations { get; set; } = new();
    public List<ModelEntry> Matches { get; set; } = new();

    /// <summary>Nombre al que hay una correccion preparada (aun sin aplicar), si existe.</summary>
    public string? PendingNewName { get; set; }

    public int FileCount => Locations.Select(l => l.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public string UsageText => $"{Locations.Count} en {FileCount} archivos";

    public string StateLabel
    {
        get
        {
            var label = State switch
            {
                RefState.Installed => "INSTALADO",
                RefState.OtherLocation => "OTRA UBICACION",
                RefState.Missing => "AUSENTE",
                RefState.Ambiguous => "AMBIGUO",
                _ => "IGNORADO"
            };

            return PendingNewName == null ? label : label + "  \u00b7  CORRECCION PREPARADA";
        }
    }

    /// <summary>0 = neutro, 1 = correcto, 2 = aviso. Lo usa la vista para el color.</summary>
    public int StateLevel => State switch
    {
        RefState.Installed or RefState.OtherLocation => 1,
        RefState.Missing or RefState.Ambiguous => 2,
        _ => 0
    };

    public string LocationText => State switch
    {
        RefState.Ambiguous => $"{Matches.Count} archivos con este nombre",
        RefState.Missing => "no esta en la biblioteca",
        RefState.Ignored => "ignorado por el usuario",
        _ => Matches.Count > 0 ? $"{Matches[0].TypeLabel}  \u00b7  {Matches[0].DisplayFolder}" : ""
    };
}

/// <summary>Cambio de nombre de referencia ya decidido pero NO aplicado: ningun wildcard se toca hasta que
/// el usuario lo revise y lo confirme en Referencias > Pendientes.</summary>
public class PendingCorrection
{
    public string OldName { get; set; } = "";
    public string NewName { get; set; } = "";
    public DateTime Created { get; set; } = DateTime.Now;

    /// <summary>"rename" (tras renombrar un archivo) o "manual" (elegida por el usuario).</summary>
    public string Source { get; set; } = "manual";

    [JsonIgnore]
    public string Summary { get; set; } = "";
}

/// <summary>Avisos entre paneles que no se conocen entre si.</summary>
public static class ModelsEvents
{
    public static event Action? PendingChanged;
    public static void RaisePendingChanged() => PendingChanged?.Invoke();
}
