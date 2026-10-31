using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;
namespace TempView;

/// <summary>Cached Windows personalization state. Refreshed when Windows reports a preference change.</summary>
public static class SystemTheme
{
    const string Personalize = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    static bool taskbarLight, appsLight, transparency;
    public static event Action? Changed;

    static SystemTheme()
    {
        Read();
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.Accessibility or UserPreferenceCategory.VisualStyle)) return;
            var before = (taskbarLight, appsLight, transparency, HighContrast);
            Read();
            if (before != (taskbarLight, appsLight, transparency, HighContrast)) Changed?.Invoke();
        };
    }

    static void Read()
    {
        taskbarLight = (Registry.GetValue(Personalize, "SystemUsesLightTheme", 0) as int? ?? 0) == 1;
        appsLight = (Registry.GetValue(Personalize, "AppsUseLightTheme", 1) as int? ?? 1) == 1;
        transparency = (Registry.GetValue(Personalize, "EnableTransparency", 1) as int? ?? 1) == 1;
    }

    /// <summary>Taskbar and tray color mode ("Choose your default Windows mode").</summary>
    public static bool TaskbarLight => taskbarLight;
    public static bool AppsLight => appsLight;
    public static bool Transparency => transparency && !HighContrast;
    public static bool HighContrast => SystemParameters.HighContrast;
    /// <summary>"Animation effects" in Windows accessibility settings.</summary>
    public static bool Animations => SystemParameters.ClientAreaAnimation && !HighContrast;

    /// <summary>Applies the Fluent theme for windows, menus and tooltips.</summary>
    public static void Apply(Application app, string theme)
    {
#pragma warning disable WPF0001 // ThemeMode is the supported WPF Fluent switch; still flagged experimental in .NET 10.
        var mode = HighContrast ? ThemeMode.System : theme switch { "light" => ThemeMode.Light, "dark" => ThemeMode.Dark, _ => ThemeMode.System };
        if (app.ThemeMode != mode) app.ThemeMode = mode;
#pragma warning restore WPF0001
    }
}

/// <summary>Icons rendered from TempView's own vector artwork.</summary>
public static class Visuals
{
    static readonly Color Tile = Color.FromRgb(0x00, 0x5F, 0xB8);

    /// <summary>App icon: white thermometer on a blue rounded tile.</summary>
    public static BitmapSource AppIcon(int size = 64) => Render(size, dc =>
    {
        double s = size, r = s * .22;
        dc.DrawRoundedRectangle(new SolidColorBrush(Tile), null, new Rect(0, 0, s, s), r, r);
        DrawThermometer(dc, Brushes.White, s * .14, s * .72, Math.Max(1, s / 16 * 1.1));
    });

    /// <summary>Monochrome tray glyph matching the taskbar color mode.</summary>
    public static BitmapSource TrayGlyph(int size, bool lightTaskbar, bool highContrast) => Render(size, dc =>
    {
        var color = highContrast ? SystemColors.WindowTextColor : lightTaskbar ? Color.FromRgb(0x1A, 0x1A, 0x1A) : Colors.White;
        DrawThermometer(dc, new SolidColorBrush(color), 0, size, Math.Max(1, size / 16d * 1.25));
    });

    static void DrawThermometer(DrawingContext dc, Brush brush, double offset, double extent, double thickness)
    {
        var k = extent / 16;
        dc.PushTransform(new TranslateTransform(offset, offset));
        dc.PushTransform(new ScaleTransform(k, k));
        var pen = new Pen(brush, thickness / k) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, pen, Icons.Thermometer);
        dc.DrawGeometry(brush, pen, Icons.ThermometerLevel);
        dc.Pop(); dc.Pop();
    }

    static BitmapSource Render(int size, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) draw(dc);
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze();
        return bitmap;
    }

    public static System.Drawing.Icon ToIcon(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream(); encoder.Save(stream); stream.Position = 0;
        using var bitmap = new System.Drawing.Bitmap(stream);
        var handle = bitmap.GetHicon();
        try { return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    /// <summary>Writes a multi-resolution .ico (PNG frames). Used to regenerate Assets/TempView.ico.</summary>
    public static void WriteIco(string path)
    {
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 256];
        var frames = sizes.Select(s => { var e = new PngBitmapEncoder(); e.Frames.Add(BitmapFrame.Create(AppIcon(s))); using var m = new MemoryStream(); e.Save(m); return m.ToArray(); }).ToArray();
        using var w = new BinaryWriter(File.Create(path));
        w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
        var offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
            w.Write(frames[i].Length); w.Write(offset); offset += frames[i].Length;
        }
        foreach (var f in frames) w.Write(f);
    }

    [DllImport("user32.dll")] static extern bool DestroyIcon(nint handle);
    [DllImport("user32.dll")] static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("user32.dll")] static extern uint GetDpiForSystem();
    /// <summary>Small icon size for the notification area at the system DPI.</summary>
    public static int TrayIconSize() { try { return GetSystemMetricsForDpi(49, GetDpiForSystem()); } catch { return 16; } }
}
