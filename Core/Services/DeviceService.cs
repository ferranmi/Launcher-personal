using System.IO;
using System.Management;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
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
                "SELECT Name, Manufacturer, PNPClass, DeviceID, HardwareID, ConfigManagerErrorCode " +
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

                    var (vendorBrand, vidPid) = IdentifyVendor(id, obj["HardwareID"] as string[]);
                    var busName = ReadBusReportedName(id);

                    var device = new DeviceInfo
                    {
                        Brand = vendorBrand ?? CleanManufacturer(manufacturer),
                        Model = busName != null && !IsGenericName(busName) ? busName
                              : !IsGenericName(name) ? name : "",
                        VidPid = vidPid,
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

    // ===================== Marca y modelo =====================

    private static readonly Regex VidPidRegex = new(
        @"VID[_&]([0-9A-F]{4,8})&PID[_&]([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Fabricantes USB habituales (VID asignado por USB-IF). Los desconocidos se muestran solo como codigo.
    private static readonly Dictionary<string, string> UsbVendors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["046D"] = "Logitech", ["1532"] = "Razer", ["1B1C"] = "Corsair", ["1038"] = "SteelSeries",
        ["045E"] = "Microsoft", ["054C"] = "Sony", ["057E"] = "Nintendo", ["05AC"] = "Apple",
        ["04E8"] = "Samsung", ["0781"] = "SanDisk", ["0951"] = "Kingston / HyperX", ["0BDA"] = "Realtek",
        ["8087"] = "Intel", ["0B05"] = "ASUS", ["413C"] = "Dell", ["03F0"] = "HP", ["17EF"] = "Lenovo",
        ["0D8C"] = "C-Media", ["1395"] = "Sennheiser", ["0A12"] = "Cambridge Silicon Radio", ["0A5C"] = "Broadcom",
        ["04F2"] = "Chicony", ["0C45"] = "Microdia", ["10C4"] = "Silicon Labs", ["0403"] = "FTDI",
        ["2109"] = "VIA Labs", ["05E3"] = "Genesys Logic", ["1A86"] = "QinHeng (CH340)", ["2717"] = "Xiaomi",
    };

    // Identificadores de empresa de Bluetooth SIG (cuando el fabricante lo declara asi).
    private static readonly Dictionary<string, string> BluetoothVendors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["004C"] = "Apple", ["0006"] = "Microsoft", ["0075"] = "Samsung", ["012D"] = "Sony", ["00E0"] = "Google",
        ["000F"] = "Broadcom", ["0002"] = "Intel", ["001D"] = "Qualcomm", ["0087"] = "Garmin", ["009E"] = "Bose",
        ["0157"] = "Anker", ["0171"] = "Amazon", ["038F"] = "Xiaomi", ["0310"] = "Logitech", ["0494"] = "JBL / Harman",
    };

    /// <summary>Marca y codigo fabricante:producto a partir del DeviceID o de los HardwareID (VID_xxxx&amp;PID_xxxx).</summary>
    private static (string? Brand, string VidPid) IdentifyVendor(string deviceId, string[]? hardwareIds)
    {
        var candidates = new List<string> { deviceId };
        if (hardwareIds != null) candidates.AddRange(hardwareIds);

        foreach (var text in candidates)
        {
            var m = VidPidRegex.Match(text);
            if (!m.Success) continue;

            var vid = m.Groups[1].Value.ToUpperInvariant();
            var pid = m.Groups[2].Value.ToUpperInvariant();

            // 8 digitos = origen (0001 Bluetooth SIG, 0002 USB-IF) + codigo de 4 digitos.
            var source = vid.Length == 8 ? vid[..4] : "0002";
            var code = vid.Length == 8 ? vid[4..] : vid;

            var table = source == "0001" ? BluetoothVendors : UsbVendors;
            return (table.TryGetValue(code, out var brand) ? brand : null, $"{code}:{pid}");
        }

        return (null, "");
    }

    /// <summary>El fabricante del driver suele ser "(Standard system devices)" o "Microsoft": solo vale si es una marca real.</summary>
    private static string CleanManufacturer(string manufacturer)
    {
        var m = manufacturer.Trim();
        if (m.Length == 0 || m.StartsWith('(')) return "";

        var lower = m.ToLowerInvariant();
        return lower is "microsoft" or "generic" or "compatible" || lower.Contains("standard") ? "" : m;
    }

    /// <summary>Nombre de producto que el propio dispositivo declara por USB (BusReportedDeviceDesc), si Windows lo guarda.
    /// Mejor esfuerzo: sin permisos o sin la clave devuelve null.</summary>
    private static string? ReadBusReportedName(string deviceId)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Enum\{deviceId}\Properties\{{540b947e-8b40-45bc-a8a2-6a0b894cbda2}}\0004");
            var value = key?.GetValue("") as string;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsGenericName(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("hid") || n.Contains("composite") || n.Contains("compuesto") || n.Contains("generic") ||
               n.Contains("compatible") || n.Contains("dispositivo de entrada") || n.Contains("input device") ||
               n.StartsWith("usb ") || n.StartsWith("dispositivo usb");
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
