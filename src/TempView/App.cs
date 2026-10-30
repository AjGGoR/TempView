using System.IO;
using System.Text.Json;
using System.Windows;
using TempView.Core;
namespace TempView;

public sealed class App : Application
{
    volatile AppSettings settings = new(); readonly SettingsStore store = new(); readonly Dictionary<string, Widget> widgets = [];
    readonly CancellationTokenSource stop = new(); SettingsWindow? editor; System.Windows.Forms.NotifyIcon? tray; volatile Sample sample = new(SensorReading.Unavailable("Oczekiwanie na czujnik"), SensorReading.Unavailable("Oczekiwanie na czujnik"), []); string placement = "Oczekiwanie na pasek";
    static string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TempView", "diagnostics.json");
    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\TempView.Desktop", out var created);
        if (!created) { MessageBox.Show("TempView już działa. Ustawienia otworzysz z ikony w obszarze powiadomień."); return; }
        var diag = Array.IndexOf(args, "--diagnostics"); if (diag >= 0 && diag + 1 < args.Length) LogPath = Path.GetFullPath(args[diag + 1]);
        var app = new App(); app.Run();
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, err) => { File.AppendAllText(Path.Combine(Path.GetTempPath(), "TempView-error.log"), err.Exception.ToString()); MessageBox.Show(err.Exception.Message, "TempView"); err.Handled = true; Shutdown(1); };
        var loaded = store.Load(); settings = loaded.Settings;
        tray = new() { Icon = System.Drawing.SystemIcons.Information, Text = "TempView — ustawienia", Visible = true };
        var menu = new System.Windows.Forms.ContextMenuStrip(); menu.Items.Add("Ustawienia", null, (_, _) => Dispatcher.Invoke(OpenSettings)); menu.Items.Add("Diagnostyka", null, (_, _) => Dispatcher.Invoke(() => MessageBox.Show(placement + "\nCPU: " + sample.Cpu.DisplayText + " " + sample.Cpu.Reason + "\nGPU: " + sample.Gpu.DisplayText + " " + sample.Gpu.Reason + "\n\n" + LogPath, "TempView"))); menu.Items.Add("Zakończ", null, (_, _) => Dispatcher.Invoke(() => Shutdown())); tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => Dispatcher.Invoke(OpenSettings);
        if (loaded.Warning != null) MessageBox.Show(loaded.Warning, "Konfiguracja TempView");
        sensorTask = Task.Run(ReadSensors); _ = Task.Run(LocateBars);
        var refresh = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; refresh.Tick += (_, _) => Render(); refresh.Start();
        if (e.Args.Contains("--settings") || !File.Exists(store.FilePath)) OpenSettings();
        if (e.Args.Contains("--smoke")) { var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) }; timer.Tick += (_, _) => Shutdown(); timer.Start(); }
    }
    void OpenSettings()
    {
        if (editor != null) { editor.Activate(); return; }
        editor = new SettingsWindow(settings, sample.Sensors, s => settings = s, s => { store.Save(s); settings = s; }); editor.Closed += (_, _) => editor = null; editor.Show();
    }
    Task? sensorTask; volatile BarSlot[] bars = []; DateTime lastLayout = DateTime.MinValue;
    async Task ReadSensors()
    {
        try
        {
            using var sensors = new Sensors();
            while (!stop.IsCancellationRequested)
            {
                var s = settings; sample = sensors.Read(s);
                await Task.Delay(TimeSpan.FromSeconds(s.RefreshSeconds), stop.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { sample = new(SensorReading.Unavailable(ex.Message), SensorReading.Unavailable(ex.Message), []); LogError(ex); }
    }
    async Task LocateBars()
    {
        var nextLog = DateTime.MinValue;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var s = settings; var left = Taskbars.Scan(s.Width, device => s.Monitors.FirstOrDefault(m => m.DeviceName == device)?.Side ?? "left");
                bars = left.ToArray(); lastLayout = DateTime.UtcNow;
                placement = string.Join("\n", bars.Select(b => b.Device + ": " + b.Status));
                if (bars.Length == 0) placement = "Brak paska zadań.";
                if (DateTime.UtcNow >= nextLog)
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                        File.WriteAllText(LogPath, JsonSerializer.Serialize(new { Time = DateTimeOffset.Now, Sample = sample, Bars = bars, SettingsPath = store.FilePath, ProcessMemoryBytes = Environment.WorkingSet }, new JsonSerializerOptions { WriteIndented = true })); nextLog = DateTime.UtcNow.AddSeconds(15);
                    }
                    catch (Exception ex) { LogError(ex); }
                }
            }
            catch (Exception ex) { bars = []; placement = ex.Message; LogError(ex); }
            try { await Task.Delay(1000, stop.Token); } catch (OperationCanceledException) { break; }
        }
    }
    void LogError(Exception ex) { try { Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!); File.WriteAllText(LogPath + ".error", ex.ToString()); } catch { } }
    void Render()
    {
        var s = settings; var current = sample with { Cpu = sample.Cpu.Expire(TimeSpan.FromSeconds(Math.Max(5, s.RefreshSeconds * 3)), DateTimeOffset.UtcNow), Gpu = sample.Gpu.Expire(TimeSpan.FromSeconds(Math.Max(5, s.RefreshSeconds * 3)), DateTimeOffset.UtcNow) };
        var selected = (DateTime.UtcNow - lastLayout > TimeSpan.FromSeconds(5) ? Array.Empty<BarSlot>() : bars).Where(b => s.MonitorMode == "all" || s.MonitorMode == "primary" && b.Primary || s.MonitorMode == "selected" && s.Monitors.Any(m => m.Enabled && m.DeviceName == b.Device)).ToArray();
        foreach (var key in widgets.Keys.Except(selected.Select(b => b.Device)).ToArray()) { widgets[key].Close(); widgets.Remove(key); }
        foreach (var b in selected) { if (!widgets.TryGetValue(b.Device, out var w)) widgets[b.Device] = w = new Widget(OpenSettings); w.Update(b, s, current); }
    }
    protected override void OnExit(ExitEventArgs e) { stop.Cancel(); sensorTask?.Wait(TimeSpan.FromSeconds(3)); tray?.Dispose(); base.OnExit(e); }
}
