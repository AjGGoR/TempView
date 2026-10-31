using System.Text.Json;

namespace TempView.Core;

/// <summary>One temperature sensor. Value is null when the sensor exists but did not return a measurement.</summary>
public sealed record SensorValue(string Id, string Name, string Kind, double? Value);

/// <summary>What the sensor service publishes. The service accepts no input; clients only read this snapshot.</summary>
public sealed record BrokerSnapshot(int Version, DateTimeOffset Timestamp, bool DriverInstalled, string? DriverVersion, string? Error, IReadOnlyList<SensorValue> Sensors);

public enum BrokerState { NotInstalled, Running, Unreachable }

public static class BrokerProtocol
{
    public const int Version = 1;
    public const string PipeName = "TempView.Sensors.v1";
    public const string ServiceName = "TempViewSensors";
    public const int MaxMessageBytes = 64 * 1024;
    const int MaxSensors = 128, MaxText = 256;
    static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static byte[] Serialize(BrokerSnapshot snapshot) => JsonSerializer.SerializeToUtf8Bytes(snapshot, Options);

    /// <summary>Parses and validates a snapshot from an untrusted pipe. Returns null for anything malformed, oversized or from another protocol version.</summary>
    public static BrokerSnapshot? Parse(ReadOnlySpan<byte> data, DateTimeOffset now)
    {
        if (data.Length == 0 || data.Length > MaxMessageBytes) return null;
        BrokerSnapshot? raw;
        try { raw = JsonSerializer.Deserialize<BrokerSnapshot>(data, Options); }
        catch (JsonException) { return null; }
        if (raw is null || raw.Version != Version || raw.Sensors is null || raw.Sensors.Count > MaxSensors) return null;
        if (raw.Timestamp > now.AddMinutes(1)) return null;
        var sensors = new List<SensorValue>();
        foreach (var s in raw.Sensors)
        {
            if (s is null || !Text(s.Id) || !Text(s.Name) || s.Kind is not ("CPU" or "GPU")) return null;
            // Same plausibility rule as local readings: anything outside the range is "no measurement", never a number.
            var value = s.Value is double v && double.IsFinite(v) && v > 0 && v <= 200 ? v : (double?)null;
            sensors.Add(s with { Value = value });
        }
        return raw with { Sensors = sensors, Error = Clip(raw.Error), DriverVersion = Clip(raw.DriverVersion) };
    }

    static bool Text(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length <= MaxText && !s.Any(char.IsControl);
    static string? Clip(string? s) => s is null ? null : new string(s.Where(c => !char.IsControl(c)).Take(MaxText).ToArray());
}

public static class SensorSelection
{
    /// <summary>Chosen sensor for a kind: the configured id, or the most representative sensor that currently has a value.</summary>
    public static SensorValue? Pick(IEnumerable<SensorValue> sensors, string kind, string? id)
    {
        var candidates = sensors.Where(s => s.Kind == kind);
        return !string.IsNullOrEmpty(id)
            ? candidates.FirstOrDefault(s => s.Id == id)
            : candidates.OrderByDescending(s => s.Value is not null).ThenByDescending(s => Score(s.Name, kind)).FirstOrDefault();
    }

    static int Score(string name, string kind) =>
        name.Contains("Package", StringComparison.OrdinalIgnoreCase) ? 100
        : name.Contains(kind == "CPU" ? "Tctl" : "Core", StringComparison.OrdinalIgnoreCase) ? 90
        : name.Contains("Average", StringComparison.OrdinalIgnoreCase) ? 80 : 0;
}

/// <summary>Everything that decides why a CPU reading may be missing.</summary>
public sealed record CpuAccess(BrokerState Broker, BrokerSnapshot? Snapshot, bool LocalDriverInstalled, bool Elevated);

public static class CpuDiagnosis
{
    /// <summary>User-facing reason for a missing CPU reading. Never guesses a value.</summary>
    public static string Explain(CpuAccess access, bool sensorSelected, bool sensorFound)
    {
        if (sensorSelected && !sensorFound && access.Broker != BrokerState.Unreachable)
            return "Wybrany czujnik CPU jest niedostępny. Wybierz inny czujnik lub tryb automatyczny.";
        switch (access.Broker)
        {
            case BrokerState.Unreachable:
                return "Usługa TempView odczytu CPU nie odpowiada. Uruchom ponownie komputer albo zainstaluj TempView ponownie.";
            case BrokerState.Running when access.Snapshot is { } s:
                if (!s.DriverInstalled) return "Sterownik PawnIO nie jest zainstalowany. Uruchom instalator TempView ponownie i zaznacz odczyt temperatury CPU.";
                if (s.Error is not null) return "Usługa TempView nie mogła odczytać czujników CPU: " + s.Error;
                return sensorFound
                    ? "Czujnik CPU nie zwrócił aktualnego pomiaru."
                    : "Ten procesor nie udostępnia obsługiwanego czujnika temperatury albo Windows zablokował sterownik PawnIO.";
            default:
                if (!access.LocalDriverInstalled) return "Odczyt temperatury CPU wymaga sterownika PawnIO. Zainstaluj TempView instalatorem i zaznacz odczyt temperatury CPU.";
                if (!access.Elevated) return "Sterownik PawnIO jest zainstalowany, ale bez usługi TempView odczyt CPU wymaga uprawnień administratora. Zainstaluj TempView instalatorem z odczytem CPU.";
                return sensorFound
                    ? "Czujnik CPU nie zwrócił aktualnego pomiaru."
                    : "Ten procesor nie udostępnia obsługiwanego czujnika temperatury albo Windows zablokował sterownik PawnIO.";
        }
    }
}
