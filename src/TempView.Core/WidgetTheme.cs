using System.Globalization;

namespace TempView.Core;

/// <summary>Panel colors as #AARRGGBB strings.</summary>
public sealed record WidgetPalette(string CpuText, string GpuText, string Surface, string SurfaceHover, string SurfacePressed, string Stroke, bool IsLight);

public static class WidgetTheme
{
    public static bool IsLight(string theme, bool systemLight) => theme == "light" || theme == "auto" && systemLight;

    /// <summary>Resolves the panel palette. Without system transparency the surface is opaque.</summary>
    public static WidgetPalette Resolve(AppSettings settings, bool systemLight, bool transparencyEnabled)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var light = IsLight(settings.Theme, systemLight);
        var alpha = transparencyEnabled ? (byte)Math.Round(Math.Clamp(settings.BackgroundOpacity, 0, 1) * 255) : (byte)255;
        string surface, hover, pressed, stroke, text;
        if (settings.CustomColors)
        {
            var bg = settings.BackgroundColor;
            var lightSurface = ColorContrast.RelativeLuminance(bg) > .4;
            surface = Argb(alpha, bg);
            hover = Argb(Math.Max(alpha, (byte)0xB0), ColorInterpolator.Mix(bg, lightSurface ? "#000000" : "#FFFFFF", .08));
            pressed = Argb(Math.Max(alpha, (byte)0xB0), ColorInterpolator.Mix(bg, lightSurface ? "#000000" : "#FFFFFF", .03));
            stroke = lightSurface ? "#14000000" : "#1AFFFFFF";
            return new(settings.CpuColor, settings.GpuColor, surface, hover, pressed, stroke, lightSurface);
        }
        if (light) { surface = Argb(alpha, "#F3F3F3"); hover = Argb(Math.Max(alpha, (byte)0xE0), "#FBFBFB"); pressed = Argb(Math.Max(alpha, (byte)0xE0), "#EDEDED"); stroke = "#14000000"; text = "#E41A1A1A"; }
        else { surface = Argb(alpha, "#2B2B2B"); hover = Argb(Math.Max(alpha, (byte)0xE0), "#363636"); pressed = Argb(Math.Max(alpha, (byte)0xE0), "#303030"); stroke = "#1AFFFFFF"; text = "#FFFFFFFF"; }
        return new(text, text, surface, hover, pressed, stroke, light);
    }

    /// <summary>Opaque surface color used for contrast checks (#RRGGBB).</summary>
    public static string OpaqueSurface(WidgetPalette palette) => "#" + palette.Surface[3..];

    static string Argb(byte alpha, string rgb) => "#" + alpha.ToString("X2", CultureInfo.InvariantCulture) + rgb[1..];
}

public static class ColorContrast
{
    /// <summary>WCAG 2 relative luminance of #RRGGBB or #AARRGGBB (alpha ignored).</summary>
    public static double RelativeLuminance(string color)
    {
        var rgb = color.Length == 9 ? color[3..] : color[1..];
        double Channel(int i)
        {
            var c = int.Parse(rgb.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(1) + 0.0722 * Channel(2);
    }

    /// <summary>WCAG 2 contrast ratio, 1–21.</summary>
    public static double Ratio(string a, string b)
    {
        double la = RelativeLuminance(a), lb = RelativeLuminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }
}
