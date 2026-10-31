using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using TempView.Core;
using Path = System.Windows.Shapes.Path;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
namespace TempView;

/// <summary>Stroke icons on a 16×16 grid. Original TempView artwork.</summary>
public static class Icons
{
    public static readonly Geometry Cpu = Geometry.Parse("M5,4 H11 A1,1 0 0 1 12,5 V11 A1,1 0 0 1 11,12 H5 A1,1 0 0 1 4,11 V5 A1,1 0 0 1 5,4 Z M6.75,6.75 H9.25 V9.25 H6.75 Z M6.5,1.75 V4 M9.5,1.75 V4 M6.5,12 V14.25 M9.5,12 V14.25 M1.75,6.5 H4 M1.75,9.5 H4 M12,6.5 H14.25 M12,9.5 H14.25");
    public static readonly Geometry Gpu = Geometry.Parse("M2,4 H14 A0.75,0.75 0 0 1 14.75,4.75 V11.25 A0.75,0.75 0 0 1 14,12 H2 Z M2,2.25 V14.25 M4.5,12 V13.75 H8 V12 M8.25,8 A2.25,2.25 0 1 1 3.75,8 A2.25,2.25 0 1 1 8.25,8 Z M10.5,6.5 H12.5 M10.5,9.5 H12.5");
    public static readonly Geometry Thermometer = Geometry.Parse("M6.5,9.6 V3.5 A1.5,1.5 0 0 1 9.5,3.5 V9.6 A3,3 0 1 1 6.5,9.6 Z");
    public static readonly Geometry ThermometerLevel = Geometry.Parse("M8,6.5 V11 M9.3,12.2 A1.3,1.3 0 1 1 6.7,12.2 A1.3,1.3 0 1 1 9.3,12.2 Z");

    public static Path Create(Geometry geometry, double size, Brush stroke, double thickness = 1.2)
    {
        var scaled = geometry.Clone(); scaled.Transform = new ScaleTransform(size / 16, size / 16);
        return new Path { Data = scaled, Stroke = stroke, StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Width = size, Height = size, SnapsToDevicePixels = false };
    }
}

/// <summary>The CPU/GPU panel. Shared by the taskbar overlay and the settings preview.</summary>
public sealed class WidgetView : Border
{
    sealed class Row
    {
        public readonly Path Icon; public readonly TextBlock Label = new(), Value = new();
        public Color? Target;
        public Row(Geometry icon) { Icon = Icons.Create(icon, 14, Brushes.White); Label.Text = icon == Icons.Cpu ? "CPU" : "GPU"; }
    }
    static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");
    const double LineFactor = 1.3;
    readonly Grid grid = new();
    readonly Row cpu = new(Icons.Cpu), gpu = new(Icons.Gpu);
    AppSettings? appliedSettings; WidgetPalette? appliedPalette; bool appliedHighContrast; double appliedHeight, appliedWidth = -1;
    Brush surface = Brushes.Transparent, hover = Brushes.Transparent, pressed = Brushes.Transparent;
    bool animate = true, isPressed;

    public WidgetView()
    {
        Child = grid; SnapsToDevicePixels = true; Focusable = false; Cursor = Cursors.Hand;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        foreach (var row in new[] { cpu, gpu })
            foreach (var text in new[] { row.Label, row.Value })
            {
                text.FontFamily = Font; text.VerticalAlignment = VerticalAlignment.Center;
                text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                Typography.SetNumeralAlignment(text, FontNumeralAlignment.Tabular);
            }
        cpu.Value.FontWeight = gpu.Value.FontWeight = FontWeights.SemiBold;
        // Right-aligned in a fixed column: units line up and the panel never changes width.
        cpu.Value.TextAlignment = gpu.Value.TextAlignment = TextAlignment.Right;
        MouseEnter += (_, _) => { SetSurface(hover); if (appliedHighContrast) BorderBrush = SystemColors.HighlightBrush; };
        MouseLeave += (_, _) => { isPressed = false; SetSurface(surface); if (appliedHighContrast) BorderBrush = SystemColors.WindowTextBrush; };
        PreviewMouseLeftButtonDown += (_, _) => { isPressed = true; SetSurface(pressed); };
        PreviewMouseLeftButtonUp += (_, _) => { isPressed = false; SetSurface(IsMouseOver ? hover : surface); };
    }

    /// <summary>Panel height for a taskbar of the given height (DIP): a 48 DIP bar gets a 40 DIP panel.</summary>
    public static double PanelHeight(double barHeight) => barHeight >= 40 ? barHeight - 8 : Math.Max(20, barHeight - 4);

    static double FontFor(AppSettings s, double panelHeight) =>
        Math.Max(7, Math.Min(s.FontSize, s.Horizontal ? (panelHeight - 8) / LineFactor : (panelHeight - 6) / 2 / LineFactor));

    static double TextWidth(string text, double size, FontWeight weight) =>
        new FormattedText(text, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, new Typeface(Font, FontStyles.Normal, weight, FontStretches.Normal), size, Brushes.Black, 1).WidthIncludingTrailingWhitespace;

    static (double Icon, double Gap, double Label, double Value) Metrics(AppSettings s, double size) =>
        (s.ShowIcons ? Math.Round(size * 1.15) : 0, Math.Round(size * .5), TextWidth("GPU", size, FontWeights.Normal), Math.Ceiling(TextWidth("100°C", size, FontWeights.SemiBold)));

    /// <summary>Content width in DIP for a 40 DIP panel. Uses worst-case strings so the panel never resizes while readings change.</summary>
    public static double MeasureWidth(AppSettings s, double panelHeight = 40)
    {
        var size = FontFor(s, panelHeight); var m = Metrics(s, size);
        double row = (m.Icon > 0 ? m.Icon + m.Gap : 0) + m.Label + m.Gap * 1.5 + m.Value;
        return Math.Ceiling((s.Horizontal ? row * 2 + size * 1.25 : row) + 2 * s.Padding + 2);
    }

    /// <summary>Applies layout and colors. Cheap when nothing changed.</summary>
    public void Apply(AppSettings s, WidgetPalette palette, bool highContrast, bool animations, double panelHeight, double? width)
    {
        animate = animations;
        if (ReferenceEquals(s, appliedSettings) && palette == appliedPalette && highContrast == appliedHighContrast && panelHeight == appliedHeight && (width ?? -1) == appliedWidth) return;
        appliedSettings = s; appliedPalette = palette; appliedHighContrast = highContrast; appliedHeight = panelHeight; appliedWidth = width ?? -1;

        Brush text, label, stroke;
        if (highContrast)
        {
            surface = hover = pressed = SystemColors.WindowBrush;
            text = label = SystemColors.WindowTextBrush; stroke = SystemColors.WindowTextBrush;
        }
        else
        {
            surface = Freeze(new SolidColorBrush(Parse(palette.Surface))); hover = Freeze(new SolidColorBrush(Parse(palette.SurfaceHover))); pressed = Freeze(new SolidColorBrush(Parse(palette.SurfacePressed)));
            stroke = Freeze(new SolidColorBrush(Parse(palette.Stroke)));
            text = Freeze(new SolidColorBrush(Parse(palette.CpuText))); label = text;
        }
        Background = IsMouseOver ? hover.CloneCurrentValue() : surface.CloneCurrentValue();
        BorderBrush = stroke; BorderThickness = new(highContrast ? 2 : 1); CornerRadius = new(6);
        Height = panelHeight; Padding = new(s.Padding, 0, s.Padding, 0);
        if (width is double w) Width = w; else ClearValue(WidthProperty);

        var size = FontFor(s, panelHeight);
        Build(s, size);
        foreach (var (row, color) in new[] { (cpu, palette.CpuText), (gpu, palette.GpuText) })
        {
            var rowText = highContrast ? text : Freeze(new SolidColorBrush(Parse(color)));
            row.Label.Foreground = rowText; row.Label.Opacity = highContrast ? 1 : .68;
            row.Icon.Stroke = rowText; row.Icon.Opacity = row.Label.Opacity;
            row.Target = null; row.Value.Foreground = rowText.CloneCurrentValue();
        }
        // Manual width narrower than the content: shrink text instead of clipping it.
        if (width is double limit)
        {
            Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            while (grid.DesiredSize.Width > limit - 2 * s.Padding - 2 && size > 7) { size -= .5; Build(s, size); grid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); }
        }
    }

    void Build(AppSettings s, double size)
    {
        var m = Metrics(s, size);
        grid.Children.Clear(); grid.RowDefinitions.Clear(); grid.ColumnDefinitions.Clear();
        grid.VerticalAlignment = VerticalAlignment.Center; grid.HorizontalAlignment = HorizontalAlignment.Center;
        void Column(double w) => grid.ColumnDefinitions.Add(new ColumnDefinition { Width = w < 0 ? GridLength.Auto : new GridLength(w) });
        void Place(Row row, int r, int c)
        {
            row.Icon.Visibility = s.ShowIcons ? Visibility.Visible : Visibility.Collapsed;
            if (s.ShowIcons) { var icon = Icons.Create(row == cpu ? Icons.Cpu : Icons.Gpu, m.Icon, row.Icon.Stroke); row.Icon.Data = icon.Data; row.Icon.Width = row.Icon.Height = m.Icon; }
            row.Icon.Margin = new(0, 0, m.Gap, 0);
            row.Label.Margin = new(0, 0, m.Gap * 1.5, 0);
            row.Value.MinWidth = m.Value;
            foreach (var t in new[] { row.Label, row.Value }) { t.FontSize = size; t.LineHeight = Math.Round(size * LineFactor); }
            foreach (var (e, i) in new UIElement[] { row.Icon, row.Label, row.Value }.Select((e, i) => (e, i)))
            {
                if (e is FrameworkElement { Parent: Panel parent } && parent != grid) parent.Children.Remove(e);
                Grid.SetRow(e, r); Grid.SetColumn(e, c + i); grid.Children.Add(e);
            }
        }
        if (s.Horizontal)
        {
            foreach (var w in new double[] { -1, -1, -1, size * 1.25, -1, -1, -1 }) Column(w);
            Place(cpu, 0, 0); Place(gpu, 0, 4);
        }
        else
        {
            for (int i = 0; i < 3; i++) Column(-1);
            grid.RowDefinitions.Add(new()); grid.RowDefinitions.Add(new());
            Place(cpu, 0, 0); Place(gpu, 1, 0);
        }
    }

    /// <summary>Updates the readings. Values use tabular figures inside a fixed-width column, so text never shifts.</summary>
    public void SetReadings(SensorReading cpuReading, SensorReading gpuReading, AppSettings s)
    {
        if (appliedPalette is null) return;
        Update(cpu, cpuReading, s.CpuGradient, appliedPalette.CpuText);
        Update(gpu, gpuReading, s.GpuGradient, appliedPalette.GpuText);
        // Explicit width: a long plain-string tooltip is sized before wrapping and clips its last line.
        ToolTip = new TextBlock { Text = Describe("CPU", cpuReading) + "\n" + Describe("GPU", gpuReading) + "\nKliknij, aby otworzyć ustawienia.", TextWrapping = TextWrapping.Wrap, MaxWidth = 340 };
        AutomationProperties.SetName(this, "Temperatury: CPU " + cpuReading.DisplayText + ", GPU " + gpuReading.DisplayText);
    }

    static string Describe(string name, SensorReading r) => r.IsAvailable
        ? $"{name}: {r.DisplayText}" + (r.Source is null ? "" : $" ({r.Source})")
        : $"{name}: brak odczytu. {r.Reason}";

    void Update(Row row, SensorReading reading, GradientSettings gradient, string baseColor)
    {
        row.Value.Text = reading.DisplayText;
        AutomationProperties.SetName(row.Value, row.Label.Text + " " + reading.DisplayText);
        if (appliedHighContrast) return;
        // Missing readings use the quieter label tone: N/A is shown as text, not only as a color.
        var target = !reading.IsAvailable ? Parse(baseColor) with { A = (byte)(Parse(baseColor).A * .68) }
            : gradient.Enabled ? Parse(ColorInterpolator.Interpolate(gradient.Points, reading.Value!.Value)) : Parse(baseColor);
        if (row.Target == target) return;
        var first = row.Target is null; row.Target = target;
        if (!first && animate && row.Value.Foreground is SolidColorBrush current && !current.IsFrozen)
            current.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(target, TimeSpan.FromMilliseconds(400)) { EasingFunction = new CubicEase() });
        else row.Value.Foreground = new SolidColorBrush(target);
    }

    void SetSurface(Brush target)
    {
        if (target is not SolidColorBrush to) { Background = target; return; }
        if (animate && Background is SolidColorBrush from && !from.IsFrozen)
            from.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to.Color, TimeSpan.FromMilliseconds(isPressed ? 60 : 150)));
        else Background = new SolidColorBrush(to.Color);
    }

    static Color Parse(string color) => (Color)ColorConverter.ConvertFromString(color);
    static T Freeze<T>(T brush) where T : Freezable { brush.Freeze(); return brush; }
}
