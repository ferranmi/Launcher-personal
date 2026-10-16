namespace Launcher.Core.Models;

/// <summary>Gravedad de un estado. El orden importa: Max() devuelve el peor.
/// UPLINK no esta aqui a proposito: es un evento, no una gravedad.</summary>
public enum StatusLevel
{
    Online = 0,
    Warning = 1,
    Defcon = 2
}

/// <summary>Una linea del registro global (app y, mas adelante, Windows/Uplink).</summary>
public record StatusEvent(DateTime Time, StatusLevel Level, string Source, string Message, bool IsUplink = false)
{
    /// <summary>Etiqueta en minuscula; la vista puede pasarla a mayusculas.
    /// Una entrada de uplink (conexion/desconexion) usa su propia etiqueta azul,
    /// salvo que sea un problema (Warning), que conserva el color de gravedad.</summary>
    public string Tag => IsUplink && Level == StatusLevel.Online
        ? "uplink"
        : Level switch
        {
            StatusLevel.Defcon => "defcon",
            StatusLevel.Warning => "warning",
            _ => "online"
        };
}

/// <summary>Resultado de una evaluacion: estado global + estado de cada metrica.</summary>
public record StatusReport(StatusLevel Global, IReadOnlyDictionary<string, StatusLevel> Metrics);

/// <summary>
/// Umbrales de una metrica (en % de uso). Las muestras son consecutivas (1 muestra ~ 1 s
/// con el timer actual). Margin es la histeresis: para bajar de nivel el valor debe caer
/// Margin puntos por debajo del umbral, durante CalmSamples muestras seguidas.
/// </summary>
public record MetricThresholds(
    double Warn,
    double Defcon,
    int WarnSamples = 3,
    int DefconSamples = 5,
    int CalmSamples = 5,
    double Margin = 10);
