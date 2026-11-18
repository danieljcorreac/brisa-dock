using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    FocusSession focusSession = new(new());
    TextBlock? focusCounter;
    ProgressRing? focusRing;
    string? selectedNoteId;
    bool showArchivedNotes;
    DateTimeOffset lastSessionCheckpoint;
    bool QuietNow => AlertPolicy.IsQuiet(DateTime.Now, settings.QuietHours, settings.QuietStart, settings.QuietEnd);
    string FocusLabel => focusSession.Phase switch { FocusPhase.ShortBreak => "Descanso corto", FocusPhase.LongBreak => "Descanso largo", _ => "Concentraci\u00f3n" };
    string FormatFocus() => DurationText(focusSession.Remaining(DateTimeOffset.Now));
    void TickProductivity()
    {
        var now = DateTimeOffset.Now;
        if (focusSession.Tick(now))
        {
            SaveSessionState();
            if (!QuietNow) { windowsIntegration?.Notify("Foco", FocusLabel + " \u00b7 " + focusSession.Duration.TotalMinutes + " min"); if (settings.TimerSound) DesktopUtilities.TimerAlert(); }
            if (panel?.PageId == "focus" && panel.AppWindow.IsVisible) ShowFocus();
        }
        if (panel?.PageId == "focus" && panel.AppWindow.IsVisible)
        {
            if (focusCounter != null) focusCounter.Text = FormatFocus();
            if (focusRing != null) focusRing.Value = Math.Clamp(100 - focusSession.Remaining(now).TotalSeconds / focusSession.Duration.TotalSeconds * 100, 0, 100);
        }
        if ((focusSession.Running || countdown.Running) && now - lastSessionCheckpoint >= TimeSpan.FromSeconds(30)) { lastSessionCheckpoint = now; SaveSessionState(); }
        if (QuietNow || windowsIntegration?.TrayActive != true) return;
        bool changed = false;
        for (int i = 0; i < settings.Tasks.Count; i++)
        {
            var task = settings.Tasks[i];
            if (task.Archived || task.Done || task.ReminderSent || task.RemindAt == null || task.RemindAt > now) continue;
            if (windowsIntegration.Notify("Tarea pendiente", task.Text)) { settings.Tasks[i] = task with { ReminderSent = true }; changed = true; }
        }
        if (changed) Save();
    }
    void SaveSessionState()
    {
        lastSessionCheckpoint = DateTimeOffset.Now;
        settings.FocusState = focusSession.Snapshot(DateTimeOffset.Now); settings.TimerRemainingSeconds = countdown.Remaining(DateTimeOffset.Now).TotalSeconds;
        try { store.Save(settings); } catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { }
    }
    void ShowFocus()
    {
        var body = Ui.Stack(14); var face = new Grid { Width = 190, Height = 190, HorizontalAlignment = HorizontalAlignment.Center };
        face.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 186, Height = 186, Stroke = Ui.Brush(Ui.Light ? "#467C6C" : "#B9E9D6"), StrokeThickness = 4, Opacity = .16 });
        focusRing = new ProgressRing { Width = 186, Height = 186, IsIndeterminate = false, Value = 0, Foreground = Ui.Brush(Ui.Light ? "#477F6E" : "#B9E9D6") }; face.Children.Add(focusRing);
        var inside = Ui.Stack(3); inside.HorizontalAlignment = HorizontalAlignment.Center; inside.VerticalAlignment = VerticalAlignment.Center;
        focusCounter = Ui.Text(FormatFocus(), 38, bold: true); focusCounter.HorizontalAlignment = HorizontalAlignment.Center; inside.Children.Add(focusCounter); inside.Children.Add(Ui.Text(FocusLabel, 11, Ui.MutedColor)); face.Children.Add(inside); body.Children.Add(face);
        body.Children.Add(Ui.Text($"{focusSession.Completed} sesiones completadas \u00b7 descanso largo cada {focusSession.Options.Rounds}", 12, Ui.MutedColor));
        body.Children.Add(Ui.Button(focusSession.Running ? "Pausar" : "Empezar", "Iniciar o pausar foco", () => { focusSession.Toggle(DateTimeOffset.Now); SaveSessionState(); ShowFocus(); }, true));
        body.Children.Add(Ui.Button("Reiniciar ciclo", "Reiniciar ciclo de foco", () => { focusSession.Reset(); SaveSessionState(); ShowFocus(); }));
        var work = new NumberBox { Header = "Concentraci\u00f3n (min)", Value = settings.Focus.WorkMinutes, Minimum = 1, Maximum = 180, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var shortBreak = new NumberBox { Header = "Descanso corto (min)", Value = settings.Focus.ShortBreakMinutes, Minimum = 1, Maximum = 60, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var longBreak = new NumberBox { Header = "Descanso largo (min)", Value = settings.Focus.LongBreakMinutes, Minimum = 1, Maximum = 120, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var rounds = new NumberBox { Header = "Sesiones entre descansos largos", Value = settings.Focus.Rounds, Minimum = 1, Maximum = 12, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var automatic = new ToggleSwitch { Header = "Continuar autom\u00e1ticamente", IsOn = settings.Focus.AutoAdvance };
        var options = Ui.Stack(10); options.Children.Add(work); options.Children.Add(shortBreak); options.Children.Add(longBreak); options.Children.Add(rounds); options.Children.Add(automatic);
        options.Children.Add(Ui.Button("Aplicar y reiniciar ciclo", "Aplicar configuraci\u00f3n de foco", () =>
        {
            int Number(double value, int fallback) => double.IsFinite(value) ? (int)value : fallback;
            settings.Focus = new FocusOptions(Number(work.Value, 25), Number(shortBreak.Value, 5), Number(longBreak.Value, 15), Number(rounds.Value, 4), automatic.IsOn).Normalize(); focusSession.Configure(settings.Focus); SaveSessionState(); ShowFocus();
        }));
        body.Children.Add(new Expander { Header = "Duraciones y rondas", Content = options, HorizontalAlignment = HorizontalAlignment.Stretch });
        body.Children.Add(Ui.Text("Al cerrar Brisa se conserva el tiempo restante. Al volver, el ciclo queda en pausa para que lo retomes.", 11, Ui.MutedColor));
        ShowPage("focus", "Tiempo para ti", "FOCO \u00b7 DESCANSOS \u00b7 RONDAS", body); TickProductivity();
    }
    FrameworkElement NotesTile()
    {
        var line = Ui.Stack(3); var value = Ui.Text("Notas", 12, bold: true); value.MaxLines = 1; value.MaxWidth = 108; value.TextTrimming = TextTrimming.CharacterEllipsis;
        value.TextAlignment = TextAlignment.Center; value.HorizontalAlignment = HorizontalAlignment.Center; var caption = Ui.Text("", 10, Ui.MutedColor); caption.TextAlignment = TextAlignment.Center; caption.HorizontalAlignment = HorizontalAlignment.Center; line.Children.Add(value); line.Children.Add(caption);
        updates["notes"] = () => { var notes = settings.Notes.Where(n => !n.Archived).OrderByDescending(n => n.UpdatedAt).ToArray(); value.Text = notes.FirstOrDefault() is NoteItem latest ? NoteText.Title(latest.Text) : "Nueva nota"; caption.Text = notes.Length == 1 ? "1 NOTA" : $"{notes.Length} NOTAS"; };
        var content = Ui.Stack(9, true); content.Children.Add(Ui.Glyph("\uE70B", 22, Ui.Accent)); content.Children.Add(line); return Tile(content, "Mis notas", ShowNotes, 158);
    }
    void ShowNotes()
    {
        var body = Ui.Stack(10); var top = Ui.Stack(8, true);
        var create = Ui.Button("+ Nueva", "Crear nueva nota", () => { if (settings.Notes.Count >= 100) { ShowMessage("Notas", "Hay un l\u00edmite de 100 notas, incluidas las archivadas."); return; } var note = new NoteItem(Guid.NewGuid().ToString("N"), "", DateTimeOffset.Now); settings.Notes.Add(note); selectedNoteId = note.Id; showArchivedNotes = false; Save(); ShowNotes(); }); top.Children.Add(create);
        top.Children.Add(Ui.Button(showArchivedNotes ? "Ver activas" : "Archivadas", "Alternar notas archivadas", () => { showArchivedNotes = !showArchivedNotes; selectedNoteId = null; ShowNotes(); })); body.Children.Add(top);
        var visible = settings.Notes.Where(n => n.Archived == showArchivedNotes).OrderByDescending(n => n.UpdatedAt).ToArray();
        var selected = visible.FirstOrDefault(n => n.Id == selectedNoteId) ?? visible.FirstOrDefault(); selectedNoteId = selected?.Id;
        if (visible.Length > 0)
        {
            var picker = new ComboBox { Header = showArchivedNotes ? "Notas archivadas" : "Tus notas", ItemsSource = visible.Select(n => NoteText.Title(n.Text)).ToArray(), SelectedIndex = Array.IndexOf(visible, selected), HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 240 };
            picker.SelectionChanged += (_, _) => { if (picker.SelectedIndex >= 0) { selectedNoteId = visible[picker.SelectedIndex].Id; ShowNotes(); } }; body.Children.Add(picker);
        }
        if (selected != null)
        {
            var text = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 220, MaxHeight = 340, MaxLength = 10000, PlaceholderText = "Una idea para despu\u00e9s\u2026", CornerRadius = new CornerRadius(12), IsReadOnly = selected.Archived, Text = NoteText.Normalize(selected.Text) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(text, "Contenido de la nota");
            text.TextChanged += (_, _) => { int i = settings.Notes.FindIndex(n => n.Id == selected.Id); if (i < 0) return; settings.Notes[i] = settings.Notes[i] with { Text = NoteText.Normalize(text.Text), UpdatedAt = DateTimeOffset.Now }; settings.Note = NoteText.Normalize(text.Text); Save(); if (updates.TryGetValue("notes", out var update)) update(); };
            body.Children.Add(text);
            body.Children.Add(Ui.Button(selected.Archived ? "Restaurar nota" : "Archivar nota", selected.Archived ? "Restaurar nota" : "Archivar nota", () => { int i = settings.Notes.FindIndex(n => n.Id == selected.Id); if (i >= 0) settings.Notes[i] = settings.Notes[i] with { Archived = !selected.Archived }; selectedNoteId = null; Save(); ShowNotes(); Tick(); }));
            body.Children.Add(Ui.Text("Guardado autom\u00e1tico local. Las notas archivadas se pueden restaurar.", 11, Ui.MutedColor));
        }
        else body.Children.Add(Ui.Text(showArchivedNotes ? "No tienes notas archivadas." : "Crea una nota para guardar una idea.", 14, Ui.MutedColor));
        ShowPage("notes", "Para despu\u00e9s", "TU COLECCI\u00d3N DE NOTAS", body);
    }
    void ShowTasks()
    {
        var body = Ui.Stack(10); int count = settings.Tasks.Count(t => !t.Archived && !t.Done), total = settings.Tasks.Count(t => !t.Archived);
        var summary = Ui.Text(total == 0 ? "Una cosa cada vez." : $"{total - count} de {total} completadas", 13, Ui.MutedColor); body.Children.Add(summary);
        var taskProgress = new QuotaMeter(total == 0 ? 0 : 100d * (total - count) / total, 5, Ui.Light ? "#467C6C" : "#B9E9D6"); body.Children.Add(taskProgress);
        var createForm = Ui.Stack(8);
        var input = new TextBox { PlaceholderText = "A\u00f1adir una tarea\u2026", MaxLength = 300, CornerRadius = new CornerRadius(12) }; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input, "Nueva tarea"); createForm.Children.Add(input);
        var remind = new CheckBox { Content = "Recordarme esta tarea" }; createForm.Children.Add(remind);
        var date = new CalendarDatePicker { Date = DateTimeOffset.Now.AddMinutes(15), PlaceholderText = "Fecha", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        var time = new TimePicker { Time = DateTime.Now.AddMinutes(15).TimeOfDay, ClockIdentifier = "24HourClock", IsEnabled = false }; var schedule = Ui.Stack(8); schedule.Children.Add(date); schedule.Children.Add(time); schedule.Visibility = Visibility.Collapsed; createForm.Children.Add(schedule);
        remind.Checked += (_, _) => { date.IsEnabled = time.IsEnabled = true; schedule.Visibility = Visibility.Visible; }; remind.Unchecked += (_, _) => { date.IsEnabled = time.IsEnabled = false; schedule.Visibility = Visibility.Collapsed; };
        var error = Ui.Status(12, Ui.Light ? "#AE3535" : "#F4ABAB"); createForm.Children.Add(error);
        void Add()
        {
            if (string.IsNullOrWhiteSpace(input.Text)) return;
            DateTimeOffset? when = null;
            if (remind.IsChecked == true)
            {
                if (date.Date == null) { error.Text = "Elige una fecha."; return; }
                var local = DateTime.SpecifyKind(date.Date.Value.Date + time.Time, DateTimeKind.Unspecified);
                if (TimeZoneInfo.Local.IsInvalidTime(local)) { error.Text = "Esa hora no existe por el cambio de horario."; return; }
                when = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
                if (when <= DateTimeOffset.Now) { error.Text = "El recordatorio debe ser futuro."; return; }
            }
            settings.Tasks.Add(new(Guid.NewGuid().ToString("N"), input.Text.Trim(), RemindAt: when)); taskFilter = 0; taskSearch = ""; Save(); Tick(); ShowTasks();
        }
        input.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) { e.Handled = true; Add(); } }; createForm.Children.Add(Ui.Button("+ A\u00f1adir tarea", "A\u00f1adir tarea", Add, true));
        body.Children.Add(new Expander { Header = "+ Nueva tarea", Content = createForm, IsExpanded = total == 0, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        var search = new TextBox { PlaceholderText = "Buscar en mis tareas", Text = taskSearch, MaxLength = 300 }; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(search, "Buscar tareas"); body.Children.Add(search);
        var filter = new ComboBox { Header = "Mostrar", ItemsSource = new[] { "Todas", "Pendientes", "Completadas", "Archivadas" }, SelectedIndex = taskFilter, HorizontalAlignment = HorizontalAlignment.Stretch }; body.Children.Add(filter);
        var list = Ui.Stack(8); body.Children.Add(list);
        void RenderTasks()
        {
        list.Children.Clear(); taskSearch = search.Text; taskFilter = filter.SelectedIndex;
        var visible = settings.Tasks.Where(t => (taskFilter == 3 ? t.Archived : !t.Archived) && (taskFilter != 1 || !t.Done) && (taskFilter != 2 || t.Done) && t.Text.Contains(taskSearch, StringComparison.CurrentCultureIgnoreCase)).OrderBy(t => t.Done).ThenBy(t => t.RemindAt ?? DateTimeOffset.MaxValue).ToArray();
        if (visible.Length == 0) list.Children.Add(Ui.Text(taskFilter == 3 ? "No hay tareas archivadas que coincidan." : "No hay tareas que coincidan con este filtro.", 12, Ui.MutedColor));
        foreach (var item in visible)
        {
            var content = Ui.Stack(4); var label = Ui.Text(item.Text, 13, item.Done ? Ui.MutedColor : Ui.TextColor); label.MaxWidth = 265; content.Children.Add(label);
            if (item.RemindAt.HasValue) content.Children.Add(Ui.Text(item.RemindAt.Value.ToLocalTime().ToString("d MMM HH:mm") + (item.ReminderSent ? " \u00b7 aviso enviado" : ""), 10, Ui.MutedColor));
            var check = new CheckBox { Content = content, IsChecked = item.Done, HorizontalAlignment = HorizontalAlignment.Stretch };
            check.IsEnabled = !item.Archived;
            void Update() { int i = settings.Tasks.FindIndex(t => t.Id == item.Id); if (i >= 0) settings.Tasks[i] = settings.Tasks[i] with { Done = check.IsChecked == true }; int done = settings.Tasks.Count(t => !t.Archived && t.Done); int active = settings.Tasks.Count(t => !t.Archived); summary.Text = $"{done} de {active} completadas"; taskProgress.Set(active == 0 ? 0 : 100d * done / active, Ui.Light ? "#467C6C" : "#B9E9D6"); label.Foreground = Ui.Brush(check.IsChecked == true ? Ui.MutedColor : Ui.TextColor); Save(); Tick(); RenderTasks(); }
            check.Checked += (_, _) => Update(); check.Unchecked += (_, _) => Update();
            var menu = new MenuFlyout();
            void Reminder(string label, Func<DateTimeOffset?> at) { var entry = new MenuFlyoutItem { Text = label }; entry.Click += (_, _) => { int i = settings.Tasks.FindIndex(t => t.Id == item.Id); if (i >= 0) settings.Tasks[i] = settings.Tasks[i] with { RemindAt = at(), ReminderSent = false }; Save(); ShowTasks(); }; menu.Items.Add(entry); }
            Reminder("Recordar en 15 minutos", () => DateTimeOffset.Now.AddMinutes(15)); Reminder("Recordar en una hora", () => DateTimeOffset.Now.AddHours(1)); Reminder("Quitar recordatorio", () => null); check.ContextFlyout = menu;
            var card = Ui.Stack(6); card.Children.Add(check);
            if (item.Archived) card.Children.Add(Ui.Button("Restaurar tarea", "Restaurar " + item.Text, () => { int i = settings.Tasks.FindIndex(t => t.Id == item.Id); if (i >= 0) settings.Tasks[i] = settings.Tasks[i] with { Archived = false }; Save(); Tick(); ShowTasks(); }));
            else card.Children.Add(Ui.Button("Editar tarea y recordatorio", "Editar " + item.Text, () => ShowTaskEditor(item.Id)));
            list.Children.Add(Ui.Card(card, 10));
        }
        }
        search.TextChanged += (_, _) => RenderTasks(); filter.SelectionChanged += (_, _) => RenderTasks(); RenderTasks();
        if (settings.Tasks.Any(t => t.Done && !t.Archived)) body.Children.Add(Ui.Button("Archivar las completadas", "Archivar tareas completadas conservando la posibilidad de restaurarlas", () => { settings.Tasks = TaskEditing.ArchiveCompleted(settings.Tasks); Save(); Tick(); ShowTasks(); }));
        body.Children.Add(Ui.Text("Los avisos requieren Brisa abierta y el icono de bandeja activo. Windows puede silenciarlos. Las horas silenciosas aplazan los avisos.", 11, Ui.MutedColor));
        ShowPage("tasks", "Un poco de foco", "TAREAS \u00b7 RECORDATORIOS LOCALES", body);
    }
    string taskSearch = "";
    int taskFilter;
    void ShowTaskEditor(string id)
    {
        var task = settings.Tasks.FirstOrDefault(t => t.Id == id); if (task == null) return;
        var body = Ui.Stack(12);
        var text = new TextBox { Header = "Tarea", Text = task.Text, MaxLength = 300, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap }; body.Children.Add(text);
        var remind = new ToggleSwitch { Header = "Recordatorio", IsOn = task.RemindAt.HasValue }; body.Children.Add(remind);
        var initial = task.RemindAt ?? DateTimeOffset.Now.AddMinutes(15);
        var date = new CalendarDatePicker { Header = "Fecha", Date = initial, HorizontalAlignment = HorizontalAlignment.Stretch };
        var time = new TimePicker { Header = "Hora", Time = initial.LocalDateTime.TimeOfDay, ClockIdentifier = "24HourClock" };
        var reminderFields = Ui.Stack(10); reminderFields.Children.Add(date); reminderFields.Children.Add(time); body.Children.Add(reminderFields);
        void Toggle() { date.IsEnabled = time.IsEnabled = remind.IsOn; reminderFields.Visibility = remind.IsOn ? Visibility.Visible : Visibility.Collapsed; } remind.Toggled += (_, _) => Toggle(); Toggle();
        body.Children.Add(Ui.Text("Al cambiar la fecha se prepara un nuevo aviso. Las tareas completadas no envían recordatorios.", 11, Ui.MutedColor));
        var error = Ui.Status(12, Ui.Light ? "#AE3535" : "#F4ABAB"); body.Children.Add(error);
        body.Children.Add(Ui.Button("Guardar cambios", "Guardar tarea y recordatorio", () =>
        {
            DateTimeOffset? at = null;
            if (remind.IsOn)
            {
                if (date.Date == null) { error.Text = "Elige una fecha para el recordatorio."; return; }
                var local = DateTime.SpecifyKind(date.Date.Value.Date + time.Time, DateTimeKind.Unspecified);
                if (TimeZoneInfo.Local.IsInvalidTime(local)) { error.Text = "Esa hora no existe por el cambio de horario."; return; }
                at = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
            }
            try
            {
                int index = settings.Tasks.FindIndex(t => t.Id == id); if (index < 0) return;
                // Retain seconds when the visible date and minute were not changed.
                if (at.HasValue && task.RemindAt.HasValue && at.Value.LocalDateTime.Date == initial.LocalDateTime.Date && (int)at.Value.TimeOfDay.TotalMinutes == (int)initial.TimeOfDay.TotalMinutes) at = task.RemindAt;
                settings.Tasks[index] = TaskEditing.Change(settings.Tasks[index], text.Text, at, DateTimeOffset.Now); Save(); Tick(); ShowTasks();
            }
            catch (ArgumentException e) { error.Text = e.Message; }
        }, true));
        body.Children.Add(Ui.Button("Cancelar", "Volver sin guardar", ShowTasks)); ShowPage("task-editor", "Editar tarea", "TEXTO · FECHA · HORA", body);
    }
}
