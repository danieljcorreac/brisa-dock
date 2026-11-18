using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Globalization;
using Windows.Media.Control;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    DateTime calendarMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1);
    DateTime selectedCalendarDate = DateTime.Today;
    string galleryCategory = "Todos";
    void ShowGallery()
    {
        var body = Ui.Stack(16);
        body.Children.Add(Ui.Text($"{settings.Widgets.Count} widgets en tu dock · Vistas de ejemplo", 12, Ui.MutedColor));
        var organization = Ui.Stack(10); organization.Children.Add(Ui.Button("Organizar los widgets de mi dock", "Organizar widgets existentes", ShowOrder));
        if (!galleryPositionChosen || (galleryInsertAfter != null && !settings.Widgets.Contains(galleryInsertAfter))) galleryInsertAfter = settings.Widgets.LastOrDefault();
        var positions = new[] { "Al principio" }.Concat(settings.Widgets.Select(id => "Después de " + WidgetCatalog.Find(id)!.Name)).ToArray();
        var insertAt = new ComboBox { Header = "Colocar los nuevos widgets", ItemsSource = positions, SelectedIndex = galleryInsertAfter == null ? 0 : Math.Max(0, settings.Widgets.IndexOf(galleryInsertAfter) + 1), HorizontalAlignment = HorizontalAlignment.Stretch };
        insertAt.SelectionChanged += (_, _) => { galleryPositionChosen = true; galleryInsertAfter = insertAt.SelectedIndex <= 0 ? null : settings.Widgets[insertAt.SelectedIndex - 1]; }; organization.Children.Add(insertAt);
        var search = new TextBox { PlaceholderText = "Buscar widgets…", Text = galleryQuery, CornerRadius = new CornerRadius(12) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(search, "Buscar widgets"); body.Children.Add(search);
        var categoryNames = new[] { "Todos" }.Concat(WidgetCatalog.All.Select(w => w.Category).Distinct()).ToArray();
        var category = new ComboBox { ItemsSource = categoryNames, SelectedItem = galleryCategory, HorizontalAlignment = HorizontalAlignment.Stretch };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(category, "Categoría de widgets"); body.Children.Add(category);
        body.Children.Add(new Expander { Header = "Orden y posición al añadir", Content = organization, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        var categories = Ui.Stack(14); body.Children.Add(categories);
        void Render()
        {
            categories.Children.Clear();
            var matches = WidgetCatalog.All.Where(w => (galleryCategory == "Todos" || w.Category == galleryCategory) && (string.IsNullOrWhiteSpace(search.Text) || (w.Name + " " + w.Description).Contains(search.Text, StringComparison.CurrentCultureIgnoreCase))).ToArray();
            foreach (var group in matches.GroupBy(w => w.Category))
            {
                categories.Children.Add(Ui.Text(group.Key.ToUpperInvariant(), 11, Ui.MutedColor, true));
                var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 }; grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new());
                int i = 0;
                foreach (var d in group)
                {
                    if (i % 2 == 0) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
                    var content = Ui.Stack(10); content.Children.Add(WidgetSample(d)); content.Children.Add(Ui.Text(d.Name, 15, bold: true));
                    content.Children.Add(Ui.Text(d.Description, 12, Ui.MutedColor));
                    bool active = settings.Widgets.Contains(d.Id);
                    content.Children.Add(Ui.Text(active ? "✓ En tu dock" : "Disponible", 11, active ? Ui.Accent : Ui.MutedColor, active));
                    content.Children.Add(Ui.Button(active ? "Quitar del dock" : "+ Añadir al dock", (active ? "Quitar " : "Añadir ") + d.Name, () => { if (active) { RemoveWidgetAnimated(d.Id); ShowGallery(); return; } WidgetOrder.InsertAfter(settings.Widgets, d.Id, galleryInsertAfter); galleryInsertAfter = d.Id; galleryPositionChosen = true; Save(); BuildDock(); ShowGallery(); }, !active));
                    var card = Ui.Card(content); Grid.SetRow(card, i / 2); Grid.SetColumn(card, i % 2); grid.Children.Add(card); i++;
                }
                categories.Children.Add(grid);
            }
            if (matches.Length == 0) categories.Children.Add(Ui.Text("No hay widgets con ese nombre.", 13, Ui.MutedColor));
        }
        category.SelectionChanged += (_, _) => { galleryCategory = category.SelectedItem as string ?? "Todos"; Render(); };
        search.TextChanged += (_, _) => { galleryQuery = search.Text; Render(); }; Render();
        ShowPage("gallery", "Explora tus widgets", "COLECCIÓN", body, true);
    }
    void ShowSettings() => ShowPreferencesHome();
    void ShowCalendar()
    {
        var body = Ui.Stack(12); body.Children.Add(Ui.Text(DateTime.Now.ToString("D", CultureInfo.GetCultureInfo("es-ES")), 15));
        var navigation = new Grid { ColumnSpacing = 6 }; navigation.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); navigation.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); navigation.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        navigation.Children.Add(Ui.Text(calendarMonth.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-ES")), 17, bold: true));
        var previous = Ui.Button("‹", "Mes anterior", () => { calendarMonth = calendarMonth.AddMonths(-1); ShowCalendar(); }); previous.IsEnabled = calendarMonth.Year > 1 || calendarMonth.Month > 1; Grid.SetColumn(previous, 1); navigation.Children.Add(previous);
        var next = Ui.Button("›", "Mes siguiente", () => { calendarMonth = calendarMonth.AddMonths(1); ShowCalendar(); }); next.IsEnabled = calendarMonth.Year < 9999 || calendarMonth.Month < 12; Grid.SetColumn(next, 2); navigation.Children.Add(next); body.Children.Add(navigation);
        var calendar = new Grid { ColumnSpacing = 3, RowSpacing = 3 };
        for (int column = 0; column < 7; column++) calendar.ColumnDefinitions.Add(new());
        for (int r = 0; r < 7; r++) calendar.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var weekdays = new[] { "L", "M", "X", "J", "V", "S", "D" };
        for (int column = 0; column < 7; column++) { var label = Ui.Text(weekdays[column], 11, Ui.MutedColor, true); label.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetColumn(label, column); calendar.Children.Add(label); }
        var gridStart = CalendarProgress.GridStart(calendarMonth);
        var monthEvents = CalendarSchedule.Between(settings.Events, new DateTimeOffset(gridStart), new DateTimeOffset(gridStart.AddDays(42)));
        for (int i = 0; i < 42; i++)
        {
            var date = gridStart.AddDays(i); bool selected = date.Date == selectedCalendarDate.Date;
            var text = Ui.Text(date.Day.ToString() + (monthEvents.Any(e => e.Start.LocalDateTime.Date <= date.Date && e.End.LocalDateTime > date.Date) ? " ·" : ""), 13, selected ? Ui.Light ? "#FFFFFF" : "#142B24" : date.Month == calendarMonth.Month ? Ui.TextColor : Ui.MutedColor, date.Date == DateTime.Today);
            var day = Ui.Button(text, date.ToString("D", CultureInfo.GetCultureInfo("es-ES")), () => { selectedCalendarDate = date; ShowCalendar(); }, selected); day.Padding = new Thickness(3); day.Height = 35; day.CornerRadius = new CornerRadius(10);
            if (date.Date == DateTime.Today && !selected) { day.BorderBrush = Ui.Brush(Ui.Light ? "#467C6C" : "#B8E5D4"); day.BorderThickness = new Thickness(1); }
            Grid.SetRow(day, 1 + i / 7); Grid.SetColumn(day, i % 7); calendar.Children.Add(day);
        }
        body.Children.Add(calendar); body.Children.Add(Ui.Text(selectedCalendarDate.ToString("D", CultureInfo.GetCultureInfo("es-ES")), 12, Ui.MutedColor));
        body.Children.Add(Ui.Button("Volver a hoy", "Ir a hoy", () => { selectedCalendarDate = DateTime.Today; calendarMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1); ShowCalendar(); }));
        var remaining = settings.Tasks.Where(t => !t.Done).Take(3).ToList();
        body.Children.Add(Ui.Text(remaining.Count > 0 ? "TAREAS PENDIENTES" : "Un día por delante.", 11, Ui.MutedColor, true));
        foreach (var task in remaining) body.Children.Add(Ui.Text("○  " + task.Text, 12));
        AddCalendarEvents(body);
        ShowPage("calendar", "Hoy", "TU DÍA, DE UN VISTAZO", body);
    }
    void ShowSystem()
    {
        var body = Ui.Stack(12); var info = Ui.Stack(6);
        body.Children.Add(Ui.Text(stats.ProcessorName, 15, bold: true));
        body.Children.Add(Ui.Text($"{Environment.ProcessorCount} procesadores lógicos disponibles · {Environment.Is64BitOperatingSystem switch { true => "64", false => "32" }} bits", 11, Ui.MutedColor));
        var cpu = Ui.Text("", 46, Ui.Light ? "#426D62" : "#B9E9D6", true); info.Children.Add(cpu); info.Children.Add(Ui.Text("Uso del procesador", 12, Ui.MutedColor));
        var cpuBar = new QuotaMeter(stats.Cpu, 5, Ui.Light ? "#467C6C" : "#B8E5D4"); info.Children.Add(cpuBar); body.Children.Add(Ui.Card(info));
        var memory = Ui.Stack(7); var ram = Ui.Text("", 23, bold: true); var detail = Ui.Text("", 13, Ui.MutedColor); var ramBar = new QuotaMeter(stats.MemoryPercent, 5, Ui.Light ? "#467C6C" : "#B8E5D4");
        memory.Children.Add(ram); memory.Children.Add(detail); memory.Children.Add(ramBar); body.Children.Add(Ui.Card(memory));
        var uptime = Ui.Text("", 11, Ui.MutedColor); body.Children.Add(uptime);
        void Update() { cpu.Text = $"{stats.Cpu:0}%"; cpuBar.Set(stats.Cpu, Ui.Light ? "#467C6C" : "#B8E5D4"); ram.Text = $"{stats.MemoryPercent:0}% de memoria"; detail.Text = $"{stats.UsedGb:0.0} GB en uso de {stats.TotalGb:0.0} GB"; ramBar.Set(stats.MemoryPercent, Ui.Light ? "#467C6C" : "#B8E5D4"); var up=TimeSpan.FromMilliseconds(Environment.TickCount64); uptime.Text=$"Tiempo encendido: {(int)up.TotalDays} d {up.Hours} h {up.Minutes} min"; }
        Update(); body.Children.Add(Ui.Button("Administrador de tareas", "Abrir Administrador de tareas", () => Launch("taskmgr.exe")));
        ShowPage("system", "El pulso de tu PC", "CPU Y MEMORIA · EN DIRECTO", body); livePanelUpdate = Update;
    }
    void ShowBattery()
    {
        var body = Ui.Stack(14); var level = Ui.Text("", 42, bold: true); var source = Ui.Text("", 14, Ui.MutedColor); var bar = new QuotaMeter(stats.Battery ?? 0, 5, Ui.Accent);
        body.Children.Add(level); body.Children.Add(source); body.Children.Add(bar);
        var batteryDetail = Ui.Text("", 12, Ui.MutedColor); body.Children.Add(batteryDetail);
        void Update()
        {
            level.Text = stats.Battery is byte b ? $"{b}%" : "Sin lectura";
            source.Text = !stats.PowerAvailable ? "No se pudo consultar el estado de energía." : stats.Battery == null ? "Windows no informa de una batería disponible." : stats.Charging ? "Cargando" : stats.Plugged ? "Conectado a la corriente" : "Funcionando con batería";
            bar.Visibility = stats.Battery == null ? Visibility.Collapsed : Visibility.Visible; bar.Set(stats.Battery ?? 0, stats.Battery < 20 ? Ui.Light ? "#A53E32" : "#F2A995" : Ui.Accent);
            batteryDetail.Text = stats.Battery == null ? "" : (stats.BatterySaver ? "Ahorro de batería activado" : "Ahorro de batería desactivado") + (stats.BatteryRemaining is { } remaining ? $"\nAutonomía estimada por Windows: {(int)remaining.TotalHours} h {remaining.Minutes} min" : "\nWindows no facilita una estimación de autonomía.");
        }
        Update(); body.Children.Add(Ui.Button("Ajustes de energía", "Abrir ajustes de energía", () => Launch("ms-settings:powersleep"))); ShowPage("battery", "Tu energía", "ENERGÍA · EN DIRECTO", body); livePanelUpdate = Update;
    }
    void ShowSound()
    {
        RenderSoundMixer();
    }
}
