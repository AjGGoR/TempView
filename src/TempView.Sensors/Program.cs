using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using TempView.Core;
using TempView.Hardware;

namespace TempView.Sensors;

/// <summary>
/// TempView sensor service. Runs as LocalSystem only because PawnIO grants hardware access to administrators;
/// the installer strips its token to SeChangeNotifyPrivilege. It reads CPU temperature sensors and publishes
/// them on a read-only pipe. It accepts no requests, files or commands.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // Load native DLLs only from System32 and the protected installation directory.
        SetDefaultDllDirectories(0x00000800 /* SYSTEM32 */ | 0x00000200 /* APPLICATION_DIR */);
        if (args.Contains("--console"))
        {
            // Development: same publisher loop in the foreground under the current account (no hardware privileges).
            using var stop = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
            Console.WriteLine($"TempView.Sensors: \\\\.\\pipe\\{BrokerProtocol.PipeName} (Ctrl+C kończy)");
            new Publisher().RunAsync(stop.Token).GetAwaiter().GetResult();
            return 0;
        }
        ServiceBase.Run(new SensorService());
        return 0;
    }

    [DllImport("kernel32.dll")] static extern bool SetDefaultDllDirectories(uint flags);
}

sealed class SensorService : ServiceBase
{
    CancellationTokenSource? stop; Task? work;
    public SensorService() { ServiceName = BrokerProtocol.ServiceName; CanStop = true; CanShutdown = true; AutoLog = false; }
    protected override void OnStart(string[] args) { stop = new(); work = new Publisher().RunAsync(stop.Token); }
    protected override void OnStop() => Halt();
    protected override void OnShutdown() => Halt();
    void Halt() { stop?.Cancel(); try { work?.Wait(TimeSpan.FromSeconds(5)); } catch (AggregateException) { } }
}

/// <summary>Reads sensors once per second on one thread and keeps the latest serialized snapshot.</summary>
sealed class Publisher
{
    volatile byte[] latest = BrokerProtocol.Serialize(new(BrokerProtocol.Version, DateTimeOffset.UtcNow, HardwareReader.DriverVersion is not null, HardwareReader.DriverVersion, "Oczekiwanie na pierwszy odczyt.", []));

    public Task RunAsync(CancellationToken stop) => Task.WhenAll(
        Task.Factory.StartNew(() => Loop(stop), stop, TaskCreationOptions.LongRunning, TaskScheduler.Default),
        new BrokerServer(BrokerProtocol.PipeName, () => latest).RunAsync(stop));

    void Loop(CancellationToken stop)
    {
        // CPU only: discrete GPU vendor interfaces work without privileges in the user's own process.
        using var reader = new HardwareReader(cpu: true, gpu: false);
        string? lastError = null;
        while (!stop.IsCancellationRequested)
        {
            var driver = HardwareReader.DriverVersion;
            BrokerSnapshot snapshot;
            try { snapshot = new(BrokerProtocol.Version, DateTimeOffset.UtcNow, driver is not null, driver, null, reader.Read()); }
            catch (Exception ex)
            {
                snapshot = new(BrokerProtocol.Version, DateTimeOffset.UtcNow, driver is not null, driver, ex.GetType().Name + ": " + ex.Message, []);
                if (ex.Message != lastError) Log(ex.ToString(), EventLogEntryType.Warning);
                lastError = ex.Message;
            }
            latest = BrokerProtocol.Serialize(snapshot);
            stop.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
        }
    }

    // Event Log instead of files: a SYSTEM process must not write into user-writable directories.
    static void Log(string message, EventLogEntryType type)
    {
        // Own event source in the Application log (registered on first use by the SYSTEM account).
        try { if (Environment.UserInteractive) Console.Error.WriteLine(message); else EventLog.WriteEntry(BrokerProtocol.ServiceName, message, type); } catch { }
    }
}
