using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TempView.Core;
namespace TempView;

public sealed class Widget : Window
{
    readonly Dictionary<TextBlock, Color> targets = [];
    readonly Border border = new(); readonly StackPanel stack = new(); readonly TextBlock cpu = new(), gpu = new();
    public Widget(Action settings)
    {
        Title = "TempView · Panel"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true; FontFamily = new("Segoe UI Variable Text");
        border.Child = stack; Content = border; stack.Children.Add(cpu); stack.Children.Add(gpu);
        MouseLeftButtonUp += (_, _) => settings();
        var menu = new ContextMenu(); var open = new MenuItem { Header = "Ustawienia TempView" }; open.Click += (_, _) => settings(); menu.Items.Add(open); ContextMenu = menu;
        SourceInitialized += (_, _) => { var h = new WindowInteropHelper(this).Handle; int ex = GetWindowLong(h, -20); SetWindowLong(h, -20, ex | 0x08000000 | 0x80); };
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetWindowLong(nint h, int index);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int SetWindowLong(nint h, int index, int value);
    public void Update(BarSlot bar, AppSettings s, Sample sample)
    {
        if (bar.Status != "OK") { Hide(); return; }
        var light = s.Theme == "light" || s.Theme == "auto" && (int?)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) == 1;
        var bg = (Color)ColorConverter.ConvertFromString(s.BackgroundColor); if (s.Theme == "auto" && s.BackgroundColor == "#202020") bg = light ? Color.FromRgb(245, 245, 245) : Color.FromRgb(32, 32, 32);
        bg.A = (byte)Math.Round(s.BackgroundOpacity * 255); border.Background = new SolidColorBrush(bg); border.CornerRadius = new(4);
        stack.Orientation = s.Horizontal ? Orientation.Horizontal : Orientation.Vertical; stack.VerticalAlignment = VerticalAlignment.Center; stack.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
        double scale = bar.Dpi / 96d; double size = Math.Min(s.FontSize, Math.Max(7, (bar.Height / scale - 2 * s.Padding) / (s.Horizontal ? 1.4 : 2.7)));
        border.Padding = new(s.Padding, 0, s.Padding, 0);
        void Row(TextBlock text, string name, SensorReading reading, string fixedColor, GradientSettings gradient)
        {
            text.FontSize = size; text.Margin = new(2, 0, 2, 0); text.Text = (s.ShowIcons ? (name == "CPU" ? "▣ " : "▰ ") : "") + name + " – " + reading.DisplayText; text.ToolTip = reading.Reason ?? $"Pomiar: {reading.Timestamp.LocalDateTime:T}";
            string color = gradient.Enabled && reading.IsAvailable ? ColorInterpolator.Interpolate(gradient.Points, reading.Value!.Value) : fixedColor;
            if (!gradient.Enabled && light && fixedColor == "#ECEFF4") color = "#202020";
            var target = (Color)ColorConverter.ConvertFromString(color);
            if (targets.TryGetValue(text, out var previous) && previous == target) return;
            targets[text] = target;
            if (text.Foreground is SolidColorBrush current && !current.IsFrozen) current.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(target, TimeSpan.FromMilliseconds(250)));
            else text.Foreground = new SolidColorBrush(target);
        }
        Row(cpu, "CPU", sample.Cpu, s.CpuColor, s.CpuGradient); Row(gpu, "GPU", sample.Gpu, s.GpuColor, s.GpuGradient);
        stack.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        while (stack.DesiredSize.Width > bar.Width / scale - 2 * s.Padding && cpu.FontSize > 8) { cpu.FontSize -= .5; gpu.FontSize = cpu.FontSize; stack.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity)); }
        if (!IsVisible) Show();
        Taskbars.SetWindowPos(new WindowInteropHelper(this).Handle, new nint(-1), bar.X, bar.Y, bar.Width, bar.Height, 0x10 | 0x40);
    }
}
