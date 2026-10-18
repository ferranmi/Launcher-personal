using System.Diagnostics;
using System.IO;
using System.Management;
using Launcher.Core.Models;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Windows.Gaming.Input;
using PowerStatus = Windows.System.Power.BatteryStatus;

namespace Launcher.Core.Services;

/// <summary>
/// Detecta dispositivos externos y su estado para el panel UPLINK:
///  - Auriculares y micros (NAudio): conectado / en uso (con el proceso que los usa).
///    Bluetooth o cable se deduce cruzando el nombre del endpoint con los dispositivos
///    Bluetooth emparejados de Windows (WMI): es una heuristica, no un dato directo.
///  - Unidades USB: conectado / transfiriendo (por los contadores de LogicalDisk).
///  - Mandos (Windows.Gaming.Input): conectado, con bateria si la hay.
///
/// Llamar a Refresh() desde el mismo DispatcherTimer del dashboard (internamente se
/// limita a una pasada cada 1,5 s). Lo lento (WMI) corre en segundo plano cada 30 s.
/// Solo usar desde el hilo de la UI.
/// </summary>
public sealed class UplinkService : IDisposable
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan SlowInterval = TimeSpan.FromSeconds(30);

    /// <summary>1 MB/s de lectura+escritura: por debajo no se considera transferencia.</summary>
    private const double TransferThresholdBytes = 1024 * 1024;
    private static readonly TimeSpan TransferHold = TimeSpan.FromSeconds(3);

    // PKEY_AudioEndpoint_FormFactor: 3 auriculares, 4 micro, 5 headset, 6 handset.
    private static readonly PropertyKey FormFactorKey =
        new(new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"), 0);

    private readonly StatusEvaluator? _log;

    // Datos lentos (WMI), reemplazados por completo desde un hilo de fondo.
    private volatile HashSet<string> _bluetoothNames = new(StringComparer.OrdinalIgnoreCase);
    private volatile HashSet<string> _usbLetters = new(StringComparer.OrdinalIgnoreCase);
    private bool _wmiErrorLogged; // el fallo de WMI se registra una sola vez, no cada ciclo
    private bool _othersErrorLogged;
    private volatile List<UplinkDevice> _others = new(); // teclados, ratones, camaras y otros Bluetooth/USB (datos lentos)
    private volatile bool _slowReady;
    private int _slowRunning;
    private DateTime _nextSlow = DateTime.MinValue;

    private readonly Dictionary<string, (PerformanceCounter Read, PerformanceCounter Write)> _diskCounters =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastTransfer = new(StringComparer.OrdinalIgnoreCase);

    private DateTime _lastRefresh = DateTime.MinValue;
    private bool _firstPassDone;
    private bool _disposed;

    private readonly EventHandler<RawGameController> _padAdded = (_, _) => { };
    private readonly EventHandler<RawGameController> _padRemoved = (_, _) => { };

    public IReadOnlyList<UplinkDevice> Devices { get; private set; } = Array.Empty<UplinkDevice>();

    /// <summary>Se dispara cuando cambia la lista o el estado de algun dispositivo.</summary>
    public event EventHandler? Changed;

    public UplinkService(StatusEvaluator? log = null)
    {
        _log = log;

        // Suscribirse a los eventos hace que RawGameControllers se llene de forma fiable.
        RawGameController.RawGameControllerAdded += _padAdded;
        RawGameController.RawGameControllerRemoved += _padRemoved;
    }

    public void Refresh()
    {
        if (_disposed) return;

        var now = DateTime.UtcNow;
        if (now - _lastRefresh < MinInterval) return;
        _lastRefresh = now;

        KickSlowRefreshIfDue();

        var found = new List<UplinkDevice>();
        found.AddRange(ScanAudio());
        found.AddRange(ScanDrives());
        found.AddRange(ScanGamepads());
        found.AddRange(ScanOthers(found));

        Apply(found);
    }

    // ===================== Aplicar cambios + registro =====================

    private void Apply(List<UplinkDevice> found)
    {
        var old = Devices;

        if (old.SequenceEqual(found)) return;

        if (_firstPassDone && _log != null)
        {
            var oldById = old.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
            var newById = found.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());

            foreach (var (id, dev) in newById)
            {
                if (!oldById.TryGetValue(id, out var before))
                {
                    var how = dev.ConnectionLabel.Length > 0 ? $" ({dev.ConnectionLabel})" : "";
                    _log.Report(StatusLevel.Online, dev.Name, $"{dev.Name} conectado{how}", isUplink: true);
                }
                else if (dev.State == UplinkState.Problem && before.State != UplinkState.Problem)
                {
                    _log.Report(StatusLevel.Warning, dev.Name, $"{dev.Name}: {dev.Detail}", isUplink: true);
                }
            }

            foreach (var (id, dev) in oldById)
            {
                if (!newById.ContainsKey(id))
                    _log.Report(StatusLevel.Online, dev.Name, $"{dev.Name} desconectado", isUplink: true);
            }
        }

        _firstPassDone = true;
        Devices = found;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ===================== Audio (auriculares y micro) =====================

    private IEnumerable<UplinkDevice> ScanAudio()
    {
        var result = new List<UplinkDevice>();
        var btNames = _bluetoothNames;

        try
        {
            using var enumerator = new MMDeviceEnumerator();

            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active))
            {
                try
                {
                    var built = BuildAudioDevice(device, btNames);
                    if (built != null) result.Add(built);
                }
                catch
                {
                    // un endpoint que falla a mitad de lectura no debe tumbar al resto
                }
                finally
                {
                    device.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Uplink: fallo al enumerar audio", ex);
        }

        return result;
    }

    private UplinkDevice? BuildAudioDevice(MMDevice device, HashSet<string> btNames)
    {
        var friendly = device.FriendlyName ?? "";
        var btMatch = btNames
            .Where(n => friendly.Contains(n, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.Length)
            .FirstOrDefault();

        var isBluetooth = btMatch != null;
        var formFactor = ReadFormFactor(device);

        // Solo auriculares/micros/headsets (o cualquier cosa Bluetooth). Altavoces y HDMI fuera.
        if (!isBluetooth && formFactor is not (3 or 4 or 5 or 6)) return null;

        var isCapture = device.DataFlow == DataFlow.Capture;
        var users = ActiveSessionProcesses(device);

        var connection = isBluetooth
            ? UplinkConnection.Bluetooth
            : _slowReady ? UplinkConnection.Wired : UplinkConnection.Unknown;

        var state = users.Count > 0 ? UplinkState.Active : UplinkState.Connected;
        var detail = "";
        if (users.Count > 0)
        {
            var who = string.Join(", ", users.Distinct());
            detail = isCapture ? $"micro en uso ({who})" : $"sonando ({who})";
        }

        return new UplinkDevice(
            device.ID,
            btMatch ?? friendly,
            isCapture ? UplinkKind.Microphone : UplinkKind.Headphones,
            connection,
            state,
            detail);
    }

    private static List<string> ActiveSessionProcesses(MMDevice device)
    {
        var names = new List<string>();

        try
        {
            var sessions = device.AudioSessionManager.Sessions;
            for (var i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                if (session.State != AudioSessionState.AudioSessionStateActive) continue;
                if (session.IsSystemSoundsSession) continue;

                names.Add(ProcessNameOrUnknown((int)session.GetProcessID));
            }
        }
        catch
        {
            // sin acceso a las sesiones de este endpoint: se queda como "conectado"
        }

        return names;
    }

    private static string ProcessNameOrUnknown(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch
        {
            return "?";
        }
    }

    private static int ReadFormFactor(MMDevice device)
    {
        try
        {
            var props = device.Properties;
            return props.Contains(FormFactorKey) ? Convert.ToInt32(props[FormFactorKey].Value) : -1;
        }
        catch
        {
            return -1;
        }
    }

    // ===================== Unidades USB =====================

    private IEnumerable<UplinkDevice> ScanDrives()
    {
        var result = new List<UplinkDevice>();
        var usb = _usbLetters;
        var now = DateTime.UtcNow;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var drive in DriveInfo.GetDrives())
        {
            var letter = drive.Name.TrimEnd('\\');

            // Removable (pendrives, lectores) o disco USB segun WMI (HDD/SSD externos salen como Fixed).
            if (drive.DriveType != DriveType.Removable && !usb.Contains(letter)) continue;

            try
            {
                if (!drive.IsReady) continue; // lector sin tarjeta, etc.

                seen.Add(letter);

                var (read, write) = ReadDiskRates(letter);
                if (read + write >= TransferThresholdBytes) _lastTransfer[letter] = now;

                var transferring = _lastTransfer.TryGetValue(letter, out var last) && now - last < TransferHold;

                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Disco externo" : drive.VolumeLabel;
                var freeGb = drive.TotalFreeSpace / 1024d / 1024 / 1024;

                var detail = transferring
                    ? $"{(write > read ? "escribiendo" : "leyendo")} {FormatRate(Math.Max(read, write))}"
                    : $"{freeGb:0} GB libres";

                result.Add(new UplinkDevice(
                    $"drive:{letter}",
                    $"{label} ({letter})",
                    UplinkKind.Drive,
                    UplinkConnection.Wired,
                    transferring ? UplinkState.Transferring : UplinkState.Connected,
                    detail));
            }
            catch
            {
                // unidad expulsada justo ahora
            }
        }

        foreach (var stale in _diskCounters.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            _diskCounters[stale].Read.Dispose();
            _diskCounters[stale].Write.Dispose();
            _diskCounters.Remove(stale);
            _lastTransfer.Remove(stale);
        }

        return result;
    }

    private (double Read, double Write) ReadDiskRates(string letter)
    {
        try
        {
            if (!_diskCounters.TryGetValue(letter, out var counters))
            {
                counters = (
                    new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", letter, true),
                    new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", letter, true));
                _diskCounters[letter] = counters;
            }

            return (counters.Read.NextValue(), counters.Write.NextValue());
        }
        catch
        {
            return (0, 0); // instancia todavia no registrada (unidad recien conectada)
        }
    }

    private static string FormatRate(double bytesPerSec)
    {
        var kb = bytesPerSec / 1024d;
        return kb >= 1024 ? $"{kb / 1024:0.#} MB/s" : $"{kb:0.#} KB/s";
    }

    // ===================== Mandos =====================

    private IEnumerable<UplinkDevice> ScanGamepads()
    {
        var result = new List<UplinkDevice>();

        try
        {
            foreach (var pad in RawGameController.RawGameControllers)
            {
                var name = string.IsNullOrWhiteSpace(pad.DisplayName) ? "Mando" : pad.DisplayName;

                var report = pad.TryGetBatteryReport();
                var hasBattery = report != null && report.Status != PowerStatus.NotPresent;

                int? percent = null;
                if (hasBattery && report!.RemainingCapacityInMilliwattHours is int rem &&
                    report.FullChargeCapacityInMilliwattHours is int full && full > 0)
                {
                    percent = Math.Clamp(rem * 100 / full, 0, 100);
                }

                var low = percent is < 15;

                result.Add(new UplinkDevice(
                    $"pad:{pad.NonRoamableId}",
                    name,
                    UplinkKind.Gamepad,
                    hasBattery ? UplinkConnection.Wireless : UplinkConnection.Wired,
                    low ? UplinkState.Problem : UplinkState.Connected,
                    low ? $"bateria baja ({percent}%)" : percent is int p ? $"bateria {p}%" : ""));
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Uplink: fallo al leer mandos", ex);
        }

        return result;
    }

    // ===================== Otros dispositivos (teclado, raton, camara, Bluetooth) =====================

    /// <summary>Devuelve los "otros" dispositivos leidos en el ciclo lento, quitando los que ya salen
    /// como auriculares, micro, mando o unidad (el mismo aparato puede estar en varias listas de Windows).</summary>
    private IEnumerable<UplinkDevice> ScanOthers(List<UplinkDevice> already)
    {
        foreach (var other in _others)
        {
            var duplicate = already.Any(a =>
                a.Name.Contains(other.Name, StringComparison.OrdinalIgnoreCase) ||
                other.Name.Contains(a.Name, StringComparison.OrdinalIgnoreCase));

            if (!duplicate) yield return other;
        }
    }

    /// <summary>Teclados, ratones, camaras, impresoras y dispositivos Bluetooth conectados ahora, a partir de
    /// DeviceService (WMI + estado de conexion Bluetooth). Solo externos: USB o Bluetooth.</summary>
    private static List<UplinkDevice> ReadOtherDevices()
    {
        var result = new List<UplinkDevice>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Esto corre en un hilo de fondo: esperar aqui no bloquea la interfaz.
        var devices = DeviceService.QueryAsync().GetAwaiter().GetResult();

        foreach (var d in devices.Where(d => d.IsExternal && d.IsConnected && !d.HasProblem))
        {
            UplinkKind kind;
            string label;

            switch (d.Group)
            {
                case "Teclados": kind = UplinkKind.Keyboard; label = "Teclado"; break;
                case "Ratones": kind = UplinkKind.Mouse; label = "Raton"; break;
                case "Camaras": kind = UplinkKind.Camera; label = "Camara"; break;
                case "Bluetooth":
                case "Impresoras": kind = UplinkKind.Other; label = "Dispositivo"; break;
                default: continue; // audio, mandos y unidades ya tienen su propio detector
            }

            var connection = d.Connection == "Bluetooth" ? UplinkConnection.Bluetooth : UplinkConnection.Wired;

            // Muchos teclados/ratones solo se llaman "Dispositivo de entrada USB" o "HID-compliant mouse".
            var name = LooksGeneric(d.Name)
                ? $"{label} {(connection == UplinkConnection.Bluetooth ? "Bluetooth" : "USB")}"
                : d.Name;

            if (!seen.Add($"{kind}|{name}")) continue; // un mismo aparato aparece varias veces (una por interfaz)

            result.Add(new UplinkDevice($"pnp:{d.DeviceId}", name, kind, connection, UplinkState.Connected));
        }

        return result;
    }

    private static bool LooksGeneric(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("hid") || n.StartsWith("usb") || n.Contains("compatible") ||
               n.Contains("input") || n.Contains("entrada");
    }

    // ===================== Datos lentos (WMI, en segundo plano) =====================

    private void KickSlowRefreshIfDue()
    {
        if (DateTime.UtcNow < _nextSlow) return;
        if (Interlocked.Exchange(ref _slowRunning, 1) == 1) return;

        _nextSlow = DateTime.UtcNow + SlowInterval;

        _ = Task.Run(() =>
        {
            try
            {
                try
                {
                    _bluetoothNames = ReadBluetoothNames();
                    _usbLetters = ReadUsbDriveLetters();
                }
                catch (Exception ex)
                {
                    if (!_wmiErrorLogged)
                    {
                        _wmiErrorLogged = true;
                        LogService.Error("Uplink: fallo al leer WMI (no se repetira en esta sesion)", ex);
                    }
                }

                // Aparte: si esto falla, auriculares, unidades y mandos siguen funcionando.
                try
                {
                    _others = ReadOtherDevices();
                }
                catch (Exception ex)
                {
                    if (!_othersErrorLogged)
                    {
                        _othersErrorLogged = true;
                        LogService.Error("Uplink: fallo al leer otros dispositivos (no se repetira en esta sesion)", ex);
                    }
                }
            }
            finally
            {
                _slowReady = true;
                Interlocked.Exchange(ref _slowRunning, 0);
            }
        });
    }

    /// <summary>Nombres de dispositivos Bluetooth emparejados (nodos BTHENUM / BTHLE de Windows).
    /// Se filtran los genericos para que no coincidan con endpoints de audio por error.</summary>
    private static HashSet<string> ReadBluetoothNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var searcher = new ManagementObjectSearcher(
            "SELECT Name FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'BTHENUM%' OR PNPDeviceID LIKE 'BTHLE%'");

        foreach (ManagementObject obj in searcher.Get())
        {
            using (obj)
            {
                var name = obj["Name"] as string;
                if (string.IsNullOrWhiteSpace(name) || name.Length < 3) continue;
                if (name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase)) continue;
                names.Add(name.Trim());
            }
        }

        return names;
    }

    /// <summary>Enumera una consulta WMI sin lanzar si WMI devuelve "No encontrado" a mitad
    /// (un disco que se retira entre consultas, una particion sin letra...): termina la lista y ya.</summary>
    private static IEnumerable<ManagementObject> SafeEnumerate(ManagementObjectSearcher searcher)
    {
        ManagementObjectCollection? collection;
        try { collection = searcher.Get(); }
        catch (ManagementException) { collection = null; }
        if (collection == null) yield break;

        using var enumerator = collection.GetEnumerator();
        while (true)
        {
            bool hasNext;
            try { hasNext = enumerator.MoveNext(); }
            catch (ManagementException) { hasNext = false; }

            if (!hasNext) yield break;
            yield return (ManagementObject)enumerator.Current;
        }
    }

    /// <summary>Letras de unidad que cuelgan de un disco con interfaz USB. Los discos USB que
    /// usan UASP a veces se anuncian como SCSI y no aparecerian aqui.</summary>
    private static HashSet<string> ReadUsbDriveLetters()
    {
        var letters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var disks = new ManagementObjectSearcher(
            "SELECT DeviceID FROM Win32_DiskDrive WHERE InterfaceType = 'USB'");

        foreach (ManagementObject disk in SafeEnumerate(disks))
        {
            using (disk)
            {
                var diskId = (disk["DeviceID"] as string ?? "").Replace("\\", "\\\\");
                if (diskId.Length == 0) continue;

                using var partitions = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{diskId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");

                foreach (ManagementObject partition in SafeEnumerate(partitions))
                {
                    using (partition)
                    {
                        var partitionId = partition["DeviceID"] as string;
                        if (string.IsNullOrEmpty(partitionId)) continue;

                        using var logicals = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionId}'}} WHERE AssocClass = Win32_LogicalDiskToPartition");

                        foreach (ManagementObject logical in SafeEnumerate(logicals))
                        {
                            using (logical)
                            {
                                if (logical["DeviceID"] is string letter) letters.Add(letter);
                            }
                        }
                    }
                }
            }
        }

        return letters;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        RawGameController.RawGameControllerAdded -= _padAdded;
        RawGameController.RawGameControllerRemoved -= _padRemoved;

        foreach (var (read, write) in _diskCounters.Values)
        {
            read.Dispose();
            write.Dispose();
        }
        _diskCounters.Clear();
    }
}
