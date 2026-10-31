using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using TempView.Core;
namespace TempView;

public sealed class App : Application
{
    const string OpenSignalName = @"Local\TempView.Desktop.OpenSettings";
    volatile AppSettings settings = new(); readonly SettingsStore store; readonly Dictionary<string, Widget> widgets = [];
    readonly CancellationTokenSource stop = new(); SettingsWindow? editor; System.Windows.Forms.NotifyIcon? tray; ContextMenu? trayMenu; volatile Sample sample = new(SensorReading.Unavailable("Oczekiwanie na pierwszy odczyt czujnika."), SensorReading.Unavailable("Oczekiwanie na pierwszy odczyt czujnika."), []);
    static string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TempView", "diagnostics.json");
    static string? SettingsPath;
    EventWaitHandle? openSignal;
    App() => store = new SettingsStore(SettingsPath);

    [STAThread]
    public static void Main(string[] args)
    {
        string? Arg(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        if (Arg("--export-icon") is { } icon) { Visuals.WriteIco(Path.GetFullPath(icon)); return; }
        using var mutex = new Mutex(true, @"Local\TempView.Desktop", out var created);
        if (!created)
        {
            // Already running: ask the running instance to show its settings instead of showing an error.
            try { using var signal = EventWaitHandle.OpenExisting(OpenSignalName); signal.Set(); }
            catch (WaitHandleCannotBeOpenedException) { MessageBox.Show("TempView już działa. Ustawienia otworzysz z ikony w obszarze powiadomień.", "TempView", MessageBoxButton.OK, MessageBoxImage.Information); }
            return;
        }
        if (Arg("--diagnostics") is { } diag) LogPath = Path.GetFullPath(diag);
        if (Arg("--settings-file") is { } file) SettingsPath = Path.GetFullPath(file);
        var app = new App(); app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, err) => { File.AppendAllText(Path.Combine(Path.GetTempPath(), "TempView-error.log"), err.Exception.ToString()); MessageBox.Show(err.Exception.Message, "TempView", MessageBoxButton.OK, MessageBoxImage.Error); err.Handled = true; Shutdown(1); };
        var loaded = store.Load(); settings = loaded.Settings;
        SystemTheme.Apply(this, settings.Theme);
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/TempView;component/Themes/TempView.xaml") });

        tray = new() { Text = "TempView", Visible = true };
        UpdateTrayIcon();
        tray.MouseClick += (_, m) =>
        {
            if (m.Button == System.Windows.Forms.MouseButtons.Left) Dispatcher.Invoke(() => OpenSettings());
            else if (m.Button == System.Windows.Forms.MouseButtons.Right) Dispatcher.Invoke(ShowTrayMenu);
        };
        SystemTheme.Changed += () => Dispatcher.BeginInvoke(new Action(() => { SystemTheme.Apply(this, settings.Theme); UpdateTrayIcon(); }));

        openSignal = new EventWaitHandle(false, EventResetMode.AutoReset, OpenSignalName);
        ThreadPool.RegisterWaitForSingleObject(openSignal, (_, _) => Dispatcher.BeginInvoke(new Action(() => OpenSettings())), null, -1, false);

        if (loaded.Warning != null) MessageBox.Show(loaded.Warning, "Konfiguracja TempView", MessageBoxButton.OK, MessageBoxImage.Warning);
        sensorTask = Task.Run(ReadSensors); _ = Task.Run(LocateBars);
        var refresh = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; refresh.Tick += (_, _) => Render(); refresh.Start();
        var settingsArg = Array.IndexOf(e.Args, "--settings");
        if (settingsArg >= 0 || !File.Exists(store.FilePath)) OpenSettings(settingsArg >= 0 && settingsArg + 1 < e.Args.Length && !e.Args[settingsArg + 1].StartsWith("--") ? e.Args[settingsArg + 1] : "look");
        if (e.Args.Contains("--smoke")) { var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) }; timer.Tick += (_, _) => Shutdown(); timer.Start(); }
    }

    void Apply(AppSettings s) { settings = s; SystemTheme.Apply(this, s.Theme); }

    void OpenSettings(string page = "look")
    {
        if (editor != null) { editor.Navigate(page); if (editor.WindowState == WindowState.Minimized) editor.WindowState = WindowState.Normal; editor.Activate(); return; }
        var context = new SettingsContext(() => sample, () => bars, Apply, s => { store.Save(s); Apply(s); }, store.FilePath, LogPath);
        editor = new SettingsWindow(settings, context, page); editor.Closed += (_, _) => editor = null; editor.Show(); editor.Activate();
    }

    /// <summary>Shared menu for the tray icon and the panel. Fluent-styled through the application theme.</summary>
    ContextMenu CreateMenu()
    {
        var menu = new ContextMenu();
        MenuItem Item(string text, string glyph, Action click)
        {
            var item = new MenuItem { Header = text, Icon = new Decorative { Text = glyph, FontFamily = new("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14 } };
            item.Click += (_, _) => click(); menu.Items.Add(item); return item;
        }
        Item("Ustawienia", "\uE713", () => OpenSettings());
        Item("Diagnostyka czujników", "\uE9D9", () => OpenSettings("sensors"));
        menu.Items.Add(new Separator());
        Item("Zakończ TempView", "\uE711", () => Shutdown());
        return menu;
    }

    void ShowTrayMenu()
    {
        trayMenu ??= CreateMenu();
        trayMenu.Placement = PlacementMode.MousePoint;
        trayMenu.Opened -= TrayMenuOpened; trayMenu.Opened += TrayMenuOpened;
        trayMenu.IsOpen = true;
    }

    // The menu must own the foreground window, otherwise it does not close when clicking elsewhere.
    static void TrayMenuOpened(object sender, RoutedEventArgs e) { if (PresentationSource.FromVisual((Visual)sender) is HwndSource source) SetForegroundWindow(source.Handle); }
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(nint handle);

    void UpdateTrayIcon()
    {
        if (tray is null) return;
        var old = tray.Icon;
        tray.Icon = Visuals.ToIcon(Visuals.TrayGlyph(Visuals.TrayIconSize(), SystemTheme.TaskbarLight, SystemTheme.HighContrast));
        old?.Dispose();
    }

    Task? sensorTask; volatile BarSlot[] bars = []; DateTime lastLayout = DateTime.MinValue;
    // Panel width in DIP measured on the UI thread; read by the taskbar scanner.
    double widgetWidth = 112; AppSettings? measuredFor;
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
                var s = settings; var width = Volatile.Read(ref widgetWidth);
                bars = Taskbars.Scan(width, device => s.Monitors.FirstOrDefault(m => m.DeviceName == device)?.Side ?? "left").ToArray(); lastLayout = DateTime.UtcNow;
                if (DateTime.UtcNow >= nextLog)
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                        File.WriteAllText(LogPath, JsonSerializer.Serialize(new { Time = DateTimeOffset.Now, Sample = sample, Bars = bars, WidgetWidth = width, SettingsPath = store.FilePath, ProcessMemoryBytes = Environment.WorkingSet }, new JsonSerializerOptions { WriteIndented = true })); nextLog = DateTime.UtcNow.AddSeconds(15);
                    }
                    catch (Exception ex) { LogError(ex); }
                }
            }
            catch (Exception ex) { bars = []; LogError(ex); }
            try { await Task.Delay(1000, stop.Token); } catch (OperationCanceledException) { break; }
        }
    }
    void LogError(Exception ex) { try { Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!); File.WriteAllText(LogPath + ".error", ex.ToString()); } catch { } }
    void Render()
    {
        var s = settings;
        if (!ReferenceEquals(s, measuredFor)) { measuredFor = s; Volatile.Write(ref widgetWidth, s.AutoWidth ? WidgetView.MeasureWidth(s) : s.Width); }
        var maxAge = TimeSpan.FromSeconds(Math.Max(5, s.RefreshSeconds * 3));
        var cpu = sample.Cpu.Expire(maxAge, DateTimeOffset.UtcNow); var gpu = sample.Gpu.Expire(maxAge, DateTimeOffset.UtcNow);
        var palette = WidgetTheme.Resolve(s, SystemTheme.TaskbarLight, SystemTheme.Transparency);
        var selected = (DateTime.UtcNow - lastLayout > TimeSpan.FromSeconds(5) ? Array.Empty<BarSlot>() : bars).Where(b => s.MonitorMode == "all" || s.MonitorMode == "primary" && b.Primary || s.MonitorMode == "selected" && s.Monitors.Any(m => m.Enabled && m.DeviceName == b.Device)).ToArray();
        foreach (var key in widgets.Keys.Except(selected.Select(b => b.Device)).ToArray()) { widgets[key].Close(); widgets.Remove(key); }
        foreach (var b in selected) { if (!widgets.TryGetValue(b.Device, out var w)) widgets[b.Device] = w = new Widget(() => OpenSettings(), CreateMenu); w.Update(b, s, palette, cpu, gpu); }
    }
    protected override void OnExit(ExitEventArgs e) { stop.Cancel(); sensorTask?.Wait(TimeSpan.FromSeconds(3)); if (tray != null) { tray.Visible = false; tray.Dispose(); } openSignal?.Dispose(); base.OnExit(e); }
}
