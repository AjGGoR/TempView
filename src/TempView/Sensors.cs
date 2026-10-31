using System.Security.Principal;
using TempView.Core;
using TempView.Hardware;
namespace TempView;

public record SensorOption(string Id, string Name, string Kind);
public record Sample(SensorReading Cpu, SensorReading Gpu, IReadOnlyList<SensorOption> Sensors, bool? DriverInstalled = null, BrokerState Broker = BrokerState.NotInstalled, string? DriverVersion = null);

/// <summary>
/// Owned by one worker thread; UI never calls into hardware objects.
/// GPU sensors are read in this (unprivileged) process. CPU sensors come from the TempView sensor service when it is
/// installed, because PawnIO only admits SYSTEM and Administrators. Without the service (portable use) CPU is read
/// locally, which works only when TempView itself runs elevated.
/// </summary>
public sealed class Sensors : IDisposable
{
    static readonly bool Elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    HardwareReader? local;

    public Sample Read(AppSettings settings)
    {
        var servicePath = BrokerClient.RegisteredServicePath();
        var broker = servicePath is null ? BrokerState.NotInstalled : BrokerState.Unreachable;
        if (local is null || local.Cpu != (servicePath is null)) { local?.Dispose(); local = new HardwareReader(cpu: servicePath is null, gpu: true); }
        var values = new List<SensorValue>();
        string? localError = null;
        try { values.AddRange(local.Read()); }
        catch (Exception ex) { localError = ex.Message; }
        BrokerSnapshot? snapshot = null;
        if (servicePath is not null)
        {
            (snapshot, _) = BrokerClient.Read(BrokerProtocol.PipeName, servicePath, TimeSpan.FromMilliseconds(800));
            // A snapshot older than a few seconds means the service stopped reading.
            if (snapshot is not null && DateTimeOffset.UtcNow - snapshot.Timestamp < TimeSpan.FromSeconds(10)) { broker = BrokerState.Running; values.AddRange(snapshot.Sensors); }
            else snapshot = null;
        }
        var localDriver = HardwareReader.DriverVersion;
        var access = new CpuAccess(broker, snapshot, localDriver is not null, Elevated);

        SensorReading Pick(string kind, string? id)
        {
            var chosen = SensorSelection.Pick(values, kind, id);
            if (chosen?.Value is double v) return SensorReading.Available(v) with { Source = chosen.Name };
            if (kind == "CPU") return SensorReading.Unavailable(CpuDiagnosis.Explain(access, !string.IsNullOrEmpty(id), chosen is not null));
            if (localError is not null) return SensorReading.Unavailable(localError);
            return SensorReading.Unavailable(chosen is not null ? "Czujnik nie zwrócił aktualnego pomiaru."
                : !string.IsNullOrEmpty(id) ? "Wybrany czujnik GPU jest niedostępny. Urządzenie mogło zostać odłączone. Wybierz inny czujnik lub tryb automatyczny."
                : "Nie znaleziono czujnika temperatury GPU. Ten model lub jego sterownik może nie udostępniać temperatury.");
        }
        var driverVersion = snapshot?.DriverVersion ?? localDriver;
        return new(Pick("CPU", settings.CpuSensorId), Pick("GPU", settings.GpuSensorId), values.Select(v => new SensorOption(v.Id, v.Name, v.Kind)).ToArray(),
            snapshot?.DriverInstalled ?? localDriver is not null, broker, driverVersion);
    }

    public void Dispose() => local?.Dispose();
}
