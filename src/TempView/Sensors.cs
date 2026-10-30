using LibreHardwareMonitor.Hardware;
using TempView.Core;
namespace TempView;

public record SensorOption(string Id, string Name, string Kind);
public record Sample(SensorReading Cpu, SensorReading Gpu, IReadOnlyList<SensorOption> Sensors);
// Owned by one worker thread; UI never calls into hardware objects.
public sealed class Sensors : IDisposable
{
    readonly Computer computer = new() { IsCpuEnabled = true, IsGpuEnabled = true };
    bool opened; DateTime openedAt;
    public Sample Read(AppSettings settings)
    {
        try
        {
            if (opened && DateTime.UtcNow - openedAt > TimeSpan.FromMinutes(1)) { computer.Close(); opened = false; }
            if (!opened) { computer.Open(); opened = true; openedAt = DateTime.UtcNow; }
            var values = new List<(SensorOption Option, float? Value)>();
            foreach (var h in computer.Hardware) Visit(h, values);
            SensorReading Pick(string kind, string? id)
            {
                if (kind == "CPU" && !LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled) return SensorReading.Unavailable("Brak sterownika PawnIO. Pobierz go skryptem drivers/download-driver.ps1 i zainstaluj (UAC), potem uruchom TempView ponownie.");
                var candidates = values.Where(x => x.Option.Kind == kind).ToList();
                var chosen = !string.IsNullOrEmpty(id) ? candidates.FirstOrDefault(x => x.Option.Id == id) : candidates.OrderByDescending(x => x.Value is float v && float.IsFinite(v) && v > 0).ThenByDescending(x => Score(x.Option.Name, kind)).FirstOrDefault();
                return chosen.Option == null ? SensorReading.Unavailable("Brak czujnika lub wybrane urządzenie odłączone. CPU może wymagać administratora; sterownik może być blokowany przez Windows.") : chosen.Value is float v && float.IsFinite(v) && v > 0 && v <= 200 ? SensorReading.Available(v) : SensorReading.Unavailable("Czujnik nie zwrócił aktualnego pomiaru.");
            }
            return new(Pick("CPU", settings.CpuSensorId), Pick("GPU", settings.GpuSensorId), values.Select(x => x.Option).ToArray());
        }
        catch (Exception ex) { opened = false; try { computer.Close(); } catch { } return new(SensorReading.Unavailable(ex.Message), SensorReading.Unavailable(ex.Message), []); }
    }
    static int Score(string name, string kind) => name.Contains("Package", StringComparison.OrdinalIgnoreCase) ? 100 : name.Contains(kind == "CPU" ? "Tctl" : "Core", StringComparison.OrdinalIgnoreCase) ? 90 : name.Contains("Average", StringComparison.OrdinalIgnoreCase) ? 80 : 0;
    static void Visit(IHardware h, List<(SensorOption, float?)> list)
    {
        h.Update();
        string? kind = h.HardwareType == HardwareType.Cpu ? "CPU" : h.HardwareType.ToString().StartsWith("Gpu") ? "GPU" : null;
        if (kind != null) foreach (var s in h.Sensors.Where(s => s.SensorType == SensorType.Temperature)) list.Add((new(s.Identifier.ToString(), h.Name + " / " + s.Name, h.HardwareType == HardwareType.Cpu && s.Name == "GFX" ? "GPU" : kind), s.Value));
        foreach (var sub in h.SubHardware) Visit(sub, list);
    }
    public void Dispose() { try { computer.Close(); } catch { } }
}
