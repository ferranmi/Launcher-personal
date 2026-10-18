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

    /// <summary>Marca (p.ej. Logitech), deducida del identificador de fabricante USB/Bluetooth o del fabricante del driver.</summary>
    public string Brand { get; set; } = "";

    /// <summary>Modelo/producto tal y como lo declara el propio aparato (si Windows lo guarda y no es un nombre generico).</summary>
    public string Model { get; set; } = "";

    /// <summary>Identificador fabricante:producto en hexadecimal (p.ej. 046D:C08B), si el aparato lo tiene.</summary>
    public string VidPid { get; set; } = "";

    // App del fabricante
    public string? VendorKeyword { get; set; }
    public string? VendorLabel { get; set; }
    public string? VendorPath { get; set; }

    public bool IsExternal => Connection is "Bluetooth" or "USB" && Group != "USB (generico)";

    public bool HasVendor => VendorKeyword != null;
    public bool HasSettings => SettingsUri != null;

    public string AppButtonText => VendorPath != null ? $"Abrir {VendorLabel}" : "Asignar app...";

    /// <summary>Linea con marca y modelo (vacia si no se ha podido averiguar nada mas que el nombre).</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Brand)) parts.Add(Brand);
            if (!string.IsNullOrWhiteSpace(Model) && !Model.Equals(Name, StringComparison.OrdinalIgnoreCase)) parts.Add(Model);
            return string.Join("  \u00B7  ", parts);
        }
    }

    public bool HasSummary => Summary.Length > 0;

    public string Details
    {
        get
        {
            var parts = new List<string> { Connection };
            if (VidPid.Length > 0) parts.Add($"ID {VidPid}");
            parts.Add(HasProblem ? "con error" : IsConnected ? "conectado" : "emparejado");
            return string.Join("  \u00B7  ", parts);
        }
    }
}
