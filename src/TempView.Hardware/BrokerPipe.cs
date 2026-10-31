using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using TempView.Core;

namespace TempView.Hardware;

/// <summary>
/// Publishes the latest snapshot on a read-only named pipe. Security boundary of the sensor service:
/// clients cannot send data (outbound pipe), local signed-in users may read, network access is denied,
/// and each connection receives one bounded message.
/// </summary>
public sealed class BrokerServer(string pipeName, Func<byte[]> snapshot)
{
    const int Instances = 4;

    public static PipeSecurity CreateSecurity()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.Read, AccessControlType.Allow));
        return security;
    }

    public Task RunAsync(CancellationToken stop) => Task.WhenAll(Enumerable.Range(0, Instances).Select(_ => Task.Run(() => Serve(stop), stop)));

    async Task Serve(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.Out, Instances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, BrokerProtocol.MaxMessageBytes, CreateSecurity());
                await pipe.WaitForConnectionAsync(stop);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                var data = snapshot();
                await pipe.WriteAsync(data, timeout.Token);
                await pipe.FlushAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
            catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
            catch (UnauthorizedAccessException)
            {
                // Another process owns the pipe name. Do not serve under a foreign security descriptor; retry later.
                try { await Task.Delay(TimeSpan.FromSeconds(5), stop); } catch (OperationCanceledException) { return; }
            }
        }
    }
}

/// <summary>Reads the snapshot and verifies that the pipe is served by the registered TempView service binary.</summary>
public static class BrokerClient
{
    const string ServiceKey = @"SYSTEM\CurrentControlSet\Services\" + BrokerProtocol.ServiceName;

    /// <summary>Executable path registered for the sensor service (admin-only registry key), or null when not installed.</summary>
    public static string? RegisteredServicePath()
    {
        using var key = Registry.LocalMachine.OpenSubKey(ServiceKey);
        if (key?.GetValue("ImagePath") is not string image) return null;
        image = Environment.ExpandEnvironmentVariables(image.Trim());
        // ImagePath is quoted ("C:\...\TempView.Sensors.exe") optionally followed by arguments.
        return image.StartsWith('"') ? image[1..image.IndexOf('"', 1)] : image.Split(' ')[0];
    }

    public static (BrokerSnapshot? Snapshot, string? Error) Read(string pipeName, string expectedServerPath, TimeSpan timeout)
    {
        try
        {
            // Identification only: a process squatting on the pipe name cannot impersonate this user.
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.None, TokenImpersonationLevel.Identification);
            pipe.Connect((int)timeout.TotalMilliseconds);
            var server = ServerImage(pipe);
            if (server is null || !string.Equals(Path.GetFullPath(server), Path.GetFullPath(expectedServerPath), StringComparison.OrdinalIgnoreCase))
                return (null, "Kanał czujników nie należy do usługi TempView.");
            var buffer = new byte[BrokerProtocol.MaxMessageBytes + 1];
            int total = 0, read;
            using var cts = new CancellationTokenSource(timeout);
            while ((read = pipe.ReadAsync(buffer.AsMemory(total), cts.Token).AsTask().GetAwaiter().GetResult()) > 0)
                if ((total += read) > BrokerProtocol.MaxMessageBytes) return (null, "Usługa czujników wysłała zbyt dużą odpowiedź.");
            var snapshot = BrokerProtocol.Parse(buffer.AsSpan(0, total), DateTimeOffset.UtcNow);
            return snapshot is null ? (null, "Usługa czujników wysłała nieprawidłową odpowiedź.") : (snapshot, null);
        }
        catch (TimeoutException) { return (null, "Usługa czujników nie odpowiada."); }
        catch (OperationCanceledException) { return (null, "Usługa czujników nie odpowiada."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception) { return (null, ex.Message); }
    }

    static string? ServerImage(NamedPipeClientStream pipe)
    {
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var pid)) return null;
        var process = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
        if (process == 0) return null;
        try
        {
            var text = new char[1024]; int size = text.Length;
            return QueryFullProcessImageName(process, 0, text, ref size) ? new string(text, 0, size) : null;
        }
        finally { CloseHandle(process); }
    }

    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetNamedPipeServerProcessId(nint pipe, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] static extern nint OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(nint process, int flags, char[] name, ref int size);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint handle);
}
