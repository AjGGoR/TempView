using TempView.Core;
using Xunit;

namespace TempView.Tests;

public sealed class CoreTests
{
    private static ColorStop[] Stops => [new() { Temperature = 80, Color = "#FF0000" }, new() { Temperature = 20, Color = "#000000" }];

    [Theory]
    [InlineData(-10, "#000000")]
    [InlineData(20, "#000000")]
    [InlineData(50, "#800000")]
    [InlineData(80, "#FF0000")]
    [InlineData(120, "#FF0000")]
    public void InterpolationSortsAndClamps(double temperature, string expected) => Assert.Equal(expected, ColorInterpolator.Interpolate(Stops, temperature));

    [Fact]
    public void DuplicateThresholdIsRejected()
    {
        var settings = new AppSettings();
        settings.CpuGradient.Points = [new() { Temperature = 40 }, new() { Temperature = 40 }];
        Assert.Contains(SettingsValidator.Validate(settings), e => e.Contains("powtarzać"));
        Assert.Throws<ArgumentException>(() => ColorInterpolator.Interpolate(settings.CpuGradient.Points, 40));
    }

    [Fact]
    public void SmoothConvergesWithoutOvershoot()
    {
        var value = ColorInterpolator.Smooth(0, 100, .1);
        Assert.InRange(value, 0.001, 99.999);
        Assert.True(ColorInterpolator.Smooth(value, 100, .1) > value);
        Assert.Equal(value, ColorInterpolator.Smooth(value, 100, 0));
        Assert.Equal(ColorInterpolator.Smooth(0, 100, .2), ColorInterpolator.Smooth(value, 100, .1), 8);
    }

    [Fact]
    public void DefaultsAreValid() => Assert.Empty(SettingsValidator.Validate(new()));

    [Fact]
    public void InvalidInputsAreRejected()
    {
        var settings = new AppSettings { FontSize = double.NaN, RefreshSeconds = 0, BackgroundOpacity = 2, CpuColor = "red", MonitorMode = "invalid", Theme = "invalid" };
        settings.GpuGradient.Points[0].Temperature = double.PositiveInfinity;
        Assert.True(SettingsValidator.Validate(settings).Count >= 7);
        settings.Monitors = null!;
        settings.CpuGradient = null!;
        Assert.NotEmpty(SettingsValidator.Validate(settings));
    }

    [Fact]
    public void SettingsRoundTripAndOverwriteAreAtomic()
    {
        WithDirectory(path =>
        {
            var store = new SettingsStore(Path.Combine(path, "settings.json"));
            var settings = new AppSettings { CpuColor = "#123456", RefreshSeconds = 2, MonitorMode = "selected" };
            settings.Monitors.Add(new() { DeviceName = "DISPLAY2", Side = "right" });
            store.Save(settings);
            settings.Width = 150;
            store.Save(settings);
            var loaded = store.Load();
            Assert.Null(loaded.Warning);
            Assert.Equal("#123456", loaded.Settings.CpuColor);
            Assert.Equal(150, loaded.Settings.Width);
            Assert.Equal("right", Assert.Single(loaded.Settings.Monitors).Side);
            Assert.Empty(Directory.GetFiles(path, "*.tmp"));
        });
    }

    [Fact]
    public void CorruptSettingsHaveBackupAndDefaults()
    {
        WithDirectory(path =>
        {
            var file = Path.Combine(path, "settings.json");
            File.WriteAllText(file, "broken json");
            var loaded = new SettingsStore(file).Load();
            Assert.NotNull(loaded.Warning);
            Assert.Empty(SettingsValidator.Validate(loaded.Settings));
            Assert.Equal("broken json", File.ReadAllText(Assert.Single(Directory.GetFiles(path, "*.bak"))));
        });
    }

    [Fact]
    public void FutureSettingsCannotBeOverwritten()
    {
        WithDirectory(path =>
        {
            var file = Path.Combine(path, "settings.json");
            File.WriteAllText(file, "{\"SchemaVersion\":99}");
            var store = new SettingsStore(file);
            Assert.NotNull(store.Load().Warning);
            Assert.Throws<InvalidOperationException>(() => store.Save(new()));
            Assert.Contains("99", File.ReadAllText(file));
        });
    }

    [Fact]
    public void InvalidSavePreservesPreviousSettings()
    {
        WithDirectory(path =>
        {
            var store = new SettingsStore(Path.Combine(path, "settings.json"));
            store.Save(new());
            Assert.Throws<ArgumentException>(() => store.Save(new() { RefreshSeconds = -1 }));
            Assert.Equal(1, store.Load().Settings.RefreshSeconds);
        });
    }

    [Fact]
    public void UnavailableAndInvalidSensorsNeverBecomeZero()
    {
        var missing = SensorReading.Unavailable("No permission");
        Assert.Null(missing.Value);
        Assert.Equal("N/A", missing.DisplayText);
        Assert.False(SensorReading.Available(double.NaN).IsAvailable);
        Assert.False(SensorReading.Available(400).IsAvailable);
        Assert.Equal("0°C", SensorReading.Available(0).DisplayText);
        var stale = SensorReading.Available(55).Expire(TimeSpan.FromSeconds(2), DateTimeOffset.UtcNow.AddSeconds(3));
        Assert.False(stale.IsAvailable);
        Assert.NotNull(stale.Reason);
    }

    private static void WithDirectory(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "TempViewTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try { action(path); }
        finally { Directory.Delete(path, true); }
    }
}
