using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using TempView.Core;
namespace TempView;

/// <summary>
/// Transparent, non-activating, topmost overlay window above a free area of one taskbar.
/// It is not part of Explorer and does not reserve taskbar space. Fully transparent pixels pass clicks to the taskbar.
/// </summary>
public sealed class Widget : Window
{
    readonly WidgetView view = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
    public Widget(Action settings, Func<ContextMenu> menu)
    {
        Title = "TempView · Panel"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Content = new Grid { Children = { view } };
        view.MouseLeftButtonUp += (_, _) => settings();
        view.ContextMenu = menu();
        // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW: never steals focus, hidden from Alt+Tab.
        SourceInitialized += (_, _) => { var h = new WindowInteropHelper(this).Handle; int ex = GetWindowLong(h, -20); SetWindowLong(h, -20, ex | 0x08000000 | 0x80); };
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetWindowLong(nint h, int index);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int SetWindowLong(nint h, int index, int value);

    public void Update(BarSlot bar, AppSettings s, WidgetPalette palette, SensorReading cpu, SensorReading gpu)
    {
        if (bar.Status != "OK") { Hide(); return; }
        double scale = bar.Dpi / 96d;
        view.Apply(s, palette, SystemTheme.HighContrast, SystemTheme.Animations, WidgetView.PanelHeight(bar.Height / scale), s.AutoWidth ? null : s.Width);
        view.SetReadings(cpu, gpu, s);
        if (!IsVisible) Show();
        // SWP_NOACTIVATE | SWP_SHOWWINDOW, HWND_TOPMOST.
        Taskbars.SetWindowPos(new WindowInteropHelper(this).Handle, new nint(-1), bar.X, bar.Y, bar.Width, bar.Height, 0x10 | 0x40);
    }
}
