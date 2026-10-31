namespace TempView.Core;

public sealed record SensorReading(double? Value, string? Reason, DateTimeOffset Timestamp)
{
    /// <summary>Human-readable sensor name, when known.</summary>
    public string? Source { get; init; }
    public bool IsAvailable => Value is double value && double.IsFinite(value) && value is >= -50 and <= 200;
    public string DisplayText => IsAvailable ? $"{Math.Round(Value!.Value):0}°C" : "N/A";
    public static SensorReading Available(double value) => double.IsFinite(value) && value is >= -50 and <= 200
        ? new(value, null, DateTimeOffset.UtcNow) : Unavailable("Czujnik zwrócił nieprawidłową temperaturę.");
    public static SensorReading Unavailable(string reason) => new(null, reason, DateTimeOffset.UtcNow);
    public SensorReading Expire(TimeSpan maxAge, DateTimeOffset now) => now - Timestamp > maxAge ? Unavailable("Odczyt czujnika jest nieaktualny.") : this;
}
