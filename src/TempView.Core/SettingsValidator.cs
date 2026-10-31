using System.Text.RegularExpressions;

namespace TempView.Core;

public static partial class SettingsValidator
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
    public static bool IsColor(string? color) => color is not null && ColorPattern().IsMatch(color);

    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        var errors = new List<string>();
        if (settings.SchemaVersion != AppSettings.CurrentSchemaVersion) errors.Add("Nieobsługiwana wersja konfiguracji.");
        if (settings.Theme is not ("auto" or "dark" or "light")) errors.Add("Nieprawidłowy motyw.");
        if (settings.MonitorMode is not ("primary" or "all" or "selected")) errors.Add("Nieprawidłowy tryb monitorów.");
        foreach (var color in new[] { settings.CpuColor, settings.GpuColor, settings.BackgroundColor })
            if (!IsColor(color)) errors.Add("Kolor musi mieć format #RRGGBB.");
        Range(settings.BackgroundOpacity, 0, 1, "Przezroczystość tła");
        Range(settings.FontSize, 8, 24, "Rozmiar tekstu");
        Range(settings.Width, 72, 400, "Szerokość");
        Range(settings.Padding, 0, 16, "Margines");
        Range(settings.RefreshSeconds, 0.25, 60, "Częstotliwość odświeżania");
        if (settings.Monitors is null) errors.Add("Brak listy monitorów.");
        else
        {
            if (settings.Monitors.Any(m => m is null || string.IsNullOrWhiteSpace(m.DeviceName) || m.Side is not ("left" or "right"))) errors.Add("Nieprawidłowa konfiguracja monitora.");
            if (settings.Monitors.Where(m => m is not null).GroupBy(m => m.DeviceName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) errors.Add("Powtórzony monitor.");
        }
        ValidateGradient(settings.CpuGradient, "CPU");
        ValidateGradient(settings.GpuGradient, "GPU");
        return errors;

        void Range(double value, double min, double max, string name)
        {
            if (!double.IsFinite(value) || value < min || value > max) errors.Add($"{name}: zakres {min}–{max}.");
        }
        void ValidateGradient(GradientSettings? gradient, string name)
        {
            if (gradient?.Points is null || gradient.Points.Count == 0) { errors.Add($"{name}: dodaj przynajmniej jeden punkt koloru."); return; }
            if (gradient.Points.Any(p => p is null || !double.IsFinite(p.Temperature) || p.Temperature < -50 || p.Temperature > 200 || !IsColor(p.Color))) errors.Add($"{name}: błędny punkt temperatury lub kolor.");
            if (gradient.Points.Where(p => p is not null).GroupBy(p => p.Temperature).Any(g => g.Count() > 1)) errors.Add($"{name}: temperatury punktów nie mogą się powtarzać.");
        }
    }
}
