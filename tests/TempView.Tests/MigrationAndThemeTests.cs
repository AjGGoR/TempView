using TempView.Core;
using Xunit;

namespace TempView.Tests;

public sealed class MigrationAndThemeTests
{
    const string UntouchedV1 = """
        {"SchemaVersion":1,"Theme":"auto","CpuColor":"#ECEFF4","GpuColor":"#ECEFF4","BackgroundColor":"#202020","BackgroundOpacity":0.85,
         "FontSize":11,"Width":112,"Horizontal":false,"ShowIcons":false,"Padding":4,"RefreshSeconds":1,"MonitorMode":"primary","Monitors":[],
         "CpuSensorId":null,"GpuSensorId":null,"CpuGradient":{"Enabled":false,"Points":[{"Temperature":40,"Color":"#7DDC97"}]},
         "GpuGradient":{"Enabled":true,"Points":[{"Temperature":30,"Color":"#00FF00"},{"Temperature":90,"Color":"#FF0000"}]}}
        """;

    const string CustomizedV1 = """
        {"SchemaVersion":1,"Theme":"dark","CpuColor":"#123456","GpuColor":"#ECEFF4","BackgroundColor":"#202020","BackgroundOpacity":0.5,
         "FontSize":10,"Width":81,"Horizontal":false,"ShowIcons":true,"Padding":2,"RefreshSeconds":1.75,"MonitorMode":"selected",
         "Monitors":[{"DeviceName":"\\\\.\\DISPLAY2","Enabled":true,"Side":"right"}],"CpuSensorId":"/amdcpu/0/temperature/2","GpuSensorId":null,
         "CpuGradient":{"Enabled":false,"Points":[{"Temperature":40,"Color":"#7DDC97"}]},"GpuGradient":{"Enabled":false,"Points":[{"Temperature":40,"Color":"#7DDC97"}]}}
        """;

    [Fact]
    public void UntouchedV1LayoutMovesToNewDefaultsAndKeepsOtherChoices()
    {
        var loaded = Load(UntouchedV1);
        Assert.Null(loaded.Warning);
        var s = loaded.Settings;
        Assert.Equal(AppSettings.CurrentSchemaVersion, s.SchemaVersion);
        Assert.False(s.CustomColors);
        Assert.True(s.AutoWidth);
        Assert.True(s.ShowIcons);
        Assert.Equal(new AppSettings().FontSize, s.FontSize);
        Assert.True(s.GpuGradient.Enabled);
        Assert.Equal(2, s.GpuGradient.Points.Count);
    }

    [Fact]
    public void CustomizedV1KeepsEveryExplicitValue()
    {
        var s = Load(CustomizedV1).Settings;
        Assert.True(s.CustomColors);
        Assert.False(s.AutoWidth);
        Assert.Equal((81d, 10d, 2d, 0.5, 1.75), (s.Width, s.FontSize, s.Padding, s.BackgroundOpacity, s.RefreshSeconds));
        Assert.Equal("#123456", s.CpuColor);
        Assert.Equal("right", Assert.Single(s.Monitors).Side);
        Assert.Equal("/amdcpu/0/temperature/2", s.CpuSensorId);
    }

    [Fact]
    public void LayoutChangeAloneKeepsThemeColors()
    {
        var s = new AppSettings { SchemaVersion = 1, CpuColor = "#eceff4", GpuColor = "#ECEFF4", BackgroundColor = "#202020", FontSize = 14, Padding = 4, ShowIcons = false };
        SettingsMigration.Migrate(s);
        Assert.False(s.CustomColors);
        Assert.False(s.AutoWidth);
        Assert.Equal(14, s.FontSize);
        Assert.False(s.ShowIcons);
    }

    [Fact]
    public void MigratedFileIsSavedAsCurrentSchema()
    {
        WithFile(CustomizedV1, (store, file) =>
        {
            store.Save(store.Load().Settings);
            var reloaded = new SettingsStore(file).Load();
            Assert.Null(reloaded.Warning);
            Assert.Contains("\"SchemaVersion\": 2", File.ReadAllText(file));
            Assert.Equal(81, reloaded.Settings.Width);
        });
    }

    [Fact]
    public void UnknownOldVersionIsTreatedAsUnreadable()
    {
        WithFile("{\"SchemaVersion\":0}", (store, _) => Assert.NotNull(store.Load().Warning));
    }

    [Theory]
    [InlineData("auto", true, true)]
    [InlineData("auto", false, false)]
    [InlineData("light", false, true)]
    [InlineData("dark", true, false)]
    public void ThemeFollowsSystemOnlyInAutoMode(string theme, bool systemLight, bool expected)
    {
        Assert.Equal(expected, WidgetTheme.IsLight(theme, systemLight));
        Assert.Equal(expected, WidgetTheme.Resolve(new AppSettings { Theme = theme }, systemLight, true).IsLight);
    }

    [Fact]
    public void SurfaceIsOpaqueWhenSystemTransparencyIsOff()
    {
        var settings = new AppSettings { BackgroundOpacity = 0.5 };
        Assert.StartsWith("#80", WidgetTheme.Resolve(settings, false, true).Surface);
        Assert.StartsWith("#FF", WidgetTheme.Resolve(settings, false, false).Surface);
        Assert.StartsWith("#FF", WidgetTheme.Resolve(new AppSettings { CustomColors = true, BackgroundOpacity = 0 }, false, false).Surface);
    }

    [Fact]
    public void CustomColorsAreUsedOnlyWhenEnabled()
    {
        var settings = new AppSettings { CpuColor = "#112233", GpuColor = "#445566", BackgroundColor = "#FAFAFA" };
        Assert.NotEqual("#112233", WidgetTheme.Resolve(settings, false, true).CpuText);
        settings.CustomColors = true;
        var palette = WidgetTheme.Resolve(settings, false, true);
        Assert.Equal(("#112233", "#445566"), (palette.CpuText, palette.GpuText));
        Assert.EndsWith("FAFAFA", palette.Surface);
        Assert.True(palette.IsLight);
    }

    [Fact]
    public void DefaultTextMeetsContrastOnDefaultSurfaces()
    {
        foreach (var light in new[] { false, true })
        {
            var palette = WidgetTheme.Resolve(new AppSettings(), light, false);
            Assert.True(ColorContrast.Ratio(palette.CpuText, WidgetTheme.OpaqueSurface(palette)) >= 7);
        }
    }

    [Fact]
    public void ContrastRatioMatchesWcagBounds()
    {
        Assert.Equal(21, ColorContrast.Ratio("#000000", "#FFFFFF"), 3);
        Assert.Equal(1, ColorContrast.Ratio("#7DDC97", "#7DDC97"), 6);
        Assert.Equal(ColorContrast.Ratio("#336699", "#FFFFFF"), ColorContrast.Ratio("#FFFFFF", "#FF336699"), 6);
    }

    static SettingsLoadResult Load(string json)
    {
        SettingsLoadResult? result = null;
        WithFile(json, (store, _) => result = store.Load());
        return result!;
    }

    static void WithFile(string json, Action<SettingsStore, string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "TempViewTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            var file = Path.Combine(path, "settings.json");
            File.WriteAllText(file, json);
            action(new SettingsStore(file), file);
        }
        finally { Directory.Delete(path, true); }
    }
}
