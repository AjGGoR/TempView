namespace TempView.Core;

public static class SettingsMigration
{
    // Values written by schema 1 when the user never changed them.
    const string V1Text = "#ECEFF4", V1Background = "#202020";
    const double V1FontSize = 11, V1Width = 112, V1Padding = 4;

    /// <summary>Upgrades settings in place to the current schema. Explicit user choices are kept.</summary>
    public static void Migrate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion == 1)
        {
            // Schema 1 used fixed default colors as an implicit "follow the theme" marker.
            settings.CustomColors = !(Same(settings.CpuColor, V1Text) && Same(settings.GpuColor, V1Text) && Same(settings.BackgroundColor, V1Background));
            var untouchedLayout = settings.FontSize == V1FontSize && settings.Width == V1Width && settings.Padding == V1Padding && !settings.ShowIcons && !settings.Horizontal;
            if (untouchedLayout)
            {
                var defaults = new AppSettings();
                settings.FontSize = defaults.FontSize;
                settings.Padding = defaults.Padding;
                settings.ShowIcons = defaults.ShowIcons;
                settings.AutoWidth = true;
            }
            else settings.AutoWidth = false;
            settings.SchemaVersion = 2;
        }
    }

    static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
