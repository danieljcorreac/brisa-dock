using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    DateTimeOffset lastCalendarReminderCheck;
    void AddCalendarEvents(StackPanel body)
    {
        var items = CalendarSchedule.Between(settings.Events, new DateTimeOffset(selectedCalendarDate.Date), new DateTimeOffset(selectedCalendarDate.Date.AddDays(1)));
        body.Children.Add(Ui.Text("EVENTOS LOCALES", 11, Ui.MutedColor, true));
        foreach (var occurrence in items.Take(100))
        {
            var item = occurrence.Series;
            var card = Ui.Stack(5); card.Children.Add(Ui.Text(occurrence.Title, 16, bold: true));
            card.Children.Add(Ui.Text(item.AllDay ? "Todo el día" : $"{occurrence.Start.LocalDateTime:HH:mm} – {occurrence.End.LocalDateTime:dd/MM HH:mm}", 12, Ui.MutedColor));
            if (item.RecurrenceIcs != null)
            {
                card.Children.Add(Ui.Text("Evento recurrente", 11, Ui.MutedColor));
                card.Children.Add(Ui.Button("Editar sólo esta cita", "Editar sólo la cita del día seleccionado", () => ShowEventEditor(item.Id, occurrence)));
                card.Children.Add(Ui.Button("Omitir sólo esta cita", "Omitir esta cita y conservar el resto de la serie", () => SaveOccurrenceChange(() => CalendarSchedule.ChangeOccurrence(occurrence, null))));
                card.Children.Add(Ui.Button("Cambios de la serie", "Revisar y recuperar citas modificadas u omitidas", () => ShowOccurrenceChanges(item.Id)));
            }
            if (item.Reminder) card.Children.Add(Ui.Text(item.ReminderSent || settings.CalendarReminderKeys.Contains(occurrence.Key) ? "Recordatorio enviado" : "Aviso 15 minutos antes", 11, Ui.MutedColor));
            card.Children.Add(Ui.Button(item.RecurrenceIcs == null ? "Editar" : "Editar serie completa", "Editar evento " + item.Title, () => ShowEventEditor(item.Id)));
            card.Children.Add(Ui.Button(item.RecurrenceIcs == null ? "Archivar" : "Archivar serie completa", "Archivar evento " + item.Title, () => { settings.Events[settings.Events.FindIndex(e => e.Id == item.Id)] = item with { Archived = true }; Save(); ShowCalendar(); }));
            body.Children.Add(Ui.Card(card, 12));
        }
        if (items.Count == 0) body.Children.Add(Ui.Text("No tienes eventos locales este día.", 12, Ui.MutedColor));
        if (items.Count > 100) body.Children.Add(Ui.Text("Se muestran los primeros 100 eventos del día.", 12, Ui.MutedColor));
        body.Children.Add(Ui.Button("+ Añadir evento", "Añadir evento al calendario", () => ShowEventEditor(null), true));
        if (settings.Events.Any(e => e.Archived)) body.Children.Add(Ui.Button("Eventos archivados", "Restaurar eventos archivados", ShowArchivedEvents));
        if (settings.Events.Any(e => !e.Archived && e.RecurrenceIcs != null)) body.Children.Add(Ui.Button("Gestionar series", "Revisar cambios incluso si no quedan citas este día", ShowCalendarSeries));
        body.Children.Add(Ui.Button("Importar calendario (.ics)…", "Importar eventos de un archivo iCalendar", ImportCalendar));
        body.Children.Add(Ui.Button("Exportar calendario (.ics)…", "Exportar eventos a iCalendar", ExportCalendar));
        body.Children.Add(Ui.Text("Estos eventos se guardan en tu equipo. Los avisos requieren Brisa abierta y la bandeja activa. No se sincronizan con cuentas externas.", 11, Ui.MutedColor));
    }
    void ShowEventEditor(string? id, CalendarOccurrence? occurrence = null)
    {
        var existing = settings.Events.FirstOrDefault(e => e.Id == id); var body = Ui.Stack(12);
        if (occurrence != null && existing != null) existing = existing with { Title = occurrence.Title, Start = occurrence.Start, End = occurrence.End };
        if (occurrence != null) body.Children.Add(Ui.Text("Cambiarás únicamente esta cita. La repetición y los avisos siguen los ajustes de la serie.", 12, Ui.MutedColor));
        var title = new TextBox { Header = "Título", Text = existing?.Title ?? "", MaxLength = 120 }; body.Children.Add(title);
        var date = new CalendarDatePicker { HorizontalAlignment = HorizontalAlignment.Stretch, Header = "Fecha", Date = existing?.Start ?? new DateTimeOffset(selectedCalendarDate) }; body.Children.Add(date);
        var allDay = new ToggleSwitch { Header = "Todo el día", IsOn = existing?.AllDay ?? false, IsEnabled = occurrence == null }; body.Children.Add(allDay);
        var time = new TimePicker { HorizontalAlignment = HorizontalAlignment.Stretch, Header = "Hora", Time = existing?.Start.LocalDateTime.TimeOfDay ?? TimeSpan.FromHours(9), ClockIdentifier = "24HourClock" }; body.Children.Add(time);
        var duration = new NumberBox { Header = "Duración (minutos)", Value = existing == null ? 60 : (existing.End - existing.Start).TotalMinutes, Minimum = 5, Maximum = 10080, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact }; body.Children.Add(duration);
        void Update() { time.IsEnabled = !allDay.IsOn; duration.IsEnabled = !allDay.IsOn; time.Visibility = duration.Visibility = allDay.IsOn ? Visibility.Collapsed : Visibility.Visible; } allDay.Toggled += (_, _) => Update(); Update();
        var reminder = new ToggleSwitch { Header = "Recordar 15 minutos antes", IsOn = existing?.Reminder ?? false, IsEnabled = occurrence == null }; body.Children.Add(reminder);
        var repeat = new ComboBox { Header = "Repetición", ItemsSource = new[] { "No repetir", "Cada día", "Cada semana", "Cada mes", "Cada año", "Conservar regla actual" }, SelectedIndex = existing?.RecurrenceIcs == null ? 0 : 5, HorizontalAlignment = HorizontalAlignment.Stretch }; body.Children.Add(repeat);
        var interval = new NumberBox { Header = "Repetir cada (intervalos)", Value = 1, Minimum = 1, Maximum = 365, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact }; body.Children.Add(interval);
        var repeatUntil = new CalendarDatePicker { Header = "Último día de repetición (opcional)" }; body.Children.Add(repeatUntil);
        var repeatHelp = Ui.Text("Los días que no existen, como el 31 de febrero, se omiten. Sin fecha final la serie continúa.", 11, Ui.MutedColor); body.Children.Add(repeatHelp);
        void UpdateRepeat() { bool editable = occurrence == null && repeat.SelectedIndex is >= 1 and <= 4; interval.Visibility = repeatUntil.Visibility = repeatHelp.Visibility = editable ? Visibility.Visible : Visibility.Collapsed; }
        repeat.SelectionChanged += (_, _) => UpdateRepeat(); UpdateRepeat();
        if (occurrence != null) repeat.Visibility = Visibility.Collapsed;
        var error = Ui.Status(); body.Children.Add(error);
        body.Children.Add(Ui.Button("Guardar evento", "Guardar evento local", () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text) || !date.Date.HasValue || !double.IsFinite(duration.Value)) { error.Text = "Completa el título, la fecha y la duración."; return; }
            if (existing == null && settings.Events.Count >= 1000) { error.Text = "Se ha alcanzado el límite de 1000 eventos locales."; return; }
            var startDate = date.Date.Value.Date + (allDay.IsOn ? TimeSpan.Zero : time.Time);
            if (TimeZoneInfo.Local.IsInvalidTime(startDate)) { error.Text = "Esa hora no existe por el cambio de horario. Elige otra."; return; }
            try
            {
                var start = new DateTimeOffset(startDate, TimeZoneInfo.Local.GetUtcOffset(startDate));
                var endDate = allDay.IsOn ? startDate.AddDays(existing?.AllDay == true ? Math.Max(1, (existing.End.LocalDateTime.Date - existing.Start.LocalDateTime.Date).Days) : 1) : startDate.AddMinutes(Math.Clamp(duration.Value, 5, 10080));
                if (TimeZoneInfo.Local.IsInvalidTime(endDate)) { error.Text = "La hora final coincide con un cambio de horario. Ajusta la duración."; return; }
                var item = new CalendarEvent(existing?.Id ?? Guid.NewGuid().ToString("N"), title.Text.Trim(), start, new(endDate, TimeZoneInfo.Local.GetUtcOffset(endDate)), allDay.IsOn, Reminder: reminder.IsOn, ExternalUid: existing?.ExternalUid);
                if (occurrence != null) item = CalendarSchedule.ChangeOccurrence(occurrence, item);
                else if (repeat.SelectedIndex == 5 && existing?.RecurrenceIcs != null) item = CalendarSchedule.WithRule(item with { RecurrenceIcs = existing.RecurrenceIcs }, null, true);
                else if (repeat.SelectedIndex is >= 1 and <= 4)
                {
                    if (!double.IsFinite(interval.Value)) { error.Text = "Elige un intervalo válido."; return; }
                    string frequency = repeat.SelectedIndex switch { 1 => "DAILY", 2 => "WEEKLY", 3 => "MONTHLY", _ => "YEARLY" };
                    string rule = $"FREQ={frequency};INTERVAL={Math.Clamp((int)interval.Value, 1, 365)}";
                    if (repeatUntil.Date is { } until)
                    {
                        if (until.Date < startDate.Date) { error.Text = "La fecha final debe ser posterior al inicio."; return; }
                        var last = new DateTimeOffset(until.Date.AddDays(1).AddSeconds(-1)); rule += ";UNTIL=" + (allDay.IsOn ? until.Date.ToString("yyyyMMdd") : last.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'"));
                    }
                    item = CalendarSchedule.WithRule(item, rule);
                }
                int at = settings.Events.FindIndex(e => e.Id == item.Id); if (at < 0) settings.Events.Add(item); else settings.Events[at] = item;
                selectedCalendarDate = startDate.Date; calendarMonth = new(startDate.Year, startDate.Month, 1); lastCalendarReminderCheck = DateTimeOffset.MinValue; Save(); ShowCalendar();
            }
            catch (ArgumentOutOfRangeException) { error.Text = "La fecha o la duración están fuera del intervalo permitido."; }
            catch (Exception e) when (e is System.IO.InvalidDataException or FormatException or InvalidOperationException or ArgumentException) { error.Text = "No se pudo guardar la cita. Revisa los datos y el límite de cambios de la serie."; }
        }, true));
        body.Children.Add(Ui.Button("Cancelar", "Cancelar evento", ShowCalendar));
        ShowPage("event-editor", occurrence != null ? "Editar esta cita" : existing == null ? "Nuevo evento" : "Editar evento", "CALENDARIO LOCAL", body);
    }
    void SaveOccurrenceChange(Func<CalendarEvent> change)
    {
        try
        {
            var updated = change(); int at = settings.Events.FindIndex(e => e.Id == updated.Id);
            if (at < 0) return;
            settings.Events[at] = updated; lastCalendarReminderCheck = DateTimeOffset.MinValue; Save(); ShowCalendar();
        }
        catch (Exception e) when (e is System.IO.InvalidDataException or FormatException or InvalidOperationException or ArgumentException)
        { ShowMessage("Calendario", "No se pudo aplicar el cambio a la serie. Los datos anteriores se conservan."); }
    }
    void ShowCalendarSeries()
    {
        var body = Ui.Stack(10);
        if (!settings.Events.Any(e => !e.Archived && e.RecurrenceIcs != null)) body.Children.Add(Ui.Text("No tienes series recurrentes. Puedes crear una desde el calendario.", 13, Ui.MutedColor));
        foreach (var item in settings.Events.Where(e => !e.Archived && e.RecurrenceIcs != null)) body.Children.Add(Ui.Button(item.Title, "Gestionar serie " + item.Title, () => ShowOccurrenceChanges(item.Id)));
        body.Children.Add(Ui.Button("Volver", "Volver al calendario", ShowCalendar));
        ShowPage("calendar-series", "Series recurrentes", "CALENDARIO LOCAL", body);
    }
    void ShowOccurrenceChanges(string id)
    {
        var series = settings.Events.FirstOrDefault(e => e.Id == id); if (series == null) return;
        var body = Ui.Stack(10);
        try
        {
            var changes = CalendarSchedule.Exceptions(series);
            if (changes.Count == 0) body.Children.Add(Ui.Text("Esta serie no tiene citas modificadas u omitidas.", 13));
            foreach (var change in changes)
            {
                var card = Ui.Stack(6);
                card.Children.Add(Ui.Text(change.Title, 16, bold: true));
                card.Children.Add(Ui.Text($"Fecha original: {change.OriginalStart.LocalDateTime:dd/MM/yyyy HH:mm} · {(change.Cancelled ? "Omitida" : "Modificada")}", 12, Ui.MutedColor));
                card.Children.Add(Ui.Button("Recuperar cita original", "Restaurar la fecha y el contenido de la serie", () => SaveOccurrenceChange(() => CalendarSchedule.RestoreOccurrence(series, change.OriginalStart))));
                body.Children.Add(Ui.Card(card, 12));
            }
        }
        catch { body.Children.Add(Ui.Text("No se pueden leer los cambios de esta serie.", 13)); }
        body.Children.Add(Ui.Button("Editar serie completa", "Editar la serie completa", () => ShowEventEditor(id)));
        body.Children.Add(Ui.Button("Volver", "Volver al calendario", ShowCalendar));
        ShowPage("calendar-exceptions", series.Title, "CAMBIOS INDIVIDUALES", body);
    }
    void ShowArchivedEvents()
    {
        var body = Ui.Stack(10);
        if (!settings.Events.Any(e => e.Archived)) body.Children.Add(Ui.Text("No tienes eventos archivados.", 13, Ui.MutedColor));
        foreach (var item in settings.Events.Where(e => e.Archived).OrderByDescending(e => e.Start))
            body.Children.Add(Ui.Button(item.Title + " · Restaurar", "Restaurar evento " + item.Title, () => { settings.Events[settings.Events.FindIndex(e => e.Id == item.Id)] = item with { Archived = false }; Save(); ShowArchivedEvents(); }));
        body.Children.Add(Ui.Button("Volver al calendario", "Volver al calendario", ShowCalendar)); ShowPage("event-archive", "Eventos archivados", "RECUPERAR EVENTOS", body);
    }
    void TickCalendarReminders()
    {
        if (QuietNow || windowsIntegration?.TrayActive != true) return;
        if (DateTimeOffset.Now - lastCalendarReminderCheck < TimeSpan.FromSeconds(15)) return;
        lastCalendarReminderCheck = DateTimeOffset.Now;
        bool changed = false; var now = DateTimeOffset.Now;
        foreach (var occurrence in CalendarSchedule.Between(settings.Events.Where(e => e.Reminder), now, now.AddMinutes(15).AddSeconds(1)))
        {
            var item = occurrence.Series;
            if (occurrence.Start.AddMinutes(-15) > now || (item.RecurrenceIcs == null && item.ReminderSent) || settings.CalendarReminderKeys.Contains(occurrence.Key)) continue;
            if (!windowsIntegration.Notify("Evento próximo", occurrence.Title)) continue;
            settings.CalendarReminderKeys.Add(occurrence.Key);
            if (item.RecurrenceIcs == null) settings.Events[settings.Events.FindIndex(e => e.Id == item.Id)] = item with { ReminderSent = true };
            changed = true;
        }
        if (settings.CalendarReminderKeys.Count > 2000) settings.CalendarReminderKeys.RemoveRange(0, settings.CalendarReminderKeys.Count - 2000);
        if (changed) Save();
    }
}
