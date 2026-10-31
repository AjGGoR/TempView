using LibreHardwareMonitor.Hardware;
using TempView.Core;
using SensorValue = TempView.Core.SensorValue;

namespace TempView.Hardware;

/// <summary>Reads temperature sensors through LibreHardwareMonitor. Owned by one worker thread.</summary>
public sealed class HardwareReader(bool cpu, bool gpu) : IDisposable
{
    readonly Computer computer = new() { IsCpuEnabled = cpu, IsGpuEnabled = gpu };
    bool opened; DateTime openedAt;

    public bool Cpu => cpu;
    public bool Gpu => gpu;

    /// <summary>PawnIO as reported by LibreHardwareMonitor (uninstall registration). Null when not installed.</summary>
    public static string? DriverVersion => LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled ? LibreHardwareMonitor.PawnIo.PawnIo.Version?.ToString() ?? "?" : null;

    public IReadOnlyList<SensorValue> Read()
    {
        try
        {
            // Reopening picks up hot-plugged devices and drivers installed after start.
            if (opened && DateTime.UtcNow - openedAt > TimeSpan.FromMinutes(1)) { computer.Close(); opened = false; }
            if (!opened) { computer.Open(); opened = true; openedAt = DateTime.UtcNow; }
            var values = new List<SensorValue>();
            foreach (var h in computer.Hardware) Visit(h, values);
            return values;
        }
        catch
        {
            opened = false; try { computer.Close(); } catch { }
            throw;
        }
    }

    static void Visit(IHardware h, List<SensorValue> list)
    {
        h.Update();
        string? kind = h.HardwareType == HardwareType.Cpu ? "CPU" : h.HardwareType.ToString().StartsWith("Gpu") ? "GPU" : null;
        // AMD APUs report the integrated GPU ("GFX") inside the CPU's SMU sensors.
        if (kind != null)
            foreach (var s in h.Sensors.Where(s => s.SensorType == SensorType.Temperature))
                list.Add(new(s.Identifier.ToString(), h.Name + " / " + s.Name, h.HardwareType == HardwareType.Cpu && s.Name == "GFX" ? "GPU" : kind,
                    s.Value is float v && float.IsFinite(v) && v > 0 && v <= 200 ? v : null));
        foreach (var sub in h.SubHardware) Visit(sub, list);
    }

    public void Dispose() { try { computer.Close(); } catch { } }
}
