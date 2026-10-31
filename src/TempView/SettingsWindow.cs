using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TempView.Core;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using RadioButton = System.Windows.Controls.RadioButton;
using ListBox = System.Windows.Controls.ListBox;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace TempView;

/// <summary>What the settings window may read from and do to the running application.</summary>
public sealed record SettingsContext(Func<Sample> Sample, Func<IReadOnlyList<BarSlot>> Bars, Action<AppSettings> Preview, Action<AppSettings> Save, string SettingsPath, string DiagnosticsPath);

public sealed class SettingsWindow : Window
{
    static readonly (string Id, string Glyph, string Title)[] Pages =
        [("look", "\uE771", "Wygląd"), ("displays", "\uE7F4", "Ekrany"), ("sensors", "\uE9D9", "Czujniki"), ("colors", "\uE790", "Kolory temperatur")];
    static readonly double[] RefreshPresets = [0.5, 1, 2, 5, 10, 30, 60];
    static readonly CultureInfo Ui = CultureInfo.GetCultureInfo("pl-PL");

    readonly SettingsContext context;
    AppSettings saved, draft;
    readonly ListBox nav = new();
    readonly ScrollViewer scroller = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
    readonly TextBlock statusGlyph = new Decorative(), statusText = new();
    readonly Button saveButton = new(), revertButton = new(), defaultsButton = new();
    readonly Grid dialogLayer = new() { Visibility = Visibility.Collapsed };
    readonly List<Action> live = [];
    readonly DispatcherTimer ticker = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly ColumnDefinition navColumn = new() { Width = new GridLength(264) };
    readonly Grid footerStatus = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 16, 0) };
    WidgetView? preview;
    // Text boxes whose content cannot be stored; the draft keeps their last valid value meanwhile.
    readonly HashSet<DependencyObject> invalidFields = [];
    AppSettings? previewSettings;
    string currentPage = "look";
    bool allowClose;

    public SettingsWindow(AppSettings initial, SettingsContext context, string page)
    {
        this.context = context;
        saved = Clone(initial); draft = Clone(initial);
        AddMissingMonitors(saved); AddMissingMonitors(draft);
        Title = "Ustawienia TempView";
        Width = 980; Height = 720; MinWidth = 660; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"); FontSize = 14;
        Icon = Visuals.AppIcon(); UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

        var root = new Grid();
        root.ColumnDefinitions.Add(navColumn); root.ColumnDefinitions.Add(new ColumnDefinition());
        Content = root;

        // Navigation pane
        nav.SetResourceReference(StyleProperty, "TvNavList");
        nav.Margin = new Thickness(4, 8, 4, 8);
        AutomationProperties.SetName(nav, "Sekcje ustawień");
        foreach (var (id, glyph, title) in Pages)
        {
            var item = new ListBoxItem { Tag = id, Content = NavContent(glyph, title) };
            item.SetResourceReference(StyleProperty, "TvNavItem");
            AutomationProperties.SetName(item, title);
            nav.Items.Add(item);
        }
        nav.SelectionChanged += (_, _) => { if (nav.SelectedItem is ListBoxItem { Tag: string id } && id != currentPage) ShowPage(id); };
        root.Children.Add(nav);

        // Content area with footer, modelled on the NavigationView content layer
        var contentLayer = new Border { CornerRadius = new CornerRadius(8, 0, 0, 0), BorderThickness = new Thickness(1, 1, 0, 0) };
        contentLayer.SetResourceReference(Border.BackgroundProperty, "LayerFillColorDefaultBrush");
        contentLayer.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        Grid.SetColumn(contentLayer, 1); root.Children.Add(contentLayer);
        var contentGrid = new Grid();
        contentGrid.RowDefinitions.Add(new RowDefinition()); contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentLayer.Child = contentGrid;
        contentGrid.Children.Add(scroller);
        var footer = BuildFooter(); Grid.SetRow(footer, 1); contentGrid.Children.Add(footer);

        Grid.SetColumnSpan(dialogLayer, 2); root.Children.Add(dialogLayer);

        SizeChanged += (_, _) => UpdateNavWidth();
        PreviewKeyDown += OnKey;
        ticker.Tick += (_, _) => RunLive(); ticker.Start();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        SystemTheme.Changed += SystemThemeChanged;
        Closing += (_, e) => { if (!allowClose && IsDirty) { e.Cancel = true; ShowCloseDialog(); } };
        Closed += (_, _) =>
        {
            ticker.Stop();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
            SystemTheme.Changed -= SystemThemeChanged;
            context.Preview(Clone(saved));
        };

        var start = Array.FindIndex(Pages, p => p.Id == page);
        currentPage = "";
        nav.SelectedIndex = start < 0 ? 0 : start;
        Changed();
    }

    public void Navigate(string page)
    {
        var index = Array.FindIndex(Pages, p => p.Id == page);
        if (index >= 0) nav.SelectedIndex = index;
    }

    bool IsDirty => Json(draft) != Json(saved);
    // First run: defaults are in use but nothing is on disk yet.
    bool Persisted => File.Exists(context.SettingsPath);

    // ───────────────────────── Pages ─────────────────────────

    void ShowPage(string id)
    {
        currentPage = id; live.Clear(); preview = null; invalidFields.Clear();
        var panel = new StackPanel();
        var title = new TextBlock { Text = Pages.First(p => p.Id == id).Title, Margin = new Thickness(0, 0, 0, 4) };
        title.SetResourceReference(StyleProperty, "TitleTextBlockStyle");
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level1);
        panel.Children.Add(title);
        switch (id)
        {
            case "look": BuildLook(panel); break;
            case "displays": BuildDisplays(panel); break;
            case "sensors": BuildSensors(panel); break;
            default: BuildColors(panel); break;
        }
        var page = new Border { Child = panel, MaxWidth = (double)FindResource("TvContentMaxWidth"), Padding = (Thickness)FindResource("TvPagePadding"), HorizontalAlignment = HorizontalAlignment.Left };
        page.SetBinding(WidthProperty, new System.Windows.Data.Binding(nameof(ActualWidth)) { Source = scroller, Converter = new ClampWidth() });
        scroller.Content = page;
        scroller.ScrollToTop();
        RunLive();
    }

    void BuildLook(StackPanel panel)
    {
        // Live preview over a taskbar-like strip, using the real panel control and real readings.
        var strip = new Border { Height = 64, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), Margin = new Thickness(0, 16, 0, 0), Padding = new Thickness(16, 0, 16, 0) };
        strip.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        preview = new WidgetView { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(preview, "Podgląd panelu");
        strip.Child = preview;
        panel.Children.Add(strip);
        var caption = Secondary("Podgląd pokazuje bieżące odczyty czujników i niezapisane zmiany.");
        caption.Margin = new Thickness(2, 6, 0, 0);
        panel.Children.Add(caption);
        live.Add(() =>
        {
            var light = WidgetTheme.IsLight(draft.Theme, SystemTheme.TaskbarLight);
            strip.Background = new SolidColorBrush(SystemTheme.HighContrast ? SystemColors.ControlColor : light ? Color.FromRgb(0xEE, 0xEE, 0xEE) : Color.FromRgb(0x1C, 0x1C, 0x1C));
            UpdatePreview();
        });

        Header(panel, "Układ");
        panel.Children.Add(Card("\uE8A9", "Układ panelu", "Dwa wiersze mieszczą się w standardowej wysokości paska zadań.",
            Combo([("vertical", "Pionowy, dwa wiersze"), ("horizontal", "Poziomy, jeden wiersz")], draft.Horizontal ? "horizontal" : "vertical", v => draft.Horizontal = v == "horizontal", "Układ panelu")));
        panel.Children.Add(Card("\uE950", "Ikony CPU i GPU", "Małe symbole przed nazwami podzespołów.", Toggle(draft.ShowIcons, v => draft.ShowIcons = v, "Ikony CPU i GPU")));
        var widthRow = Row(null, "Szerokość", "Szerokość panelu w pikselach przy skali 100%.", SliderField(72, 400, 1, draft.Width, v => $"{v:0} px", v => draft.Width = v, "Szerokość panelu"));
        widthRow.Visibility = draft.AutoWidth ? Visibility.Collapsed : Visibility.Visible;
        panel.Children.Add(Group(Row("\uE9A6", "Automatyczna szerokość", "Panel dopasowuje się do zawartości i nie zmienia rozmiaru przy zmianie odczytów.",
            Toggle(draft.AutoWidth, v => { draft.AutoWidth = v; widthRow.Visibility = v ? Visibility.Collapsed : Visibility.Visible; }, "Automatyczna szerokość")), widthRow));
        panel.Children.Add(Card("\uE8E9", "Rozmiar tekstu", "Tekst jest zmniejszany, gdy nie mieści się w wysokości paska.",
            SliderField(8, 24, 1, draft.FontSize, v => $"{v:0} px", v => draft.FontSize = v, "Rozmiar tekstu")));
        panel.Children.Add(Card("\uE740", "Margines wewnętrzny", "Odstęp między krawędzią panelu a jego zawartością.",
            SliderField(0, 16, 1, draft.Padding, v => $"{v:0} px", v => draft.Padding = v, "Margines wewnętrzny")));

        Header(panel, "Kolory");
        panel.Children.Add(Card("\uE793", "Motyw", "Dotyczy panelu i okna ustawień. Systemowy motyw panelu odpowiada kolorowi paska zadań.",
            Combo([("auto", "Systemowy"), ("light", "Jasny"), ("dark", "Ciemny")], draft.Theme, v => draft.Theme = v, "Motyw")));
        var opacityCard = Card("\uE706", "Krycie tła", "", SliderField(0, 1, .05, draft.BackgroundOpacity, v => $"{v * 100:0}%", v => draft.BackgroundOpacity = v, "Krycie tła"));
        panel.Children.Add(opacityCard);
        live.Add(() => SetDescription(opacityCard, SystemTheme.Transparency
            ? "Niższe krycie pozwala zobaczyć pasek zadań pod panelem."
            : "Efekty przezroczystości są wyłączone w systemie, więc tło jest pełne."));

        var colorRows = new List<FrameworkElement>
        {
            Row(null, "Tekst CPU", null, ColorField(draft.CpuColor, v => draft.CpuColor = v, "Kolor tekstu CPU", () => draft.BackgroundColor)),
            Row(null, "Tekst GPU", null, ColorField(draft.GpuColor, v => draft.GpuColor = v, "Kolor tekstu GPU", () => draft.BackgroundColor)),
            Row(null, "Tło", null, ColorField(draft.BackgroundColor, v => draft.BackgroundColor = v, "Kolor tła", null)),
        };
        foreach (var r in colorRows) r.Visibility = draft.CustomColors ? Visibility.Visible : Visibility.Collapsed;
        panel.Children.Add(Group([Row("\uE790", "Własne kolory", "Wyłączone: kolory tekstu i tła wynikają z motywu.",
            Toggle(draft.CustomColors, v => { draft.CustomColors = v; foreach (var r in colorRows) r.Visibility = v ? Visibility.Visible : Visibility.Collapsed; }, "Własne kolory")), .. colorRows]));
        panel.Children.Add(LinkCard(Icons.Thermometer, "Kolory temperatur", "Zmieniaj kolor odczytu w zależności od temperatury.", "colors"));
    }

    void BuildDisplays(StackPanel panel)
    {
        panel.Children.Add(Secondary("Panel jest nakładką nad paskiem zadań i nie rezerwuje na nim miejsca. Chowa się, gdy brakuje wolnego obszaru, pasek jest ukryty lub działa aplikacja pełnoekranowa."));
        Header(panel, "Gdzie pokazywać panel");
        var modes = new StackPanel();
        var monitorToggles = new List<CheckBox>();
        foreach (var (id, label) in new[] { ("primary", "Na ekranie głównym"), ("all", "Na wszystkich ekranach z paskiem zadań"), ("selected", "Na wybranych ekranach") })
        {
            var radio = new RadioButton { Content = label, GroupName = "monitor-mode", IsChecked = draft.MonitorMode == id, Margin = new Thickness(0, 4, 0, 4) };
            radio.Checked += (_, _) => { draft.MonitorMode = id; foreach (var t in monitorToggles) t.IsEnabled = id == "selected"; Changed(); };
            modes.Children.Add(radio);
        }
        panel.Children.Add(Group(Row("\uE7F4", "Wyświetlanie", "Wybierz ekrany, na których pasek zadań pokazuje temperatury.", null), Row(null, null, null, modes, stretch: true)));

        Header(panel, "Ekrany z paskiem zadań");
        foreach (var screen in System.Windows.Forms.Screen.AllScreens.OrderBy(s => s.DeviceName, StringComparer.OrdinalIgnoreCase))
        {
            var pref = draft.Monitors.First(m => m.DeviceName.Equals(screen.DeviceName, StringComparison.OrdinalIgnoreCase));
            var name = DisplayName(screen.DeviceName) + (screen.Primary ? " (główny)" : "");
            var toggle = Toggle(pref.Enabled, v => pref.Enabled = v, "Pokazuj na: " + name);
            toggle.IsEnabled = draft.MonitorMode == "selected";
            monitorToggles.Add(toggle);
            var header = Row("\uE7F4", name, $"{screen.Bounds.Width} × {screen.Bounds.Height}", null);
            panel.Children.Add(Group(header,
                Row(null, "Strona paska", "Panel zajmuje pierwsze wolne miejsce od wybranej krawędzi.", Combo([("left", "Lewa"), ("right", "Prawa")], pref.Side, v => pref.Side = v, "Strona paska: " + name)),
                Row(null, "Pokazuj na tym ekranie", "Dostępne w trybie „Na wybranych ekranach”.", toggle)));
            var device = screen.DeviceName;
            live.Add(() => SetDescription(header, $"{screen.Bounds.Width} × {screen.Bounds.Height}\n" + PlacementText(device)));
        }
    }

    string PlacementText(string device)
    {
        var shown = draft.MonitorMode == "all" || draft.MonitorMode == "primary" && System.Windows.Forms.Screen.AllScreens.Any(s => s.Primary && s.DeviceName == device)
            || draft.MonitorMode == "selected" && draft.Monitors.Any(m => m.Enabled && m.DeviceName == device);
        if (!shown) return "Panel wyłączony na tym ekranie.";
        var bar = context.Bars().FirstOrDefault(b => b.Device == device);
        return bar is null ? "Panel ukryty: na tym ekranie nie wykryto paska zadań." : bar.Status == "OK" ? "Panel widoczny." : "Panel ukryty: " + bar.Status + ".";
    }

    void BuildSensors(StackPanel panel)
    {
        var problems = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        panel.Children.Add(problems);
        string? shownProblems = null;
        live.Add(() =>
        {
            var sample = context.Sample();
            var key = $"{sample.Cpu.Reason}|{sample.Gpu.Reason}|{sample.DriverInstalled}";
            if (key == shownProblems) return;
            shownProblems = key; problems.Children.Clear();
            foreach (var (name, reading) in new[] { ("CPU", sample.Cpu), ("GPU", sample.Gpu) })
                if (!reading.IsAvailable)
                {
                    var driver = name == "CPU" && sample.DriverInstalled == false;
                    problems.Children.Add(InfoBar($"Brak odczytu temperatury {name}", reading.Reason ?? "Czujnik nie zwrócił pomiaru.",
                        driver ? ("Instrukcja instalacji sterownika", OpenDriverGuide) : null));
                }
        });

        Header(panel, "Źródło pomiaru");
        var lists = new List<Action>();
        foreach (var kind in new[] { "CPU", "GPU" })
        {
            var combo = new ComboBox { MinWidth = 260, MaxWidth = 360 };
            AutomationProperties.SetName(combo, "Czujnik " + kind);
            void Fill()
            {
                var current = kind == "CPU" ? draft.CpuSensorId : draft.GpuSensorId;
                var choices = new List<Choice> { new("", "Automatycznie (zalecane)") };
                choices.AddRange(context.Sample().Sensors.Where(s => s.Kind == kind).Select(s => new Choice(s.Id, s.Name)));
                if (!string.IsNullOrEmpty(current) && choices.All(c => c.Id != current)) choices.Add(new(current, "Niedostępny: " + current));
                combo.ItemsSource = choices; combo.DisplayMemberPath = nameof(Choice.Label); combo.SelectedValuePath = nameof(Choice.Id);
                combo.SelectedValue = current ?? "";
            }
            Fill(); lists.Add(Fill);
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedValue is not string id) return;
                var value = id.Length == 0 ? null : id;
                if (kind == "CPU") { if (draft.CpuSensorId == value) return; draft.CpuSensorId = value; } else { if (draft.GpuSensorId == value) return; draft.GpuSensorId = value; }
                Changed();
            };
            var card = Card(null, kind == "CPU" ? "Procesor (CPU)" : "Karta graficzna (GPU)", "", combo);
            SetIcon((Grid)card.Child, Icons.Create(kind == "CPU" ? Icons.Cpu : Icons.Gpu, 20, Brushes.Black, 1.4));
            panel.Children.Add(card);
            live.Add(() =>
            {
                var r = kind == "CPU" ? context.Sample().Cpu : context.Sample().Gpu;
                SetDescription(card, r.IsAvailable ? $"Bieżący odczyt: {r.DisplayText}" + (r.Source is null ? "" : $"\nCzujnik: {r.Source}") : "Bieżący odczyt: N/A");
            });
        }
        var refresh = SubtleButton("\uE72C", "Odśwież listę czujników", () => { foreach (var fill in lists) fill(); });
        refresh.HorizontalAlignment = HorizontalAlignment.Left; refresh.Margin = new Thickness(0, 4, 0, 0);
        panel.Children.Add(refresh);

        Header(panel, "Odczyt");
        var presets = RefreshPresets.ToList();
        if (!presets.Contains(draft.RefreshSeconds)) { presets.Add(draft.RefreshSeconds); presets.Sort(); }
        panel.Children.Add(Card("\uE916", "Częstotliwość odczytu", "Częstszy odczyt szybciej pokazuje zmiany, ale nieco bardziej obciąża procesor.",
            Combo(presets.Select(p => (p.ToString(CultureInfo.InvariantCulture), p < 1 ? $"Co {p.ToString(Ui)} s" : p == 1 ? "Co sekundę" : $"Co {p.ToString(Ui)} s")), draft.RefreshSeconds.ToString(CultureInfo.InvariantCulture),
                v => draft.RefreshSeconds = double.Parse(v, CultureInfo.InvariantCulture), "Częstotliwość odczytu")));

        Header(panel, "Diagnostyka");
        var driverCard = Card("\uE7EF", "Sterownik PawnIO", "", SubtleButton("\uE8A7", "Instrukcja", OpenDriverGuide));
        panel.Children.Add(driverCard);
        live.Add(() => SetDescription(driverCard, context.Sample().DriverInstalled switch
        {
            true => "Zainstalowany. Część czujników CPU wymaga również uruchomienia TempView jako administrator.",
            false => "Nie zainstalowano. Bez niego większość czujników CPU jest niedostępna. Sterownik instaluje się osobno.",
            _ => "Sprawdzanie…",
        }));
        var elevated = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        panel.Children.Add(Card("\uE83D", "Uprawnienia", elevated ? "TempView działa z uprawnieniami administratora." : "TempView działa bez uprawnień administratora. To bezpieczny tryb; część czujników CPU może pokazywać N/A.", null));
        var folder = Path.GetDirectoryName(context.DiagnosticsPath) ?? "";
        panel.Children.Add(Card("\uE8B7", "Pliki ustawień i diagnostyki", context.SettingsPath + "\n" + context.DiagnosticsPath,
            SubtleButton("\uE838", "Otwórz folder", () => Open(folder))));
    }

    void BuildColors(StackPanel panel)
    {
        panel.Children.Add(Secondary("Kolor odczytu zmienia się płynnie między punktami. Poniżej pierwszego i powyżej ostatniego punktu używany jest kolor skrajny. Nazwa podzespołu zachowuje kolor tekstu."));
        foreach (var kind in new[] { "CPU", "GPU" })
        {
            Header(panel, kind == "CPU" ? "Procesor (CPU)" : "Karta graficzna (GPU)");
            var gradient = kind == "CPU" ? draft.CpuGradient : draft.GpuGradient;
            var bar = new GradientBar { Height = 52, Margin = new Thickness(0, 4, 0, 0) };
            AutomationProperties.SetName(bar, "Skala kolorów " + kind);
            var points = new StackPanel();
            var add = SubtleButton("\uE710", "Dodaj punkt", () => { }); add.HorizontalAlignment = HorizontalAlignment.Left;
            var details = new List<FrameworkElement> { Row(null, null, null, bar, stretch: true), Row(null, null, null, points, stretch: true), Row(null, null, null, add, stretch: true) };
            void Sync() { bar.Points = gradient.Points.Select(p => (p.Temperature, p.Color)).ToList(); bar.InvalidateVisual(); }
            var shown = new List<ColorStop>();
            void Rebuild()
            {
                invalidFields.RemoveWhere(points.IsAncestorOf);
                points.Children.Clear();
                shown = gradient.Points.OrderBy(p => p.Temperature).ToList();
                foreach (var point in shown) points.Children.Add(PointRow(gradient, point, Rebuild, RebuildIfReordered, Sync, kind));
                add.IsEnabled = gradient.Points.Count < 8;
                Sync();
            }
            void RebuildIfReordered()
            {
                if (!gradient.Points.OrderBy(p => p.Temperature).SequenceEqual(shown)) Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Rebuild));
            }
            add.Click += (_, _) =>
            {
                var t = gradient.Points.Count == 0 ? 50 : Math.Min(200, Math.Round(gradient.Points.Max(p => p.Temperature)) + 10);
                while (gradient.Points.Any(p => p.Temperature == t) && t > -50) t--;
                gradient.Points.Add(new ColorStop { Temperature = t, Color = gradient.Points.LastOrDefault()?.Color ?? "#FF6B6B" });
                Rebuild(); Changed();
            };
            Rebuild();
            foreach (var d in details) d.Visibility = gradient.Enabled ? Visibility.Visible : Visibility.Collapsed;
            var header = Row(null, "Kolor zależny od temperatury", "Wyłączone: odczyt ma kolor tekstu panelu.",
                Toggle(gradient.Enabled, v => { gradient.Enabled = v; foreach (var d in details) d.Visibility = v ? Visibility.Visible : Visibility.Collapsed; }, "Kolor zależny od temperatury " + kind));
            SetIcon(header, Icons.Create(kind == "CPU" ? Icons.Cpu : Icons.Gpu, 20, Brushes.Black, 1.4));
            panel.Children.Add(Group([header, .. details]));
            live.Add(() => { var r = kind == "CPU" ? context.Sample().Cpu : context.Sample().Gpu; bar.Current = r.IsAvailable ? r.Value : null; bar.InvalidateVisual(); });
        }
    }

    FrameworkElement PointRow(GradientSettings gradient, ColorStop point, Action rebuild, Action rebuildIfReordered, Action sync, string kind)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        foreach (var w in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        var temp = new TextBox { Text = point.Temperature.ToString("0.##", Ui), Width = 76, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(temp, $"Temperatura punktu {kind}");
        var unit = Secondary("°C"); unit.VerticalAlignment = VerticalAlignment.Center; unit.Margin = new Thickness(6, 0, 16, 0);
        var error = Secondary(""); error.Visibility = Visibility.Collapsed; error.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        temp.TextChanged += (_, _) =>
        {
            var ok = TryParseNumber(temp.Text, out var v) && v is >= -50 and <= 200;
            var duplicate = ok && gradient.Points.Any(p => p != point && p.Temperature == v);
            error.Text = !ok ? "Wpisz liczbę od −50 do 200." : duplicate ? "Ta temperatura jest już używana." : "";
            error.Visibility = error.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            MarkInvalid(temp, error.Text.Length > 0);
            // Unparsable text never enters the settings: the draft keeps the last valid value and Save stays blocked.
            if (ok) { point.Temperature = v; invalidFields.Remove(temp); } else invalidFields.Add(temp);
            sync(); Changed();
        };
        // Re-sort only when the order really changed; rebuilding on every focus loss would swallow clicks on other rows.
        temp.LostKeyboardFocus += (_, _) => { if (error.Text.Length == 0) rebuildIfReordered(); };
        var color = ColorField(point.Color, v => { point.Color = v; sync(); }, $"Kolor punktu {kind}", () => WidgetTheme.OpaqueSurface(Palette(draft)));
        var remove = SubtleButton("\uE74D", null, () => { gradient.Points.Remove(point); rebuild(); Changed(); });
        remove.IsEnabled = gradient.Points.Count > 1;
        remove.ToolTip = gradient.Points.Count > 1 ? "Usuń punkt" : "Skala wymaga co najmniej jednego punktu";
        AutomationProperties.SetName(remove, "Usuń punkt");
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(temp); left.Children.Add(unit);
        grid.Children.Add(left);
        Grid.SetColumn(color, 1); grid.Children.Add(color);
        Grid.SetColumn(remove, 3); grid.Children.Add(remove);
        var stack = new StackPanel(); stack.Children.Add(grid); stack.Children.Add(error);
        return stack;
    }

    // ───────────────────────── State ─────────────────────────

    void Changed()
    {
        var problems = SettingsValidator.Validate(draft).ToList();
        if (invalidFields.Count > 0) problems.Insert(0, "popraw pola zaznaczone na czerwono.");
        if (problems.Count == 0)
        {
            // One instance per change: the preview control caches by reference and keeps its animations between ticks.
            previewSettings = Clone(draft);
            context.Preview(Clone(draft));
        }
        UpdatePreview();
        var dirty = IsDirty;
        string glyph, text, brush;
        if (problems.Count > 0) { glyph = "\uEA39"; brush = "SystemFillColorCriticalBrush"; text = "Nie można zapisać: " + problems[0] + (problems.Count > 1 ? $" (i {problems.Count - 1} więcej)" : ""); }
        else if (dirty) { glyph = "\uE7BA"; brush = "SystemFillColorCautionBrush"; text = "Niezapisane zmiany. Panel pokazuje ich podgląd."; }
        else if (!Persisted) { glyph = "\uE946"; brush = "AccentTextFillColorPrimaryBrush"; text = "Ustawienia domyślne nie są jeszcze zapisane."; }
        else { glyph = "\uE73E"; brush = "SystemFillColorSuccessBrush"; text = "Wszystkie zmiany zapisane."; }
        statusGlyph.Text = glyph; statusGlyph.SetResourceReference(TextBlock.ForegroundProperty, brush);
        statusText.Text = text;
        AutomationProperties.SetName(statusText, text);
        saveButton.IsEnabled = (dirty || !Persisted) && problems.Count == 0;
        revertButton.IsEnabled = dirty;
    }

    void UpdatePreview()
    {
        if (preview is null || previewSettings is not { } s) return;
        preview.Apply(s, Palette(s), SystemTheme.HighContrast, SystemTheme.Animations, 40, s.AutoWidth ? null : s.Width);
        var sample = context.Sample();
        var maxAge = TimeSpan.FromSeconds(Math.Max(5, s.RefreshSeconds * 3));
        preview.SetReadings(sample.Cpu.Expire(maxAge, DateTimeOffset.UtcNow), sample.Gpu.Expire(maxAge, DateTimeOffset.UtcNow), s);
    }

    static WidgetPalette Palette(AppSettings s) => WidgetTheme.Resolve(s, SystemTheme.TaskbarLight, SystemTheme.Transparency);

    void Save()
    {
        if (!saveButton.IsEnabled) return;
        try { context.Save(Clone(draft)); saved = Clone(draft); Changed(); statusText.Text = "Zapisano. Ustawienia zostaną użyte także po ponownym uruchomieniu."; }
        catch (Exception ex) { statusGlyph.Text = "\uEA39"; statusGlyph.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush"); statusText.Text = "Nie udało się zapisać ustawień: " + ex.Message; }
    }

    void Revert() { draft = Clone(saved); ShowPage(currentPage); Changed(); }

    void RestoreDefaults()
    {
        draft = new AppSettings(); AddMissingMonitors(draft);
        ShowPage(currentPage); Changed();
        statusText.Text = "Przywrócono ustawienia domyślne. Zapisz, aby je zachować, lub odrzuć zmiany.";
    }

    void RunLive() { foreach (var action in live.ToArray()) action(); }

    void SystemThemeChanged() => Dispatcher.BeginInvoke(new Action(() => { ShowPage(currentPage); Changed(); }));

    void DisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(() =>
    {
        AddMissingMonitors(saved); AddMissingMonitors(draft);
        if (currentPage == "displays") ShowPage(currentPage);
        Changed();
    }));

    static void AddMissingMonitors(AppSettings settings)
    {
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            if (!settings.Monitors.Any(m => m.DeviceName.Equals(screen.DeviceName, StringComparison.OrdinalIgnoreCase)))
                settings.Monitors.Add(new MonitorPreference { DeviceName = screen.DeviceName });
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        if (dialogLayer.Visibility == Visibility.Visible) return;
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { Save(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
    }

    void ShowCloseDialog()
    {
        var card = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), MaxWidth = 440, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        card.SetResourceReference(Border.BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "SurfaceStrokeColorDefaultBrush");
        if (!SystemTheme.HighContrast) card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 32, ShadowDepth = 8, Direction = 270, Opacity = .28, Color = Colors.Black };
        var body = new StackPanel { Margin = new Thickness(24, 24, 24, 24) };
        var heading = new TextBlock { Text = "Zapisać zmiany?" }; heading.SetResourceReference(StyleProperty, "SubtitleTextBlockStyle");
        body.Children.Add(heading);
        var canSave = saveButton.IsEnabled;
        body.Children.Add(new TextBlock { Text = canSave
            ? "Zmiany w ustawieniach TempView nie zostały zapisane. Bez zapisu panel wróci do poprzedniego wyglądu."
            : "Niektóre pola mają błędne wartości, więc zmian nie można zapisać. Wróć, aby je poprawić, albo zamknij bez zapisywania.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
        var buttons = new UniformGrid { Columns = 3, Margin = new Thickness(24, 0, 24, 24) };
        void Finish(bool save, bool close)
        {
            dialogLayer.Visibility = Visibility.Collapsed; dialogLayer.Children.Clear();
            if (save) { Save(); if (IsDirty) return; }
            if (close) { allowClose = true; Close(); }
        }
        var yes = MakeButton("Zapisz", () => Finish(true, true), accent: true); yes.IsDefault = canSave; yes.IsEnabled = canSave;
        var no = MakeButton("Nie zapisuj", () => Finish(false, true));
        var cancel = MakeButton("Anuluj", () => Finish(false, false)); cancel.IsCancel = true;
        foreach (var b in new[] { yes, no, cancel }) { b.Margin = new Thickness(4, 0, 4, 0); b.HorizontalAlignment = HorizontalAlignment.Stretch; buttons.Children.Add(b); }
        var layout = new StackPanel(); layout.Children.Add(body); layout.Children.Add(buttons);
        card.Child = layout;
        KeyboardNavigation.SetTabNavigation(card, KeyboardNavigationMode.Cycle);
        AutomationProperties.SetName(card, "Zapisać zmiany?");
        dialogLayer.Children.Clear();
        dialogLayer.SetResourceReference(Panel.BackgroundProperty, "SmokeFillColorDefaultBrush");
        dialogLayer.Children.Add(card);
        dialogLayer.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => (canSave ? yes : cancel).Focus()));
    }

    // ───────────────────────── Building blocks ─────────────────────────

    FrameworkElement BuildFooter()
    {
        var footer = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(24, 12, 24, 12) };
        footer.SetResourceReference(Border.BorderBrushProperty, "DividerStrokeColorDefaultBrush");
        var grid = new Grid();
        foreach (var w in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        Configure(defaultsButton, "Przywróć domyślne", RestoreDefaults);
        defaultsButton.ToolTip = "Ustawia wartości początkowe. Zmiana nie jest zapisywana, dopóki nie klikniesz Zapisz.";
        grid.Children.Add(defaultsButton);
        var status = footerStatus;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        status.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); status.ColumnDefinitions.Add(new ColumnDefinition());
        statusGlyph.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"); statusGlyph.FontSize = 16; statusGlyph.VerticalAlignment = VerticalAlignment.Center; statusGlyph.Margin = new Thickness(0, 0, 8, 0);
        statusText.TextWrapping = TextWrapping.Wrap; statusText.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetLiveSetting(statusText, AutomationLiveSetting.Polite);
        status.Children.Add(statusGlyph); Grid.SetColumn(statusText, 1); status.Children.Add(statusText);
        Grid.SetColumn(status, 1); grid.Children.Add(status);
        Configure(revertButton, "Odrzuć zmiany", Revert); revertButton.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(revertButton, 2); grid.Children.Add(revertButton);
        Configure(saveButton, "Zapisz", Save, accent: true); saveButton.MinWidth = 120;
        saveButton.ToolTip = "Zapisz (Ctrl+S)";
        Grid.SetColumn(saveButton, 3); grid.Children.Add(saveButton);
        footer.Child = grid;
        return footer;
    }

    static FrameworkElement NavContent(string glyph, string title)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = new Decorative { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        row.Children.Add(icon);
        row.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, Tag = "label" });
        return row;
    }

    void UpdateNavWidth()
    {
        // Compact navigation (icons only) below 760 px, like NavigationView.
        var compact = ActualWidth < 760;
        navColumn.Width = new GridLength(compact ? 56 : 264);
        // Narrow window: the save state gets its own line below the buttons instead of wrapping into a column.
        Grid.SetRow(footerStatus, compact ? 1 : 0); Grid.SetColumn(footerStatus, compact ? 0 : 1); Grid.SetColumnSpan(footerStatus, compact ? 4 : 1);
        footerStatus.Margin = compact ? new Thickness(0, 10, 0, 0) : new Thickness(16, 0, 16, 0);
        foreach (ListBoxItem item in nav.Items)
            if (item.Content is StackPanel row)
            {
                row.Children[1].Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
                item.ToolTip = compact ? ((TextBlock)row.Children[1]).Text : null;
            }
    }

    static void Header(StackPanel panel, string text)
    {
        var header = new TextBlock { Text = text, Margin = new Thickness(1, 28, 0, 8) };
        header.SetResourceReference(StyleProperty, "BodyStrongTextBlockStyle");
        AutomationProperties.SetHeadingLevel(header, AutomationHeadingLevel.Level2);
        panel.Children.Add(header);
    }

    static TextBlock Secondary(string text)
    {
        var block = new TextBlock { Text = text };
        block.SetResourceReference(StyleProperty, "TvSecondaryText");
        return block;
    }

    /// <summary>A setting row: icon, title, description and control. Used alone (card) or inside a group.</summary>
    static Grid Row(string? glyph, string? title, string? description, FrameworkElement? control, bool stretch = false)
    {
        var grid = new Grid { MinHeight = title is null ? 0 : 40 };
        foreach (var w in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        var icon = new Decorative { Text = glyph ?? "", Tag = "icon" };
        icon.SetResourceReference(StyleProperty, "TvCardIcon");
        if (glyph is null) icon.Width = title is null ? 0 : 20;
        grid.Children.Add(icon);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        if (title is not null) text.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap });
        var desc = Secondary(description ?? ""); desc.Tag = "description";
        desc.Visibility = string.IsNullOrEmpty(description) ? Visibility.Collapsed : Visibility.Visible;
        text.Children.Add(desc);
        Grid.SetColumn(text, 1); grid.Children.Add(text);
        if (control is not null)
        {
            control.VerticalAlignment = VerticalAlignment.Center;
            if (stretch) { Grid.SetColumn(control, 0); Grid.SetColumnSpan(control, 3); text.Visibility = Visibility.Collapsed; icon.Visibility = Visibility.Collapsed; }
            else Grid.SetColumn(control, 2);
            grid.Children.Add(control);
            if (title is not null && control is not Button) AutomationProperties.SetLabeledBy(control, (TextBlock)text.Children[0]);
        }
        return grid;
    }

    static Border Card(string? glyph, string title, string? description, FrameworkElement? control)
    {
        var card = new Border { Child = Row(glyph, title, description, control) };
        card.SetResourceReference(StyleProperty, "TvCard");
        return card;
    }

    /// <summary>Rows that depend on each other share one card, separated by dividers.</summary>
    static Border Group(params FrameworkElement[] rows)
    {
        var stack = new StackPanel();
        for (int i = 0; i < rows.Length; i++)
        {
            var host = new Border { Child = rows[i], Padding = i == 0 ? new Thickness(16, 12, 16, 12) : new Thickness(56, 10, 16, 10), BorderThickness = new Thickness(0, i == 0 ? 0 : 1, 0, 0) };
            host.SetResourceReference(Border.BorderBrushProperty, "DividerStrokeColorDefaultBrush");
            // A binding (not DependencyPropertyDescriptor.AddValueChanged, which roots the window) keeps the divider row in sync.
            host.SetBinding(VisibilityProperty, new System.Windows.Data.Binding(nameof(Visibility)) { Source = rows[i] });
            stack.Children.Add(host);
        }
        var card = new Border { Child = stack, Padding = new Thickness(0) };
        card.SetResourceReference(StyleProperty, "TvCard");
        card.Padding = new Thickness(0);
        return card;
    }

    Button LinkCard(Geometry icon, string title, string description, string page)
    {
        var chevron = new Decorative { Text = "\uE76C", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12 };
        var row = Row(null, title, description, chevron);
        SetIcon(row, Icons.Create(icon, 20, Brushes.Black, 1.4));
        var button = new Button { Content = row };
        button.SetResourceReference(StyleProperty, "TvLinkCard");
        AutomationProperties.SetName(button, title);
        AutomationProperties.SetHelpText(button, description);
        button.Click += (_, _) => Navigate(page);
        return button;
    }

    static void SetDescription(FrameworkElement cardOrRow, string text)
    {
        var desc = Find(cardOrRow, "description");
        if (desc is null || desc.Text == text) return;
        desc.Text = text; desc.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    static void SetIcon(Grid grid, FrameworkElement icon)
    {
        var old = Find(grid, "icon")!;
        grid.Children.Remove(old);
        icon.Margin = new Thickness(2, 0, 18, 0); icon.VerticalAlignment = VerticalAlignment.Center;
        if (icon is System.Windows.Shapes.Path path) path.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "TextFillColorPrimaryBrush");
        grid.Children.Add(icon);
    }

    static TextBlock? Find(DependencyObject root, string tag)
    {
        if (root is TextBlock { Tag: string t } block && t == tag) return block;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            if (Find(child, tag) is { } found) return found;
        return null;
    }

    CheckBox Toggle(bool value, Action<bool> setter, string name)
    {
        var toggle = new CheckBox { IsChecked = value };
        toggle.SetResourceReference(StyleProperty, "TvToggleSwitch");
        AutomationProperties.SetName(toggle, name);
        toggle.Checked += (_, _) => { setter(true); Changed(); };
        toggle.Unchecked += (_, _) => { setter(false); Changed(); };
        return toggle;
    }

    ComboBox Combo(IEnumerable<(string Id, string Label)> options, string value, Action<string> setter, string name)
    {
        var combo = new ComboBox { ItemsSource = options.Select(x => new Choice(x.Id, x.Label)).ToList(), DisplayMemberPath = nameof(Choice.Label), SelectedValuePath = nameof(Choice.Id), SelectedValue = value, MinWidth = 200 };
        AutomationProperties.SetName(combo, name);
        combo.SelectionChanged += (_, _) => { if (combo.SelectedValue is string selected) { setter(selected); Changed(); } };
        return combo;
    }

    FrameworkElement SliderField(double min, double max, double step, double value, Func<double, string> format, Action<double> setter, string name)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var label = new Decorative { Text = format(value), MinWidth = 52, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        Typography.SetNumeralAlignment(label, FontNumeralAlignment.Tabular);
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, IsSnapToTickEnabled = true, SmallChange = step, LargeChange = step * 4, Width = 200, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(slider, name);
        AutomationProperties.SetItemStatus(slider, format(value));
        slider.ValueChanged += (_, _) => { setter(slider.Value); label.Text = format(slider.Value); AutomationProperties.SetItemStatus(slider, label.Text); Changed(); };
        row.Children.Add(label); row.Children.Add(slider);
        return row;
    }

    /// <summary>Swatch (system color dialog) plus #RRGGBB field with inline validation and an optional contrast warning.</summary>
    FrameworkElement ColorField(string value, Action<string> setter, string name, Func<string>? contrastAgainst)
    {
        var outer = new StackPanel();
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var warning = new TextBlock { Text = "\uE7BA", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), Visibility = Visibility.Collapsed };
        warning.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCautionBrush");
        var swatch = new Button { Margin = new Thickness(0, 0, 8, 0), ToolTip = "Wybierz kolor" };
        swatch.SetResourceReference(StyleProperty, "TvSwatchButton");
        AutomationProperties.SetName(swatch, name + ": wybierz kolor");
        var field = new TextBox { Text = value, Width = 96, MaxLength = 7, VerticalAlignment = VerticalAlignment.Center, CharacterCasing = CharacterCasing.Upper };
        AutomationProperties.SetName(field, name + " w formacie #RRGGBB");
        var error = Secondary("Wpisz kolor w formacie #RRGGBB, np. #FF6B6B."); error.Visibility = Visibility.Collapsed; error.TextAlignment = TextAlignment.Right; error.Margin = new Thickness(0, 4, 0, 0);
        error.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        void Refresh()
        {
            var text = field.Text.Trim(); var ok = SettingsValidator.IsColor(text);
            error.Visibility = ok ? Visibility.Collapsed : Visibility.Visible; MarkInvalid(field, !ok);
            swatch.Background = ok ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(text)) : Brushes.Transparent;
            double ratio = ok && contrastAgainst is not null && SettingsValidator.IsColor(contrastAgainst()) ? ColorContrast.Ratio(text, contrastAgainst()) : 21;
            warning.Visibility = ratio < 3 ? Visibility.Visible : Visibility.Collapsed;
            warning.ToolTip = $"Niski kontrast z tłem panelu ({ratio.ToString("0.0", Ui)}:1). Odczyt może być słabo czytelny.";
            AutomationProperties.SetHelpText(field, ratio < 3 ? (string)warning.ToolTip : "");
        }
        field.TextChanged += (_, _) =>
        {
            // Only valid colors reach the settings, so palette and contrast calculations never see partial input.
            var text = field.Text.Trim();
            if (SettingsValidator.IsColor(text)) { setter(text); invalidFields.Remove(field); } else invalidFields.Add(field);
            Refresh(); Changed();
        };
        swatch.Click += (_, _) =>
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true };
            if (SettingsValidator.IsColor(field.Text.Trim())) { var c = (Color)ColorConverter.ConvertFromString(field.Text.Trim()); dialog.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B); }
            var owner = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (dialog.ShowDialog(new Win32Owner(owner)) == System.Windows.Forms.DialogResult.OK) field.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        };
        live.Add(Refresh);
        outer.Unloaded += (_, _) => live.Remove(Refresh);
        Refresh();
        row.Children.Add(warning); row.Children.Add(swatch); row.Children.Add(field);
        outer.Children.Add(row); outer.Children.Add(error);
        return outer;
    }

    static void MarkInvalid(TextBox box, bool invalid)
    {
        if (invalid) { box.SetResourceReference(BorderBrushProperty, "SystemFillColorCriticalBrush"); box.BorderThickness = new Thickness(1, 1, 1, 2); }
        else { box.ClearValue(BorderBrushProperty); box.ClearValue(BorderThicknessProperty); }
    }

    Border InfoBar(string title, string message, (string Label, Action Click)? action)
    {
        var bar = new Border();
        bar.SetResourceReference(StyleProperty, "TvInfoBar");
        bar.SetResourceReference(Border.BackgroundProperty, "SystemFillColorCautionBackgroundBrush");
        var grid = new Grid();
        foreach (var w in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        var icon = new TextBlock { Text = "\uE814", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16, Margin = new Thickness(0, 2, 12, 0), VerticalAlignment = VerticalAlignment.Top };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCautionBrush");
        grid.Children.Add(icon);
        var text = new StackPanel();
        var heading = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap }; heading.SetResourceReference(StyleProperty, "BodyStrongTextBlockStyle");
        text.Children.Add(heading);
        text.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(text, 1); grid.Children.Add(text);
        if (action is { } a)
        {
            var button = MakeButton(a.Label, a.Click); button.Margin = new Thickness(16, 0, 0, 0); button.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(button, 2); grid.Children.Add(button);
        }
        bar.Child = grid;
        AutomationProperties.SetName(bar, title + ". " + message);
        AutomationProperties.SetLiveSetting(bar, AutomationLiveSetting.Polite);
        return bar;
    }

    static Button MakeButton(string text, Action click, bool accent = false) { var b = new Button(); Configure(b, text, click, accent); return b; }

    static void Configure(Button button, string text, Action click, bool accent = false)
    {
        button.Content = text; button.Padding = new Thickness(16, 6, 16, 6); button.MinWidth = 96;
        if (accent) button.SetResourceReference(StyleProperty, "AccentButtonStyle");
        button.Click += (_, _) => click();
    }

    static Button SubtleButton(string glyph, string? text, Action click)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = new Decorative { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(icon);
        if (text is not null) content.Children.Add(new TextBlock { Text = text, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = content };
        button.SetResourceReference(StyleProperty, "TvSubtleButton");
        if (text is not null) AutomationProperties.SetName(button, text);
        button.Click += (_, _) => click();
        return button;
    }

    static string DisplayName(string device)
    {
        var digits = new string(device.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return digits.Length > 0 ? "Ekran " + digits : device;
    }

    static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text.Trim().Replace('−', '-').Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    static void OpenDriverGuide()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "DRIVER.md");
        Open(File.Exists(local) ? local : "https://github.com/AjGGoR/TempView/blob/main/docs/DRIVER.md");
    }

    static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("Nie można otworzyć: " + target + "\n" + ex.Message, "TempView", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    static string Json(AppSettings s) => JsonSerializer.Serialize(s);
    static AppSettings Clone(AppSettings settings) => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
    // ToString is what UI Automation announces for combo box items.
    sealed record Choice(string Id, string Label) { public override string ToString() => Label; }
    sealed class Win32Owner(nint handle) : System.Windows.Forms.IWin32Window { public nint Handle { get; } = handle; }
    sealed class ClampWidth : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is double w ? Math.Max(0, w - 1) : DependencyProperty.UnsetValue;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }
}

/// <summary>Color scale with point markers and the current reading.</summary>
public sealed class GradientBar : FrameworkElement
{
    public IReadOnlyList<(double Temperature, string Color)> Points { get; set; } = [];
    public double? Current { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        var valid = Points.Where(p => double.IsFinite(p.Temperature) && SettingsValidator.IsColor(p.Color)).OrderBy(p => p.Temperature).ToList();
        if (valid.Count == 0 || ActualWidth < 40) return;
        var text = (Brush?)TryFindResource("TextFillColorSecondaryBrush") ?? Brushes.Gray;
        var primary = (Brush?)TryFindResource("TextFillColorPrimaryBrush") ?? Brushes.Black;
        var stroke = (Brush?)TryFindResource("ControlStrokeColorDefaultBrush") ?? Brushes.Gray;
        double lo = valid[0].Temperature - 10, hi = valid[^1].Temperature + 10;
        if (Current is double c) { lo = Math.Min(lo, c - 5); hi = Math.Max(hi, c + 5); }
        double X(double t) => 6 + (t - lo) / (hi - lo) * (ActualWidth - 12);
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        foreach (var p in valid) brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(p.Color), (X(p.Temperature) - 0) / ActualWidth));
        var barRect = new Rect(0, 14, ActualWidth, 12);
        dc.DrawRoundedRectangle(brush, new Pen(stroke, 1), barRect, 6, 6);
        var typeface = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (var p in valid)
        {
            var x = X(p.Temperature);
            dc.DrawLine(new Pen(text, 1), new Point(x, 27), new Point(x, 31));
            var label = new FormattedText($"{p.Temperature:0.#}°", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, text, dpi);
            dc.DrawText(label, new Point(Math.Clamp(x - label.Width / 2, 0, ActualWidth - label.Width), 33));
        }
        if (Current is double now)
        {
            var x = X(now);
            dc.DrawRoundedRectangle(primary, new Pen(Brushes.Transparent, 0), new Rect(x - 1.5, 10, 3, 20), 1.5, 1.5);
            var label = new FormattedText($"{now:0}°C teraz", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(typeface.FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 11, primary, dpi);
            dc.DrawText(label, new Point(Math.Clamp(x - label.Width / 2, 0, ActualWidth - label.Width), -4));
        }
    }
}

/// <summary>Icon glyphs and duplicated values: visible, but hidden from screen readers.</summary>
public sealed class Decorative : TextBlock
{
    protected override System.Windows.Automation.Peers.AutomationPeer? OnCreateAutomationPeer() => null;
}
