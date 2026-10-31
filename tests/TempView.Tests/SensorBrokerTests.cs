using System.Diagnostics;
using System.Text;
using TempView.Core;
using TempView.Hardware;
using Xunit;

namespace TempView.Tests;

public sealed class SensorBrokerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    static BrokerSnapshot Snapshot(params SensorValue[] sensors) => new(BrokerProtocol.Version, Now, true, "2.2.0", null, sensors);
    static readonly SensorValue Tctl = new("/amdcpu/0/temperature/2", "AMD Ryzen 7 7800X3D / Core (Tctl/Tdie)", "CPU", 54.5);

    [Fact]
    public void SnapshotRoundTrips()
    {
        var parsed = BrokerProtocol.Parse(BrokerProtocol.Serialize(Snapshot(Tctl)), Now);
        Assert.NotNull(parsed);
        Assert.Equal(54.5, Assert.Single(parsed!.Sensors).Value);
        Assert.Equal("2.2.0", parsed.DriverVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"version\":2,\"timestamp\":\"2026-10-31T12:00:00Z\",\"sensors\":[]}")]
    [InlineData("{\"version\":1,\"timestamp\":\"2026-10-31T12:00:00Z\"}")]
    [InlineData("{\"version\":1,\"timestamp\":\"2026-10-31T12:00:00Z\",\"sensors\":[{\"id\":\"a\",\"name\":\"b\",\"kind\":\"DISK\",\"value\":40}]}")]
    [InlineData("{\"version\":1,\"timestamp\":\"2026-10-31T12:00:00Z\",\"sensors\":[{\"id\":\"a\\u0007\",\"name\":\"b\",\"kind\":\"CPU\",\"value\":40}]}")]
    [InlineData("{\"version\":1,\"timestamp\":\"2027-01-01T00:00:00Z\",\"sensors\":[]}")]
    public void MalformedOrForeignMessagesAreRejected(string json) => Assert.Null(BrokerProtocol.Parse(Encoding.UTF8.GetBytes(json), Now));

    [Fact]
    public void OversizedMessagesAndTooManySensorsAreRejected()
    {
        Assert.Null(BrokerProtocol.Parse(new byte[BrokerProtocol.MaxMessageBytes + 1], Now));
        var many = Enumerable.Range(0, 129).Select(i => Tctl with { Id = "s" + i }).ToArray();
        Assert.Null(BrokerProtocol.Parse(BrokerProtocol.Serialize(Snapshot(many)), Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(250)]
    public void ImplausibleValuesBecomeMissingNeverNumbers(double value)
    {
        var parsed = BrokerProtocol.Parse(BrokerProtocol.Serialize(Snapshot(Tctl with { Value = value })), Now)!;
        Assert.Null(Assert.Single(parsed.Sensors).Value);
    }

    [Fact]
    public void SelectionPrefersMeasuredRepresentativeSensorAndHonorsExplicitChoice()
    {
        SensorValue[] sensors = [new("c1", "CPU / Core #1", "CPU", 50), new("c2", "CPU / Package", "CPU", null), new("c3", "CPU / Package", "CPU", 61), new("g1", "GPU / GPU Core", "GPU", 40)];
        Assert.Equal("c3", SensorSelection.Pick(sensors, "CPU", null)!.Id);
        Assert.Equal("c2", SensorSelection.Pick(sensors, "CPU", "c2")!.Id);
        Assert.Null(SensorSelection.Pick(sensors, "CPU", "missing"));
        Assert.Equal("g1", SensorSelection.Pick(sensors, "GPU", null)!.Id);
    }

    [Fact]
    public void CpuDiagnosisNamesTheActualCause()
    {
        static string Explain(CpuAccess a, bool found = false) => CpuDiagnosis.Explain(a, false, found);
        Assert.Contains("nie odpowiada", Explain(new(BrokerState.Unreachable, null, true, false)));
        Assert.Contains("PawnIO nie jest zainstalowany", Explain(new(BrokerState.Running, Snapshot() with { DriverInstalled = false }, false, false)));
        Assert.Contains("nie mogła odczytać", Explain(new(BrokerState.Running, Snapshot() with { Error = "boom" }, true, false)));
        Assert.Contains("nie udostępnia", Explain(new(BrokerState.Running, Snapshot(), true, false)));
        Assert.Contains("nie zwrócił", Explain(new(BrokerState.Running, Snapshot(), true, false), found: true));
        Assert.Contains("wymaga sterownika", Explain(new(BrokerState.NotInstalled, null, false, false)));
        Assert.Contains("administratora", Explain(new(BrokerState.NotInstalled, null, true, false)));
        Assert.Contains("nie udostępnia", Explain(new(BrokerState.NotInstalled, null, true, true)));
        Assert.Contains("Wybrany czujnik", CpuDiagnosis.Explain(new(BrokerState.Running, Snapshot(), true, false), true, false));
    }

    [Fact]
    public async Task PipeDeliversSnapshotOnlyFromTheExpectedServerBinary()
    {
        var name = "TempView.Tests." + Guid.NewGuid().ToString("N");
        var payload = BrokerProtocol.Serialize(Snapshot(Tctl) with { Timestamp = DateTimeOffset.UtcNow });
        using var stop = new CancellationTokenSource();
        var server = new BrokerServer(name, () => payload).RunAsync(stop.Token);
        try
        {
            var self = Process.GetCurrentProcess().MainModule!.FileName;
            var (ok, error) = BrokerClient.Read(name, self, TimeSpan.FromSeconds(5));
            Assert.Null(error);
            Assert.Equal(54.5, Assert.Single(ok!.Sensors).Value);

            var (spoofed, reason) = BrokerClient.Read(name, @"C:\Program Files\TempView\TempView.Sensors.exe", TimeSpan.FromSeconds(5));
            Assert.Null(spoofed);
            Assert.Contains("nie należy", reason);
        }
        finally { stop.Cancel(); await server; }
    }

    [Fact]
    public void MissingPipeReportsUnavailableInsteadOfThrowing()
    {
        var (snapshot, error) = BrokerClient.Read("TempView.Tests.missing." + Guid.NewGuid().ToString("N"), "x.exe", TimeSpan.FromMilliseconds(200));
        Assert.Null(snapshot);
        Assert.NotNull(error);
    }
}
