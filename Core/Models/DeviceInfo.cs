namespace Launcher.Core.Models;

/// <summary>Un dispositivo detectado (WMI Win32_PnPEntity) listo para mostrarse.</summary>
public class DeviceInfo
{
    public string Name { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Group { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string DeviceId { get; set; } = "";

    /// <summary>"Bluetooth", "USB" o "Sistema" (interno/virtual).</summary>
    public string Connection { get; set; } = "Sistema";

    public bool HasProblem { get; set; }

    /// <summary>Presente = conectado, salvo en Bluetooth: ahi se consulta la conexion real
    /// (un dispositivo emparejado sigue "presente" aunque este apagado).</summary>
    public bool IsConnected { get; set; } = true;

    public string? SettingsUri { get; set; }

    // App del fabricante
    public string? VendorKeyword { get; set; }
    public string? VendorLabel { get; set; }
    public string? VendorPath { get; set; }

    public bool IsExternal => Connection is "Bluetooth" or "USB" && Group != "USB (generico)";

    public bool HasVendor => VendorKeyword != null;
    public bool HasSettings => SettingsUri != null;

    public string AppButtonText => VendorPath != null ? $"Abrir {VendorLabel}" : "Asignar app...";

    public string Details
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Manufacturer) && !Manufacturer.StartsWith('(')) parts.Add(Manufacturer);
            parts.Add(Connection);
            parts.Add(HasProblem ? "con error" : IsConnected ? "conectado" : "emparejado");
            return string.Join("  \u00B7  ", parts);
        }
    }
}
