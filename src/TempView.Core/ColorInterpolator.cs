using System.Globalization;

namespace TempView.Core;

public static class ColorInterpolator
{
    public static string Interpolate(IEnumerable<ColorStop> points, double temperature)
    {
        if (!double.IsFinite(temperature)) throw new ArgumentOutOfRangeException(nameof(temperature));
        var ordered = points.OrderBy(p => p.Temperature).ToArray();
        if (ordered.Length == 0 || ordered.Any(p => !double.IsFinite(p.Temperature) || !SettingsValidator.IsColor(p.Color)) || ordered.GroupBy(p => p.Temperature).Any(g => g.Count() > 1)) throw new ArgumentException("Invalid color stops.", nameof(points));
        if (temperature <= ordered[0].Temperature) return ordered[0].Color;
        if (temperature >= ordered[^1].Temperature) return ordered[^1].Color;
        var upper = Array.FindIndex(ordered, p => p.Temperature >= temperature);
        var low = ordered[upper - 1];
        var high = ordered[upper];
        return Mix(low.Color, high.Color, (temperature - low.Temperature) / (high.Temperature - low.Temperature));
    }

    public static string Mix(string from, string to, double amount)
    {
        if (!SettingsValidator.IsColor(from) || !SettingsValidator.IsColor(to) || !double.IsFinite(amount)) throw new ArgumentException("Invalid color or interpolation amount.");
        amount = Math.Clamp(amount, 0, 1);
        return "#" + string.Concat(Enumerable.Range(0, 3).Select(i =>
        {
            var a = int.Parse(from.AsSpan(1 + i * 2, 2), NumberStyles.HexNumber);
            var b = int.Parse(to.AsSpan(1 + i * 2, 2), NumberStyles.HexNumber);
            return ((int)Math.Round(a + (b - a) * amount, MidpointRounding.AwayFromZero)).ToString("X2");
        }));
    }

    public static double Smooth(double current, double target, double elapsedSeconds, double timeConstantSeconds = 0.25)
    {
        if (!double.IsFinite(current) || !double.IsFinite(target) || !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0 || !double.IsFinite(timeConstantSeconds) || timeConstantSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        return current + (target - current) * (1 - Math.Exp(-elapsedSeconds / timeConstantSeconds));
    }
}
