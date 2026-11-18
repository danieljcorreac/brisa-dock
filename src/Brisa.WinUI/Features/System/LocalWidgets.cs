using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Windows.Foundation;
using Path = System.IO.Path;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    readonly Countdown countdown = new();
    readonly Stopwatch chronometer = new();
    readonly List<TimeSpan> laps = [];
    readonly LocalMetrics localMetrics = new();
    TextBlock? timerCounter, stopwatchCounter;
    TextBlock? timerStatus;
    QuotaMeter? timerProgress;
    Button? timerToggle;
    bool timerFinished;
    DiskReading? storageReading;
    string? storageSelection;
    long storageStamp;
    DiskReading? ReadStorage()
    {
        if (storageStamp == 0 || storageSelection != settings.StorageDrive || Stopwatch.GetElapsedTime(storageStamp) >= TimeSpan.FromSeconds(15))
        {
            storageSelection = settings.StorageDrive; storageReading = LocalMetrics.Drive(storageSelection); storageStamp = Stopwatch.GetTimestamp();
        }
        return storageReading;
    }
    static string DurationText(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
    FrameworkElement? BuildLocalWidget(WidgetDefinition d)
    {
        Action? action = d.Id switch {
            "timer" => ShowTimer, "stopwatch" => ShowStopwatch, "day-progress" or "month-progress" or "year-progress" => () => ShowProgress(d.Id),
            "date" or "day-number" => ShowCalendar, "world-time" => ShowWorldTime, "storage" => ShowStorage, "network" => ShowNetwork,
            "search" => ShowSearch, "recycle-bin" => ShowRecycleBin, "keyboard" => () => { try { DesktopUtilities.Keyboard(); } catch { ShowMessage("Teclado", "Windows no ha permitido abrir el teclado en pantalla."); } }, "cpu" or "ram" => ShowSystem, _ => null };
        if (action == null) return null;
        var stack = Ui.Stack(2); stack.Width = d.Width - 12; stack.HorizontalAlignment = HorizontalAlignment.Center; var face = new Grid { Height = 34 }; stack.Children.Add(face);
        var value = Ui.Text("", 18, bold: true); value.TextWrapping = TextWrapping.NoWrap; value.TextAlignment = TextAlignment.Center; value.HorizontalAlignment = HorizontalAlignment.Center;
        var caption = Ui.Text(d.Name.ToUpperInvariant(), 10, Ui.MutedColor); caption.TextWrapping = TextWrapping.NoWrap; caption.Height = 14; caption.TextAlignment = TextAlignment.Center; caption.HorizontalAlignment = HorizontalAlignment.Center; caption.MaxWidth = d.Width - 12; caption.TextTrimming = TextTrimming.CharacterEllipsis;
        QuotaMeter? progress = null; Polyline? graph = null; ProgressRing? ring = null;
        if (d.Id is "search" or "keyboard" or "recycle-bin") face.Children.Add(Ui.Glyph(d.Glyph, 23));
        else if (d.Id is "cpu" or "ram")
        {
            var grid = new Grid { Width = 34, Height = 34, HorizontalAlignment = HorizontalAlignment.Center }; ring = new ProgressRing { Width = 34, Height = 34, IsIndeterminate = false, Value = 0, Foreground = Ui.Brush(Ui.Light ? "#467C6C" : "#B8E5D4") }; grid.Children.Add(ring); value.FontSize = 11; value.HorizontalAlignment = HorizontalAlignment.Center; grid.Children.Add(value); face.Children.Add(grid);
        }
        else face.Children.Add(value);
        stack.Children.Add(caption);
        if (d.Id.EndsWith("-progress") || d.Id == "storage") { progress = new QuotaMeter(0, 3, Ui.Light ? "#467C6C" : "#B8E5D4") { Width = d.Width - 24, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center }; face.Children.Add(progress); }
        if (d.Id == "network") { graph = new Polyline { Width = 95, Height = 8, Stroke = Ui.Brush(Ui.Light ? "#467C6C" : "#B8E5D4"), StrokeThickness = 1.3 }; graph.VerticalAlignment = VerticalAlignment.Bottom; graph.HorizontalAlignment = HorizontalAlignment.Center; face.Children.Add(graph); }
        var bin = d.Id == "recycle-bin" ? DesktopUtilities.RecycleBin() : null;
        updates[d.Id] = () =>
        {
            var now = DateTime.Now;
            switch (d.Id)
            {
                case "timer": value.Text = timerFinished ? "Listo ✓" : DurationText(countdown.Remaining(DateTimeOffset.Now)); caption.Text = countdown.Running ? "EN MARCHA" : "TEMPORIZADOR"; break;
                case "stopwatch": value.Text = DurationText(chronometer.Elapsed); caption.Text = chronometer.IsRunning ? "CRONOMETRANDO" : "CRONÓMETRO"; break;
                case "day-progress": case "month-progress": case "year-progress":
                    double percent = d.Id == "day-progress" ? CalendarProgress.Day(now) : d.Id == "month-progress" ? CalendarProgress.Month(now) : CalendarProgress.Year(now);
                    value.Text = $"{percent:0.0}%"; progress!.Set(percent, Ui.Light ? "#467C6C" : "#B8E5D4"); caption.Text = d.Id == "day-progress" ? "DEL DÍA" : d.Id == "month-progress" ? "DEL MES" : "DEL AÑO"; break;
                case "date": value.Text = now.ToString("d MMM yyyy", CultureInfo.GetCultureInfo("es-ES")); value.FontSize = 13; caption.Text = now.ToString("dddd", CultureInfo.GetCultureInfo("es-ES")).ToUpperInvariant(); break;
                case "day-number": value.Text = now.Day.ToString(); value.FontSize = 24; caption.Text = now.ToString("MMM", CultureInfo.GetCultureInfo("es-ES")).ToUpperInvariant(); break;
                case "world-time": var world = WorldNow(); value.Text = world.ToString(settings.Clock12Hour ? (settings.ClockSeconds ? "hh:mm:ss tt" : "hh:mm tt") : (settings.ClockSeconds ? "HH:mm:ss" : "HH:mm")); value.FontSize = settings.ClockSeconds ? 13 : 16; caption.Text = "UTC" + (world.Offset < TimeSpan.Zero ? "−" : "+") + world.Offset.Duration().ToString(@"hh\:mm"); break;
                case "storage": var drive = ReadStorage(); value.Text = drive == null ? "—" : $"{drive.AvailableFreeSpace / 1073741824d:0} GB"; caption.Text = drive == null ? "SIN UNIDAD" : "LIBRES · " + drive.Name.TrimEnd('\\'); progress!.Visibility = drive == null ? Visibility.Collapsed : Visibility.Visible; progress.Set(drive == null ? 0 : (drive.TotalSize - drive.AvailableFreeSpace) * 100d / Math.Max(1, drive.TotalSize), Ui.Light ? "#467C6C" : "#B8E5D4"); break;
                case "network": value.Text = "↓ " + LocalMetrics.Rate(localMetrics.Download); value.FontSize = 12; caption.Text = "↑ " + LocalMetrics.Rate(localMetrics.Upload); if (graph != null) { var samples = localMetrics.DownloadHistory.ToArray(); double peak = Math.Max(1, samples.DefaultIfEmpty(1).Max()); var points = new PointCollection(); for (int i = 0; i < samples.Length; i++) points.Add(new Point(i * 95d / Math.Max(1, samples.Length - 1), 8 - 7 * samples[i] / peak)); graph.Points = points; } break;
                case "recycle-bin": caption.Text = bin.HasValue ? bin.Value.Items.ToString("N0", CultureInfo.GetCultureInfo("es-ES")) : "PAPELERA"; break;
                case "cpu": value.Text = $"{stats.Cpu:0}%"; ring!.Value = stats.Cpu; break;
                case "ram": value.Text = $"{stats.MemoryPercent:0}%"; ring!.Value = stats.MemoryPercent; break;
                case "keyboard": caption.Text = "TECLADO"; break;
                case "search": caption.Text = "BUSCAR"; break;
            }
        };
        var tile = Tile(stack, d.Name, action, d.Width); if (d.Id is "cpu" or "ram") tile.Padding = new Thickness(3); return tile;
    }
    void TickLocalWidgets()
    {
        if (settings.Widgets.Contains("network") || panel?.PageId == "network" && panel.AppWindow.IsVisible) localMetrics.ReadNetwork();
        if (countdown.CompleteIfDue(DateTimeOffset.Now)) { timerFinished = true; SaveSessionState(); if (settings.TimerSound && !QuietNow) DesktopUtilities.TimerAlert(); }
        if (panel?.PageId == "timer" && panel.AppWindow.IsVisible)
        {
            if (timerCounter != null) timerCounter.Text = DurationText(countdown.Remaining(DateTimeOffset.Now));
            timerProgress?.Set(Math.Clamp(100 - countdown.Remaining(DateTimeOffset.Now).TotalSeconds / settings.TimerSeconds * 100, 0, 100), Ui.Light ? "#467C6C" : "#B9E9D6");
            if (timerStatus != null) timerStatus.Text = timerFinished ? "Tiempo terminado" : countdown.Running ? "En marcha" : "En pausa · listo para continuar";
            if (timerToggle != null) timerToggle.Content = countdown.Running ? "Pausar" : timerFinished ? "Volver a iniciar" : "Iniciar";
        }
        if (panel?.PageId == "stopwatch" && panel.AppWindow.IsVisible && stopwatchCounter != null) stopwatchCounter.Text = DurationText(chronometer.Elapsed);
    }
    void ShowTimer()
    {
        var body = Ui.Stack(12);
        var display = Ui.Stack(10); display.HorizontalAlignment = HorizontalAlignment.Stretch;
        timerCounter = Ui.Text(DurationText(countdown.Remaining(DateTimeOffset.Now)), 46, bold: true); timerCounter.HorizontalAlignment = HorizontalAlignment.Center; display.Children.Add(timerCounter);
        timerStatus = Ui.Text(timerFinished ? "Tiempo terminado" : countdown.Running ? "En marcha" : "En pausa · listo para continuar", 12, Ui.MutedColor); timerStatus.HorizontalAlignment = HorizontalAlignment.Center; display.Children.Add(timerStatus);
        timerProgress = new QuotaMeter(Math.Clamp(100 - countdown.Remaining(DateTimeOffset.Now).TotalSeconds / settings.TimerSeconds * 100, 0, 100), 5, Ui.Light ? "#467C6C" : "#B9E9D6"); display.Children.Add(timerProgress); body.Children.Add(Ui.Card(display));
        var controls = new Grid { ColumnSpacing = 8 }; controls.ColumnDefinitions.Add(new()); controls.ColumnDefinitions.Add(new());
        timerToggle = Ui.Button(countdown.Running ? "Pausar" : "Iniciar", "Iniciar o pausar temporizador", () => { if (countdown.Running) countdown.Pause(DateTimeOffset.Now); else { if (countdown.Remaining(DateTimeOffset.Now) <= TimeSpan.Zero) countdown.Set(TimeSpan.FromSeconds(settings.TimerSeconds)); countdown.Start(DateTimeOffset.Now); timerFinished = false; } SaveSessionState(); ShowTimer(); }, true); controls.Children.Add(timerToggle);
        var restart = Ui.Button("Reiniciar", "Reiniciar temporizador", () => { countdown.Set(TimeSpan.FromSeconds(settings.TimerSeconds)); timerFinished = false; SaveSessionState(); ShowTimer(); }); Grid.SetColumn(restart, 1); controls.Children.Add(restart); body.Children.Add(controls);
        var fields = new Grid { ColumnSpacing = 8 }; for (int i = 0; i < 3; i++) fields.ColumnDefinitions.Add(new());
        NumberBox Input(string title, int value, int max, int column) { var box = new NumberBox { Header = title, Value = value, Minimum = 0, Maximum = max, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, SmallChange = 1 }; Grid.SetColumn(box, column); fields.Children.Add(box); return box; }
        var hours = Input("Horas", settings.TimerSeconds / 3600, 99, 0); var minutes = Input("Minutos", settings.TimerSeconds % 3600 / 60, 59, 1); var seconds = Input("Segundos", settings.TimerSeconds % 60, 59, 2); body.Children.Add(fields);
        body.Children.Add(Ui.Button("Usar esta duración", "Aplicar duración", () => { double total = hours.Value * 3600 + minutes.Value * 60 + seconds.Value; if (!double.IsFinite(total) || total < 1) return; settings.TimerSeconds = (int)Math.Clamp(total, 1, 359999); countdown.Set(TimeSpan.FromSeconds(settings.TimerSeconds)); timerFinished = false; SaveSessionState(); ShowTimer(); }));
        var presets = new Grid { ColumnSpacing = 6, RowSpacing = 6 }; presets.ColumnDefinitions.Add(new()); presets.ColumnDefinitions.Add(new());
        int presetIndex = 0;
        foreach (int preset in settings.TimerPresets)
        {
            if (presetIndex % 2 == 0) presets.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var favorite = Ui.Button(DurationText(TimeSpan.FromSeconds(preset)), "Duración " + preset + " segundos", () => { settings.TimerSeconds = preset; countdown.Set(TimeSpan.FromSeconds(preset)); timerFinished = false; SaveSessionState(); ShowTimer(); });
            Grid.SetRow(favorite, presetIndex / 2); Grid.SetColumn(favorite, presetIndex % 2); presets.Children.Add(favorite); presetIndex++;
        }
        body.Children.Add(presets);
        body.Children.Add(Ui.Button("Guardar duración como favorita", "Guardar duración favorita", () => { settings.TimerPresets = settings.TimerPresets.Where(v => v != settings.TimerSeconds).Append(settings.TimerSeconds).TakeLast(8).ToList(); Save(); ShowTimer(); }));
        var sound = new ToggleSwitch { Header = "Sonido al terminar", IsOn = settings.TimerSound }; sound.Toggled += (_, _) => { settings.TimerSound = sound.IsOn; Save(); }; body.Children.Add(sound);
        ShowPage("timer", "A tu ritmo", "TEMPORIZADOR", body);
    }
    void ShowStopwatch()
    {
        var body = Ui.Stack(12); stopwatchCounter = Ui.Text(DurationText(chronometer.Elapsed), 48, bold: true); stopwatchCounter.TextAlignment = TextAlignment.Center; body.Children.Add(Ui.Card(stopwatchCounter));
        body.Children.Add(Ui.Button(chronometer.IsRunning ? "Pausar" : "Iniciar", "Iniciar o pausar cronómetro", () => { if (chronometer.IsRunning) chronometer.Stop(); else chronometer.Start(); ShowStopwatch(); }, true));
        var lap = Ui.Button("Registrar vuelta", "Registrar vuelta", () => { laps.Add(chronometer.Elapsed); ShowStopwatch(); }); lap.IsEnabled = chronometer.IsRunning; body.Children.Add(lap);
        body.Children.Add(Ui.Button("Reiniciar cronómetro y vueltas", "Reiniciar cronómetro", () => { chronometer.Reset(); laps.Clear(); ShowStopwatch(); }));
        foreach (var (time, i) in laps.Select((v, i) => (v, i)).Reverse())
        {
            var lapRow = new Grid { ColumnSpacing = 12 }; lapRow.ColumnDefinitions.Add(new()); lapRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            lapRow.Children.Add(Ui.Text($"Vuelta {i + 1}", 12, Ui.MutedColor)); var lapTime = Ui.Text($"{DurationText(time)}.{time.Milliseconds:000}", 13, bold: true); Grid.SetColumn(lapTime, 1); lapRow.Children.Add(lapTime); body.Children.Add(Ui.Card(lapRow, 10));
        }
        body.Children.Add(Ui.Text("Las vueltas se conservan durante esta sesión de Brisa.", 11, Ui.MutedColor)); ShowPage("stopwatch", "Cada segundo", "CRONÓMETRO", body);
    }
    void ShowProgress(string id)
    {
        var body = Ui.Stack(16); var content = Ui.Stack(10);
        var value = Ui.Text("", 48, bold: true); value.TextAlignment = TextAlignment.Center; content.Children.Add(value);
        var meter = new QuotaMeter(0, 7, Ui.Light ? "#467C6C" : "#B8E5D4"); content.Children.Add(meter);
        var detail = Ui.Text("", 12, Ui.MutedColor); detail.TextAlignment = TextAlignment.Center; content.Children.Add(detail); body.Children.Add(Ui.Card(content));
        body.Children.Add(Ui.Text("Tiempo transcurrido según la fecha y hora local del equipo. La lectura se actualiza mientras este panel está abierto.", 12, Ui.MutedColor));
        void Update() { var now = DateTime.Now; double percent = id == "day-progress" ? CalendarProgress.Day(now) : id == "month-progress" ? CalendarProgress.Month(now) : CalendarProgress.Year(now); value.Text = $"{percent:0.00}%"; meter.Set(percent, Ui.Light ? "#467C6C" : "#B8E5D4"); detail.Text = id == "day-progress" ? now.ToString("dddd d MMMM · HH:mm", CultureInfo.GetCultureInfo("es-ES")) : id == "month-progress" ? $"Día {now.Day} de {DateTime.DaysInMonth(now.Year, now.Month)} · {now:MMMM yyyy}" : $"Día {now.DayOfYear} de {(DateTime.IsLeapYear(now.Year) ? 366 : 365)} · {now.Year}"; }
        Update(); ShowPage(id, id == "day-progress" ? "Tu día" : id == "month-progress" ? "Tu mes" : "Tu año", "PROGRESO · EN DIRECTO", body); livePanelUpdate = Update;
    }
    DateTimeOffset WorldNow() { try { return TimeZoneInfo.ConvertTime(DateTimeOffset.Now, TimeZoneInfo.FindSystemTimeZoneById(settings.WorldTimeZone)); } catch (TimeZoneNotFoundException) { return DateTimeOffset.UtcNow; } }
    string TimeZoneLabel() { try { return TimeZoneInfo.FindSystemTimeZoneById(settings.WorldTimeZone).StandardName; } catch (TimeZoneNotFoundException) { return "UTC"; } }
    void ShowWorldTime()
    {
        var body = Ui.Stack(12); var clock = Ui.Stack(8); var time = Ui.Text("", 46, bold: true); time.TextAlignment = TextAlignment.Center; var date = Ui.Text("", 14, Ui.MutedColor); date.TextAlignment = TextAlignment.Center; clock.Children.Add(time); clock.Children.Add(date); body.Children.Add(Ui.Card(clock));
        var offset = Ui.Text("", 12, Ui.MutedColor); body.Children.Add(offset);
        void Update() { var world = WorldNow(); time.Text = world.ToString(settings.Clock12Hour ? (settings.ClockSeconds ? "hh:mm:ss tt" : "hh:mm tt") : (settings.ClockSeconds ? "HH:mm:ss" : "HH:mm")); date.Text = world.ToString("dddd d MMMM", CultureInfo.GetCultureInfo("es-ES")); var difference = world.Offset - DateTimeOffset.Now.Offset; offset.Text = $"UTC{(world.Offset < TimeSpan.Zero ? "−" : "+")}{world.Offset.Duration():hh\\:mm} · {(difference.TotalHours >= 0 ? "+" : "")}{difference.TotalHours:0.##} h respecto a tu hora"; }
        Update();
        var zones = TimeZoneInfo.GetSystemTimeZones(); var select = new ComboBox { Header = "Zona horaria", ItemsSource = zones, DisplayMemberPath = "DisplayName", HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 280 };
        select.SelectedItem = zones.FirstOrDefault(t => t.Id == settings.WorldTimeZone); select.SelectionChanged += (_, _) => { if (select.SelectedItem is TimeZoneInfo zone) { settings.WorldTimeZone = zone.Id; Save(); Update(); } }; body.Children.Add(select); ShowPage("world-time", "En otra parte", "HORA MUNDIAL · EN DIRECTO", body); livePanelUpdate = Update;
    }
    void ShowStorage()
    {
        var body = Ui.Stack(12); var available = DriveInfo.GetDrives().Where(d => { try { return d.IsReady; } catch { return false; } }).ToArray();
        var select = new ComboBox { Header = "Unidad", ItemsSource = available.Select(d => d.Name).ToArray(), SelectedItem = settings.StorageDrive ?? Path.GetPathRoot(Environment.SystemDirectory), HorizontalAlignment = HorizontalAlignment.Stretch };
        var value = Ui.Text("", 38, bold: true); value.TextAlignment = TextAlignment.Center;
        var detail = Ui.Text("", 13, Ui.MutedColor); detail.TextAlignment = TextAlignment.Center;
        var meter = new QuotaMeter(0, 7, Ui.Light ? "#467C6C" : "#B8E5D4"); var content = Ui.Stack(10); content.Children.Add(value); content.Children.Add(detail); content.Children.Add(meter);
        var open = Ui.Button("Abrir unidad", "Abrir unidad seleccionada", () => { var reading = ReadStorage(); if (reading != null) Launch(reading.Name); });
        void Update() { var reading = ReadStorage(); value.Text = reading == null ? "Sin unidad" : LocalMetrics.Size(reading.AvailableFreeSpace); detail.Text = reading == null ? "La unidad seleccionada ya no está disponible." : "disponibles de " + LocalMetrics.Size(reading.TotalSize); meter.Visibility = reading == null ? Visibility.Collapsed : Visibility.Visible; meter.Set(reading == null ? 0 : (reading.TotalSize - reading.AvailableFreeSpace) * 100d / Math.Max(1, reading.TotalSize), Ui.Light ? "#467C6C" : "#B8E5D4"); open.IsEnabled = reading != null; }
        select.SelectionChanged += (_, _) => { if (select.SelectedItem is string drive) { settings.StorageDrive = drive; Save(); Update(); } }; body.Children.Add(select); body.Children.Add(Ui.Card(content)); body.Children.Add(open);
        body.Children.Add(Ui.Text("Lectura local cada 15 segundos mientras se usa el widget. Las unidades desconectadas se muestran como no disponibles.", 11, Ui.MutedColor));
        Update(); ShowPage("storage", "Un poco de espacio", "ALMACENAMIENTO · EN DIRECTO", body); livePanelUpdate = Update;
    }
    void ShowNetwork()
    {
        var body = Ui.Stack(12); var down = Ui.Text("", 30, bold: true); var up = Ui.Text("", 30, bold: true);
        Border Speed(TextBlock value, string label) { value.TextAlignment = TextAlignment.Center; var content = Ui.Stack(5); content.Children.Add(value); var caption = Ui.Text(label, 12, Ui.MutedColor); caption.TextAlignment = TextAlignment.Center; content.Children.Add(caption); return Ui.Card(content); }
        body.Children.Add(Speed(down, "Descarga")); body.Children.Add(Speed(up, "Subida"));
        body.Children.Add(Ui.Text("Suma de interfaces activas, excluyendo loopback y túneles. Adaptadores virtuales pueden contar el mismo tráfico más de una vez.", 12, Ui.MutedColor)); body.Children.Add(Ui.Button("Ajustes de red", "Abrir ajustes de red", () => Launch("ms-settings:network-status")));
        void Update() { down.Text = "↓ " + LocalMetrics.Rate(localMetrics.Download); up.Text = "↑ " + LocalMetrics.Rate(localMetrics.Upload); }
        Update(); ShowPage("network", "En movimiento", "RED · EN DIRECTO", body); livePanelUpdate = Update;
    }
    void ShowSearch()
    {
        var body = Ui.Stack(12); var input = new TextBox { PlaceholderText = "Busca en la web o escribe una carpeta", CornerRadius = new CornerRadius(12) }; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input, "Buscar en la web o abrir carpeta"); body.Children.Add(input);
        void Open() { var query = input.Text.Trim(); if (query.Length == 0) return; if (Directory.Exists(query)) Launch(Path.GetFullPath(query)); else Launch("https://www.bing.com/search?q=" + Uri.EscapeDataString(query)); }
        input.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) Open(); }; body.Children.Add(Ui.Button("Buscar / abrir", "Buscar o abrir carpeta", Open, true)); body.Children.Add(Ui.Text("Las búsquedas web se abren en el navegador predeterminado.", 12, Ui.MutedColor)); ShowPage("search", "Encuentra algo", "BÚSQUEDA", body);
    }
    void ShowRecycleBin()
    {
        var body = Ui.Stack(12); var info = DesktopUtilities.RecycleBin(); body.Children.Add(Ui.Text(info.HasValue ? $"{info.Value.Items} elementos" : "Papelera", 30, bold: true)); if (info.HasValue) body.Children.Add(Ui.Text(LocalMetrics.Size(info.Value.Bytes), 14, Ui.MutedColor));
        body.Children.Add(Ui.Button("Abrir papelera", "Abrir papelera de Windows", () => Launch("shell:RecycleBinFolder")));
        body.Children.Add(Ui.Button("Vaciar…", "Vaciar papelera con confirmación de Windows", () => { DesktopUtilities.EmptyBin(WinRT.Interop.WindowNative.GetWindowHandle(this)); ShowRecycleBin(); }));
        body.Children.Add(Ui.Text("Windows pedirá confirmación antes de vaciar la papelera.", 12, Ui.MutedColor)); ShowPage("recycle-bin", "La papelera", "WINDOWS", body);
    }
}
