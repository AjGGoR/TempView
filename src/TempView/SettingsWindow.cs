using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;
using TempView.Core;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using DataGrid = System.Windows.Controls.DataGrid;
using Binding = System.Windows.Data.Binding;

namespace TempView;

public sealed class SettingsWindow : Window
{
    private readonly AppSettings original;
    private AppSettings draft;
    private readonly Action<AppSettings> preview;
    private readonly Action<AppSettings> save;
    private readonly TextBlock errors = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick, Margin = new Thickness(0, 8, 0, 8) };
    private readonly StackPanel monitorRows = new();
    private readonly Border sample = new() { CornerRadius = new CornerRadius(6), Padding = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock sampleCpu = new() { Text = "CPU —" };
    private readonly TextBlock sampleGpu = new() { Text = "GPU —" };
    private readonly StackPanel sampleRows = new();
    private readonly List<DataGrid> gradientGrids = [];
    private bool ready;
    private bool saved;

    public SettingsWindow(AppSettings initial, IReadOnlyList<SensorOption> sensors, Action<AppSettings> preview, Action<AppSettings> save)
    {
        original = Clone(initial);
        draft = Clone(initial);
        this.preview = preview;
        this.save = save;
        Title = "TempView · Ustawienia";
        Width = 700; Height = 720; MinWidth = 590; MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI Variable Text"); FontSize = 14;
        Background = new SolidColorBrush(Color.FromRgb(246, 247, 249));
        var root = new DockPanel { Margin = new Thickness(22) };
        Content = root;
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        header.Children.Add(new TextBlock { Text = "TempView", FontSize = 25, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock { Text = "Zmiany mają podgląd na żywo. Zapisz, aby zachować je po restarcie.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 10) });
        sampleRows.Children.Add(sampleCpu); sampleRows.Children.Add(sampleGpu); sample.Child = sampleRows; header.Children.Add(sample);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var footer = new StackPanel();
        footer.Children.Add(errors);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(MakeButton("Zapisz domyślne", (_, _) =>
        {
            try { save(new AppSettings()); saved = true; Close(); }
            catch (Exception ex) { errors.Text = "Nie udało się zapisać ustawień: " + ex.Message; }
        }));
        buttons.Children.Add(MakeButton("Anuluj", (_, _) => Close()));
        var apply = MakeButton("Zapisz", (_, _) =>
        {
            foreach (var grid in gradientGrids)
                if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true))
                { errors.Text = "Popraw błędną wartość w tabeli punktów temperatury."; return; }
            FocusManager.SetFocusedElement(this, null);
            Keyboard.ClearFocus();
            var problems = SettingsValidator.Validate(draft);
            if (problems.Count > 0) { errors.Text = string.Join("\n", problems); return; }
            try { save(Clone(draft)); saved = true; Close(); }
            catch (Exception ex) { errors.Text = "Nie udało się zapisać ustawień: " + ex.Message; }
        });
        apply.IsDefault = true; buttons.Children.Add(apply); footer.Children.Add(buttons);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var tabs = new TabControl(); root.Children.Add(tabs);
        var look = Page(tabs, "Wygląd");
        AddCombo(look, "Motyw", new[] { ("auto", "Systemowy"), ("dark", "Ciemny"), ("light", "Jasny") }, draft.Theme, v => draft.Theme = v);
        AddText(look, "Kolor CPU (#RRGGBB)", draft.CpuColor, v => draft.CpuColor = v);
        AddText(look, "Kolor GPU (#RRGGBB)", draft.GpuColor, v => draft.GpuColor = v);
        AddText(look, "Kolor tła (#RRGGBB)", draft.BackgroundColor, v => draft.BackgroundColor = v);
        AddSlider(look, "Krycie tła", 0, 1, draft.BackgroundOpacity, v => draft.BackgroundOpacity = v, .05);
        AddSlider(look, "Rozmiar tekstu (DIP)", 8, 24, draft.FontSize, v => draft.FontSize = v, 1);
        AddSlider(look, "Szerokość (DIP)", 72, 400, draft.Width, v => draft.Width = v, 1);
        AddSlider(look, "Margines i odstęp (DIP)", 0, 16, draft.Padding, v => draft.Padding = v, 1);
        AddCheck(look, "Układ poziomy", draft.Horizontal, v => draft.Horizontal = v);
        AddCheck(look, "Ikony CPU i GPU", draft.ShowIcons, v => draft.ShowIcons = v);
        look.Children.Add(Note("Tekst pozostaje nieprzezroczysty. Rozmiar panelu jest ograniczony wysokością paska i dostępnym miejscem."));
        var monitors = Page(tabs, "Monitory");
        AddCombo(monitors, "Wyświetlaj", new[] { ("primary", "Na monitorze głównym"), ("all", "Na wszystkich monitorach z paskiem"), ("selected", "Na wybranych monitorach") }, draft.MonitorMode, v => draft.MonitorMode = v);
        monitors.Children.Add(Note("Stronę ustawisz osobno dla każdego ekranu. W trybie wybranych ekranów zaznacz widoczność. Brak paska lub bezpiecznego miejsca oznacza ukrycie panelu; ustawienia pozostają dostępne w zasobniku."));
        monitors.Children.Add(monitorRows);
        monitors.Children.Add(MakeButton("Odśwież listę ekranów", (_, _) => RefreshMonitors()));
        RefreshMonitors();
        var readings = Page(tabs, "Czujniki");
        AddSlider(readings, "Odczyt co (sekundy)", .25, 60, draft.RefreshSeconds, v => draft.RefreshSeconds = v, .25);
        AddSensor(readings, "Czujnik CPU", sensors.Where(s => s.Kind.Equals("CPU", StringComparison.OrdinalIgnoreCase)), draft.CpuSensorId, v => draft.CpuSensorId = v);
        AddSensor(readings, "Czujnik GPU", sensors.Where(s => s.Kind.Equals("GPU", StringComparison.OrdinalIgnoreCase)), draft.GpuSensorId, v => draft.GpuSensorId = v);
        readings.Children.Add(Note("N/A oznacza brak aktualnego odczytu. Powód jest dostępny po wskazaniu panelu. Część czujników CPU wymaga uruchomienia aplikacji jako administrator. Nie wyłączaj zabezpieczeń Windows. Po zmianie urządzeń otwórz ustawienia ponownie, aby odświeżyć listę czujników."));
        var gradients = Page(tabs, "Kolory temperatur");
        gradients.Children.Add(Note("Własne punkty −50…200°C i kolory #RRGGBB. Temperatury muszą być unikalne. Między punktami kolor zmienia się płynnie; poza zakresem używany jest kolor skrajny."));
        AddGradient(gradients, "CPU", draft.CpuGradient);
        AddGradient(gradients, "GPU", draft.GpuGradient);
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        Closed += (_, _) => { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged; if (!saved) preview(Clone(original)); };
        ready = true; Changed();
    }

    private void DisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(RefreshMonitors));

    private void RefreshMonitors()
    {
        monitorRows.Children.Clear();
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var preference = draft.Monitors.FirstOrDefault(m => m.DeviceName.Equals(screen.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (preference is null) { preference = new MonitorPreference { DeviceName = screen.DeviceName }; draft.Monitors.Add(preference); }
            var pref = preference;
            var row = new StackPanel { Margin = new Thickness(0, 10, 0, 8) };
            row.Children.Add(new TextBlock { Text = $"{screen.DeviceName} · {screen.Bounds.Width} × {screen.Bounds.Height}" + (screen.Primary ? " · główny" : ""), FontWeight = FontWeights.SemiBold });
            AddCheck(row, "Wybrany ekran", pref.Enabled, v => pref.Enabled = v);
            AddCombo(row, "Strona paska", new[] { ("left", "Lewa"), ("right", "Prawa") }, pref.Side, v => pref.Side = v);
            monitorRows.Children.Add(row);
        }
        Changed();
    }

    private void AddGradient(StackPanel parent, string name, GradientSettings gradient)
    {
        parent.Children.Add(new TextBlock { Text = name, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 4) });
        AddCheck(parent, "Kolor zależny od temperatury", gradient.Enabled, v => gradient.Enabled = v);
        var points = new ObservableCollection<ColorStop>(gradient.Points);
        var grid = new DataGrid { ItemsSource = points, AutoGenerateColumns = false, CanUserAddRows = false, Height = 150, Margin = new Thickness(0, 6, 0, 4), SelectionMode = DataGridSelectionMode.Single };
        gradientGrids.Add(grid);
        grid.CanUserDeleteRows = false;
        grid.Columns.Add(new DataGridTextColumn { Header = "Temperatura °C", Binding = new Binding(nameof(ColorStop.Temperature)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Kolor #RRGGBB", Binding = new Binding(nameof(ColorStop.Color)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        void Synchronize() { gradient.Points = points.ToList(); Changed(); }
        grid.CellEditEnding += (_, _) => Dispatcher.BeginInvoke(new Action(Synchronize));
        points.CollectionChanged += (_, _) => Synchronize();
        parent.Children.Add(grid);
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        controls.Children.Add(MakeButton("Dodaj punkt", (_, _) =>
        {
            var temp = 50d; while (points.Any(p => p.Temperature == temp) && temp <= 200) temp++;
            if (temp <= 200) points.Add(new ColorStop { Temperature = temp, Color = "#FFFFFF" });
        }));
        controls.Children.Add(MakeButton("Usuń zaznaczony", (_, _) => { if (grid.SelectedItem is ColorStop point) points.Remove(point); }));
        parent.Children.Add(controls);
    }

    private void Changed()
    {
        if (!ready) return;
        var problems = SettingsValidator.Validate(draft);
        errors.Text = string.Join("\n", problems);
        if (problems.Count > 0) return;
        sample.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(draft.BackgroundColor)) { Opacity = draft.BackgroundOpacity };
        sampleCpu.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(draft.CpuColor));
        sampleGpu.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(draft.GpuColor));
        sampleCpu.FontSize = sampleGpu.FontSize = draft.FontSize;
        sampleRows.Orientation = draft.Horizontal ? Orientation.Horizontal : Orientation.Vertical;
        sampleCpu.Margin = new Thickness(0, 0, draft.Horizontal ? draft.Padding : 0, 0);
        sample.Padding = new Thickness(draft.Padding);
        sample.Width = draft.Width;
        preview(Clone(draft));
    }
    private static AppSettings Clone(AppSettings settings) => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
    private static StackPanel Page(TabControl tabs, string title)
    {
        var panel = new StackPanel { Margin = new Thickness(12) };
        tabs.Items.Add(new TabItem { Header = title, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        return panel;
    }
    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 8) };
    private static Button MakeButton(string text, RoutedEventHandler click)
    {
        var button = new Button { Content = text, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(4) };
        button.Click += click; return button;
    }
    private void AddCheck(StackPanel panel, string label, bool value, Action<bool> setter)
    {
        var check = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 6, 0, 6) };
        check.Checked += (_, _) => { setter(true); Changed(); };
        check.Unchecked += (_, _) => { setter(false); Changed(); };
        panel.Children.Add(check);
    }
    private void AddText(StackPanel panel, string label, string value, Action<string> setter)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 4) });
        var field = new TextBox { Text = value, Padding = new Thickness(6), MaxLength = 7 };
        field.TextChanged += (_, _) => { setter(field.Text.Trim()); Changed(); }; panel.Children.Add(field);
    }
    private void AddSlider(StackPanel panel, string label, double min, double max, double value, Action<double> setter, double tick)
    {
        var caption = new TextBlock { Text = $"{label}: {value:0.##}", Margin = new Thickness(0, 10, 0, 4) };
        panel.Children.Add(caption);
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = tick, IsSnapToTickEnabled = true, Margin = new Thickness(0, 3, 0, 8) };
        slider.ValueChanged += (_, _) => { setter(slider.Value); caption.Text = $"{label}: {slider.Value:0.##}"; Changed(); };
        panel.Children.Add(slider);
    }
    private void AddCombo(StackPanel panel, string label, IEnumerable<(string Id, string Label)> options, string value, Action<string> setter)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 4) });
        var choices = options.Select(x => new Choice(x.Id, x.Label)).ToList();
        var combo = new ComboBox { ItemsSource = choices, DisplayMemberPath = nameof(Choice.Label), SelectedValuePath = nameof(Choice.Id), SelectedValue = value, Padding = new Thickness(4) };
        combo.SelectionChanged += (_, _) => { if (combo.SelectedValue is string selected) { setter(selected); Changed(); } }; panel.Children.Add(combo);
    }
    private void AddSensor(StackPanel panel, string label, IEnumerable<SensorOption> sensors, string? selected, Action<string?> setter)
    {
        var choices = new List<(string Id, string Label)> { ("", "Automatyczny wybór") };
        choices.AddRange(sensors.Select(s => (s.Id, s.Name)));
        if (!string.IsNullOrEmpty(selected) && choices.All(c => c.Id != selected)) choices.Add((selected, selected + " (niedostępny)"));
        AddCombo(panel, label, choices, selected ?? "", v => setter(string.IsNullOrEmpty(v) ? null : v));
    }
    private sealed record Choice(string Id, string Label);
}
