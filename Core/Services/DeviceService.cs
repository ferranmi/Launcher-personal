using System.IO;
using System.Management;
using System.Text.Json;
using Launcher.Core.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Launcher.Core.Services;

/// <summary>
/// Lista los dispositivos conectados (WMI) y, para Bluetooth, consulta el estado de
/// conexion real con WinRT. Tambien resuelve la app del fabricante: tabla conocida
/// (las rutas son las de instalacion habituales y pueden variar) mas una asignacion
/// manual por fabricante guardada en device_apps.json, que tiene prioridad.
/// </summary>
public static class DeviceService
{
    private static readonly string OverridesPath = Path.Combine(AppSettingsService.AppFolder, "device_apps.json");
    private static Dictionary<string, string>? _overrides;

    private static readonly Dictionary<string, (string Group, string? Settings)> Classes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Bluetooth"] = ("Bluetooth", "ms-settings:bluetooth"),
            ["AudioEndpoint"] = ("Audio", "ms-settings:sound"),
            ["Monitor"] = ("Pantallas", "ms-settings:display"),
            ["Keyboard"] = ("Teclados", "ms-settings:devices"),
            ["Mouse"] = ("Ratones", "ms-settings:mousetouchpad"),
            ["Camera"] = ("Camaras", "ms-settings:camera"),
            ["Image"] = ("Camaras", "ms-settings:camera"),
            ["Printer"] = ("Impresoras", "ms-settings:printers"),
            ["DiskDrive"] = ("Almacenamiento", "ms-settings:disksandvolumes"),
            ["XboxComposite"] = ("Mandos", "ms-settings:devices"),
            ["HIDClass"] = ("Mandos", "ms-settings:devices"), // se filtra abajo: solo mandos
            ["USB"] = ("USB (generico)", "ms-settings:usb"),
        };

    private static readonly string[] GamepadHints =
        { "game controller", "gamepad", "mando", "juego", "xbox", "dualsense", "dualshock", "wireless controller" };

    private static readonly (string Keyword, string Label, string[] Paths)[] Vendors =
    {
        ("logitech", "Logitech G HUB", new[] { @"%ProgramFiles%\LGHUB\lghub.exe" }),
        ("razer", "Razer Synapse", new[] { @"%ProgramFiles(x86)%\Razer\Synapse3\WPFUI\Framework\Razer Synapse 3 Host\Razer Synapse 3.exe" }),
        ("corsair", "Corsair iCUE", new[]
        {
            @"%ProgramFiles%\Corsair\CORSAIR iCUE 5 Software\iCUE.exe",
            @"%ProgramFiles%\Corsair\CORSAIR iCUE 4 Software\iCUE.exe"
        }),
        ("steelseries", "SteelSeries GG", new[] { @"%ProgramFiles%\SteelSeries\GG\SteelSeriesGG.exe" }),
        ("nvidia", "NVIDIA App", new[] { @"%ProgramFiles%\NVIDIA Corporation\NVIDIA app\CEF\NVIDIA App.exe" }),
        ("hyperx", "HyperX NGENUITY", Array.Empty<string>()),
    };

    // ===================== Consulta =====================

    public static async Task<List<DeviceInfo>> QueryAsync()
    {
        var devices = await Task.Run(Query);
        var connected = await GetConnectedBluetoothNamesAsync();

        foreach (var d in devices.Where(d => d.Group == "Bluetooth" && d.Connection == "Bluetooth"))
        {
            d.IsConnected = connected.Contains(d.Name);
        }

        return devices;
    }

    /// <summary>Solo lo que es externo y esta conectado ahora: lo que enseña el UPLINK del dashboard.</summary>
    public static async Task<List<DeviceInfo>> GetExternalAsync() =>
        (await QueryAsync()).Where(d => d.IsExternal && d.IsConnected && !d.HasProblem).ToList();

    private static List<DeviceInfo> Query()
    {
        var list = new List<DeviceInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Manufacturer, PNPClass, DeviceID, ConfigManagerErrorCode " +
                "FROM Win32_PnPEntity WHERE Present = TRUE");

            foreach (ManagementObject obj in searcher.Get())
            {
                using (obj)
                {
                    var name = obj["Name"] as string;
                    var cls = obj["PNPClass"] as string;
                    var id = obj["DeviceID"] as string;
                    if (string.IsNullOrWhiteSpace(name) || cls == null || id == null) continue;
                    if (!Classes.TryGetValue(cls, out var info)) continue;
                    if (!IsRelevant(cls, id, name)) continue;

                    var connection = ConnectionOf(cls, id);
                    var group = info.Group;
                    var key = $"{group}|{name}";
                    if (!seen.Add(key)) continue;

                    var manufacturer = obj["Manufacturer"] as string ?? "";
                    var errorCode = obj["ConfigManagerErrorCode"] is { } code ? Convert.ToInt32(code) : 0;

                    var device = new DeviceInfo
                    {
                        Name = name,
                        Manufacturer = manufacturer,
                        Group = group,
                        ClassName = cls,
                        DeviceId = id,
                        Connection = connection,
                        HasProblem = errorCode != 0,
                        SettingsUri = info.Settings
                    };

                    ResolveVendor(device);
                    list.Add(device);
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudieron listar los dispositivos", ex);
        }

        return list;
    }

    private static bool IsRelevant(string cls, string id, string name)
    {
        var lower = name.ToLowerInvariant();

        switch (cls.ToLowerInvariant())
        {
            case "bluetooth":
                // Solo dispositivos emparejados (BTHENUM\DEV_...) y las antenas; fuera los "servicios".
                return id.StartsWith(@"BTHENUM\DEV_", StringComparison.OrdinalIgnoreCase) ||
                       id.StartsWith(@"BTHLE\DEV_", StringComparison.OrdinalIgnoreCase) ||
                       id.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase) ||
                       id.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase);

            case "usb":
                return id.StartsWith(@"USB\VID_", StringComparison.OrdinalIgnoreCase) &&
                       !id.Contains("&MI_", StringComparison.OrdinalIgnoreCase) &&
                       !lower.Contains("hub") && !lower.Contains("concentrador") &&
                       !lower.Contains("host controller") && !lower.Contains("controladora host");

            case "hidclass":
                return id.Contains("VID_", StringComparison.OrdinalIgnoreCase) &&
                       !lower.Contains("system controller") && !lower.Contains("consumer") &&
                       GamepadHints.Any(lower.Contains);

            default:
                return true;
        }
    }

    private static string ConnectionOf(string cls, string id)
    {
        if (id.StartsWith("BTH", StringComparison.OrdinalIgnoreCase)) return "Bluetooth";

        // La antena Bluetooth en si es interna/USB pero no es "un dispositivo conectado".
        if (cls.Equals("Bluetooth", StringComparison.OrdinalIgnoreCase)) return "Sistema";

        if (id.StartsWith("USB", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("VID_", StringComparison.OrdinalIgnoreCase)) return "USB";

        return "Sistema";
    }

    private static async Task<HashSet<string>> GetConnectedBluetoothNamesAsync()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        async Task Collect(string selector)
        {
            try
            {
                var infos = await DeviceInformation.FindAllAsync(selector);
                foreach (var info in infos)
                {
                    if (!string.IsNullOrWhiteSpace(info.Name)) names.Add(info.Name);
                }
            }
            catch
            {
                // Bluetooth apagado o sin permisos: simplemente no hay conectados que mostrar.
            }
        }

        await Collect(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
        await Collect(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
        return names;
    }

    // ===================== App del fabricante =====================

    private static void ResolveVendor(DeviceInfo device)
    {
        var text = $"{device.Manufacturer} {device.Name}".ToLowerInvariant();

        foreach (var (keyword, label, paths) in Vendors)
        {
            if (!text.Contains(keyword)) continue;

            device.VendorKeyword = keyword;
            device.VendorLabel = label;

            if (LoadOverrides().TryGetValue(keyword, out var custom) && File.Exists(custom))
            {
                device.VendorPath = custom;
            }
            else
            {
                device.VendorPath = paths
                    .Select(Environment.ExpandEnvironmentVariables)
                    .FirstOrDefault(File.Exists);
            }

            return;
        }
    }

    public static void SetVendorApp(string keyword, string exePath)
    {
        var map = LoadOverrides();
        map[keyword] = exePath;

        try
        {
            Directory.CreateDirectory(AppSettingsService.AppFolder);
            File.WriteAllText(OverridesPath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo guardar device_apps.json", ex);
        }
    }

    private static Dictionary<string, string> LoadOverrides()
    {
        if (_overrides != null) return _overrides;

        try
        {
            if (File.Exists(OverridesPath))
            {
                _overrides = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(OverridesPath));
            }
        }
        catch
        {
            // device_apps.json corrupto: empezamos sin asignaciones.
        }

        return _overrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
