using LibreHardwareMonitor.Hardware;

namespace Launcher.Core.Services;

/// <summary>
/// Lee temperaturas de CPU y GPU mediante LibreHardwareMonitor.
/// Los sensores pueden no estar disponibles en todos los equipos.
/// </summary>
public sealed class ThermalMonitorService : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true
    };

    private bool _isOpen;

    public void Open()
    {
        if (_isOpen) return;

        try
        {
            _computer.Open();
            _isOpen = true;
        }
        catch
        {
            // La ausencia de acceso a sensores no debe impedir el uso del Dashboard.
        }
    }

    public (float? Cpu, float? Gpu) Sample()
    {
        if (!_isOpen) Open();
        if (!_isOpen) return (null, null);

        try
        {
            foreach (var hardware in _computer.Hardware)
                UpdateHardware(hardware);

            var cpu = FindTemperature(HardwareType.Cpu);
            var gpu = FindTemperature(HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel);
            return (cpu, gpu);
        }
        catch
        {
            return (null, null);
        }
    }

    private static void UpdateHardware(IHardware hardware)
    {
        hardware.Update();

        foreach (var subHardware in hardware.SubHardware)
            UpdateHardware(subHardware);
    }

    private float? FindTemperature(params HardwareType[] types)
    {
        var sensors = _computer.Hardware
            .Where(h => types.Contains(h.HardwareType))
            .SelectMany(GetSensors)
            .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
            .ToList();

        if (sensors.Count == 0) return null;

        // Prioriza sensores representativos y evita elegir un sensor auxiliar al azar.
        var preferred = sensors.FirstOrDefault(s =>
            s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
            s.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase));

        var selected = preferred ?? sensors
            .OrderByDescending(s => s.Value)
            .First();

        return selected.Value is >= 0 and <= 150 ? selected.Value : null;
    }

    private static IEnumerable<ISensor> GetSensors(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
            yield return sensor;

        foreach (var subHardware in hardware.SubHardware)
        foreach (var sensor in GetSensors(subHardware))
            yield return sensor;
    }

    public void Close()
    {
        if (!_isOpen) return;

        try { _computer.Close(); }
        catch { }
        finally { _isOpen = false; }
    }

    public void Dispose() => Close();
}
