using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows;
using Forms = System.Windows.Forms;
namespace TempView;

public record BarSlot(string Device, bool Primary, [property: System.Text.Json.Serialization.JsonIgnore] nint Handle, int X, int Y, int Width, int Height, uint Dpi, string Status);
public static class Taskbars
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint FindWindow(string cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint FindWindowEx(nint parent, nint after, string cls, string? title);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    delegate bool EnumProc(nint h, nint p);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, nint p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(nint h, System.Text.StringBuilder text, int max);
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint h, out Rect r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint h);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(nint h);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(nint h, nint after, int x, int y, int w, int ht, uint flags);
    public static List<BarSlot> Scan(double desiredWidth, Func<string, string> sideForDevice)
    {
        var result = new List<BarSlot>(); var seen = new HashSet<nint>();
        bool Process(nint h, nint _)
        {
            if (!seen.Add(h)) return true;
            var cls = new System.Text.StringBuilder(128); GetClassName(h, cls, 128);
            if (cls.ToString() is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd")) return true;
            GetWindowRect(h, out var r); var screen = Forms.Screen.FromHandle(h); string side = sideForDevice(screen.DeviceName); uint dpi = GetDpiForWindow(h); double scale = dpi / 96d;
            int width = (int)Math.Ceiling(desiredWidth * scale), height = r.Bottom - r.Top;
            string status = "OK";
            if (!IsWindowVisible(h) || height < 20 || r.Top >= screen.Bounds.Bottom - 2 || r.Bottom <= screen.Bounds.Top + 2) status = "Pasek jest ukryty";
            if (r.Right - r.Left < height) status = "Pionowy pasek nie jest obsługiwany";
            var fg = GetForegroundWindow(); GetWindowRect(fg, out var fr);
            var fc = new System.Text.StringBuilder(128); GetClassName(fg, fc, 128);
            if (fc.ToString() is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") && fr.Left <= screen.Bounds.Left && fr.Top <= screen.Bounds.Top && fr.Right >= screen.Bounds.Right && fr.Bottom >= screen.Bounds.Bottom) status = "Aplikacja pełnoekranowa";
            var occupied = new List<(int L, int R)>(); bool startSeen = false;
            try
            {
                var root = AutomationElement.FromHandle(h);
                var all = root.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition);
                for (int i = 0; i < all.Count; i++)
                {
                    var c = all[i].Current; var b = c.BoundingRectangle;
                    // Buttons cover Start/apps/widgets/tray/clock. Text captures clock variants.
                    if (c.IsOffscreen || b.IsEmpty || b.Width <= 0 || b.Height <= 0) continue;
                    if (c.ControlType != ControlType.Button && c.ControlType != ControlType.Text && c.ControlType != ControlType.CheckBox && c.ControlType != ControlType.ListItem) continue;
                    if (b.Bottom <= r.Top || b.Top >= r.Bottom || b.Width > (r.Right - r.Left) * .85) continue;
                    if (c.Name.Equals("Start", StringComparison.OrdinalIgnoreCase) || c.AutomationId.Contains("Start", StringComparison.OrdinalIgnoreCase)) startSeen = true;
                    occupied.Add(((int)b.Left - 6, (int)b.Right + 6));
                }
                if (!startSeen || occupied.Count < 2) status = "Brak wiarygodnych granic przycisków paska (UI Automation)";
            }
            catch { status = "Nie można odczytać zajętego obszaru paska (UI Automation)"; }
            // Also reserve native notification area even if its accessible children vary by build.
            ReserveTray(h, occupied);
            int? gap = TempView.Core.TaskbarLayout.TryFindGap(r.Left + 8, r.Right - 8, width, side, occupied);
            if (gap == null) status = "Brak wolnego miejsca na pasku — zmniejsz widget lub liczbę przypiętych ikon";
            int x = gap ?? r.Left;
            result.Add(new(screen.DeviceName, screen.Primary, h, x, r.Top, width, height, dpi, status)); return true;
        }
        EnumWindows(Process, 0);
        var primary = FindWindow("Shell_TrayWnd", null); if (primary != 0) Process(primary, 0);
        nint secondary = 0; while ((secondary = FindWindowEx(0, secondary, "Shell_SecondaryTrayWnd", null)) != 0) Process(secondary, 0);
        return result;
    }
    [DllImport("user32.dll")] static extern bool EnumChildWindows(nint h, EnumProc callback, nint p);
    static void ReserveTray(nint h, List<(int L, int R)> list) => EnumChildWindows(h, (child, _) => { var c = new System.Text.StringBuilder(128); GetClassName(child, c, 128); if (c.ToString() == "TrayNotifyWnd" && GetWindowRect(child, out var r)) list.Add((r.Left - 6, r.Right + 6)); return true; }, 0);
}
