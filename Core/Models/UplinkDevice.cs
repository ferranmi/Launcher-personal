namespace Launcher.Core.Models;

public enum UplinkKind { Headphones, Microphone, Gamepad, Drive, Keyboard, Mouse, Camera, Other }

/// <summary>Wireless = inalambrico sin saber si es Bluetooth (p.ej. receptor propio de un mando).</summary>
public enum UplinkConnection { Unknown, Bluetooth, Wired, Wireless }

/// <summary>Connected = presente; Active = en uso (suena / el micro captura);
/// Transferring = unidad con lectura/escritura sostenida; Problem = presente pero con aviso.</summary>
public enum UplinkState { Connected, Active, Transferring, Problem }

/// <summary>
/// Un dispositivo externo detectado. Es un record: dos lecturas iguales son iguales,
/// asi la vista solo se actualiza cuando algo cambia de verdad.
/// </summary>
public record UplinkDevice(
    string Id,
    string Name,
    UplinkKind Kind,
    UplinkConnection Connection,
    UplinkState State,
    string Detail = "")
{
    /// <summary>Glifo de Segoe MDL2 Assets para la vista.</summary>
    public string Icon => Kind switch
    {
        UplinkKind.Headphones => "\uE7F6",
        UplinkKind.Microphone => "\uE720",
        UplinkKind.Gamepad => "\uE7FC",
        UplinkKind.Keyboard => "\uE765",
        UplinkKind.Mouse => "\uE962",
        UplinkKind.Camera => "\uE722",
        UplinkKind.Other => "\uE772",
        _ => "\uE88E"
    };

    public string ConnectionLabel => Connection switch
    {
        UplinkConnection.Bluetooth => "bluetooth",
        UplinkConnection.Wired => "cable",
        UplinkConnection.Wireless => "inalambrico",
        _ => ""
    };

    public string StateLabel => State switch
    {
        UplinkState.Active => "en uso",
        UplinkState.Transferring => "transfiriendo",
        UplinkState.Problem => "problema",
        _ => "conectado"
    };

    /// <summary>Linea lista para el panel: "WH-1000XM5 · bluetooth · micro en uso (Discord)".</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string> { Name };
            if (ConnectionLabel.Length > 0) parts.Add(ConnectionLabel);
            parts.Add(string.IsNullOrWhiteSpace(Detail) ? StateLabel : Detail);
            return string.Join(" \u00B7 ", parts);
        }
    }
}
