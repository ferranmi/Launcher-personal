namespace Launcher.Core.Models;

/// <summary>Un dispositivo de la tailnet de Tailscale (este equipo o un peer). Se
/// reconstruye entero en cada refresco, asi que no necesita INotifyPropertyChanged.</summary>
public class NetworkDevice
{
    public string Name { get; init; } = "";

    /// <summary>Destino para "tailscale file cp": nombre MagicDNS o, si no hay, la IP.</summary>
    public string Target { get; init; } = "";

    /// <summary>Primera IPv4 de Tailscale (100.x.y.z). Tambien es la direccion donde
    /// se busca LocalSend, porque funciona igual en casa que fuera.</summary>
    public string Ip { get; init; } = "";

    public string Os { get; init; } = "";
    public bool IsSelf { get; init; }
    public bool IsOnline { get; init; }
    public DateTime? LastSeen { get; init; }

    public bool CanSend => IsOnline && !IsSelf && !string.IsNullOrWhiteSpace(Ip);

    public string OnlineLabel => IsSelf ? "este equipo" : IsOnline ? "online" : "offline";

    public string Detail => IsOnline || LastSeen == null
        ? Os
        : $"{Os}  \u00B7  visto {LastSeen.Value.ToLocalTime():dd/MM HH:mm}";
}
