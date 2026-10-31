namespace TempView.Core;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Theme { get; set; } = "auto";
    /// <summary>False: text and surface follow the theme. True: CpuColor, GpuColor and BackgroundColor are used.</summary>
    public bool CustomColors { get; set; }
    public string CpuColor { get; set; } = "#ECEFF4";
    public string GpuColor { get; set; } = "#ECEFF4";
    public string BackgroundColor { get; set; } = "#202020";
    public double BackgroundOpacity { get; set; } = 0.85;
    public double FontSize { get; set; } = 12;
    /// <summary>True: panel width follows its content. False: Width is used.</summary>
    public bool AutoWidth { get; set; } = true;
    public double Width { get; set; } = 112;
    public bool Horizontal { get; set; }
    public bool ShowIcons { get; set; } = true;
    public double Padding { get; set; } = 8;
    public double RefreshSeconds { get; set; } = 1;
    public string MonitorMode { get; set; } = "primary";
    public List<MonitorPreference> Monitors { get; set; } = [];
    public string? CpuSensorId { get; set; }
    public string? GpuSensorId { get; set; }
    public GradientSettings CpuGradient { get; set; } = new();
    public GradientSettings GpuGradient { get; set; } = new();
}

public sealed class MonitorPreference
{
    public string DeviceName { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string Side { get; set; } = "left";
}

public sealed class GradientSettings
{
    public bool Enabled { get; set; }
    public List<ColorStop> Points { get; set; } = [new() { Temperature = 40, Color = "#7DDC97" }, new() { Temperature = 80, Color = "#FF6B6B" }];
}

public sealed class ColorStop
{
    public double Temperature { get; set; }
    public string Color { get; set; } = "#FFFFFF";
}
