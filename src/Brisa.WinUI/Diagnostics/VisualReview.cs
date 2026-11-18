using Brisa.Core;
using Microsoft.UI.Xaml.Controls;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    void OpenReviewPage()
    {
        var args = Environment.GetCommandLineArgs(); if (!args.Contains("--check-visual")) return;
        Action? page = args.Contains("--review-appearance") ? ShowAppearance : args.Contains("--review-claude") ? () => ShowUsage("claude") : args.Contains("--review-backups") ? ShowBackupSettings : args.Contains("--review-app-manager") ? ShowAppsManager : args.Contains("--review-apps") ? ShowInstalledApps : args.Contains("--review-app-windows") ? ReviewAppWindow : args.Contains("--review-windows") ? ShowWindowsSettings : args.Contains("--review-accessibility") ? ShowAccessibilityPreferences : args.Contains("--review-installation") ? ShowInstallation : args.Contains("--review-health") ? ShowHealth : null;
        if (page != null) DispatcherQueue.TryEnqueue(() => page());
    }
    // Isolated manual QA only: never reachable in the normal application.
    async void ReviewAppWindow()
    {
        if (!Environment.GetCommandLineArgs().Contains("--check-visual")) return;
        var shortcut = settings.Apps.FirstOrDefault(); if (shortcut == null) return;
        var windows = await Task.Run(DesktopWindows.Read); if (closing) return;
        var app = RunningApps.ForTarget(ShortcutIdentity.Resolve(shortcut.Target), windows);
        if (app != null) ShowAppWindows(app, shortcut.Target);
    }
    int reviewIndex = -1;
    Action[]? reviewPages;
    bool ReviewFixtures => Environment.GetCommandLineArgs().Contains("--check-visual") && Environment.GetCommandLineArgs().Contains("--review-fixtures");
    void SetReviewQuotas()
    {
        if (!ReviewFixtures) return;
        var now = DateTimeOffset.Now;
        usage["claude"] = new("claude", ConnectionState.Connected, "Datos de ejemplo", "Revisión visual · datos de ejemplo", now, [new("5 h", 67, now.AddHours(2), 300), new("Semana", 73, now.AddDays(3), 10080), new("Fable", 26, now.AddDays(3), 10080)]);
        usage["chatgpt"] = new("chatgpt", ConnectionState.Connected, "Datos de ejemplo", "Revisión visual · datos de ejemplo", now, [new("5 h", 35, now.AddHours(2), 300), new("Semana", 100, now.AddDays(3), 10080)]);
        var reviewArgs = Environment.GetCommandLineArgs();
        if (reviewArgs.Contains("--review-docs"))
        {
            settings.Theme = reviewArgs.Contains("--review-light") ? "light" : "glass";
            settings.Material = "solid"; settings.Scale = 1; settings.IconSize = 30;
            settings.TrayIcon = false; settings.GlobalShortcuts = false; settings.ReserveSpace = false;
            settings.ShowRunningApps = false; settings.MonitorId = null; settings.WeatherLocation = null;
            settings.Widgets = ["apps", "claude", "chatgpt", "tasks", "sound", "cpu", "ram", "clock"];
            settings.Apps = [new("demo-files", "Explorador de archivos", "explorer.exe"), new("demo-calc", "Calculadora", "calc.exe")];
            settings.AppGroups = []; settings.PinnedOrder = ["app:demo-files", "app:demo-calc"];
            settings.Tasks = [new("demo-one", "Preparar la semana", true), new("demo-two", "Revisar las ideas del proyecto"), new("demo-three", "Organizar el escritorio")];
            settings.Notes = [new("demo-note", "Ideas, notas y pequeños recordatorios.", now)];
            ApplyPreferences();
        }
        if (reviewArgs.Contains("--review-usage-error")) usage["claude"] = usage["claude"] with { State = ConnectionState.Error, Message = "Fallo de conexión simulado. Se conserva la última lectura, sin actualizar." };
        if (reviewArgs.Contains("--review-usage-expired")) usage["claude"] = usage["claude"] with { UpdatedAt = now.AddMinutes(-20) };
        if (reviewArgs.Contains("--review-usage-empty")) usage["claude"] = new("claude", ConnectionState.Unavailable, "Claude no ha facilitado porcentajes de cuota. Datos de ejemplo.", "Revisión visual", now, []);
        settings.QuotaHistory.Clear();
        foreach (var provider in new[] { "claude", "chatgpt" }) foreach (var window in new[] { "5 h", "Semana" }) for (int i = 0; i < 12; i++) settings.QuotaHistory.Add(new(provider, window, 15 + i * 5, now.AddMinutes(-110 + i * 10), now.AddDays(3), "Revisión visual"));
    }
    void NextVisualReview()
    {
        if (!ReviewFixtures || closing) return;
        reviewPages ??= [
            ShowAppsManager, () => ShowUsage("claude"), () => ShowUsage("chatgpt"), ShowSystem, ShowBattery, ShowSound, ShowCalendar,
            ShowTasks, ShowNotes, ShowFocus, ShowMusic, ShowWeather, ShowAggregateUsage, ShowTimer, ShowStopwatch,
            () => ShowProgress("day-progress"), () => ShowProgress("month-progress"), () => ShowProgress("year-progress"),
            ShowCalendar, ShowCalendar, ShowWorldTime, ShowStorage, ShowNetwork, ShowSearch, ShowRecycleBin,
            () => ShowMessage("Teclado en pantalla", "Este acceso abre el teclado de Windows. No tiene un panel propio de Brisa."), ShowSystem, ShowSystem,
            ShowSettings, ShowAppearance, ShowLayoutPreferences, ShowDockSettings, ShowWidgetPreferences, ShowGallery, ShowOrder,
            ShowWindowsSettings, ShowBackupSettings, ShowRecoveryBackups, ShowAccessibilityPreferences,
            () => ShowTaskEditor(settings.Tasks[0].Id), () => ShowEventEditor(null), ShowCalendarSeries, ShowArchivedEvents,
            () => ShowGroupEditor(null), ShowClaudeDirectConsent, ShowClaudeSetup, () => ShowUsageHistory("claude"),
            () => ShowUsageHistory("chatgpt"), () => ShowFolderStack(store.DirectoryPath),
            () => ShowRecentDocuments("Archivos de ejemplo", null, null),
            () => ShowBackupPreview(SettingsBackup.Read(SettingsBackup.Export(settings)), "Copia de ejemplo"),
            () => ShowMessage("Sin resultados", "No hay elementos disponibles. Puedes volver a intentarlo desde el widget."),
            ReviewPopulatedWeather, ReviewGroup, () => ShowGroupEditor("review-group"), ReviewSeries,
            () => ShowOccurrenceChanges("review-series"), ReviewOccurrence, ShowArchivedEvents
        ];
        reviewIndex = (reviewIndex + 1) % reviewPages.Length;
        activeAnchor = null; reviewPages[reviewIndex]();
        Title = $"Brisa · Prueba visual {reviewIndex + 1}/{reviewPages.Length}";
    }
    void ReviewPopulatedWeather()
    {
        settings.WeatherLocation = new("Ciudad de ejemplo", "Revisión visual", 0, 0); weather = new(18, 12, 22, 2, true, DateTimeOffset.Now); ShowWeather();
    }
    void ReviewGroup()
    {
        if (!settings.Apps.Any(a => a.Id == "review-folder")) settings.Apps.Add(new("review-folder", "Carpeta de ejemplo", store.DirectoryPath, true));
        if (!settings.AppGroups.Any(g => g.Id == "review-group")) settings.AppGroups.Add(new("review-group", "Trabajo y proyectos", ["review-folder"]));
        ShowAppGroup("review-group");
    }
    void ReviewSeries()
    {
        var start = DateTimeOffset.Now.AddHours(2); var end = start.AddHours(1);
        string Stamp(DateTimeOffset value) => value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");
        string ics = $"BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Brisa//QA//ES\r\nBEGIN:VEVENT\r\nUID:review-series\r\nDTSTART:{Stamp(start)}\r\nDTEND:{Stamp(end)}\r\nSUMMARY:Revisión semanal\r\nRRULE:FREQ=WEEKLY;COUNT=4\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
        settings.Events.RemoveAll(e => e.Id is "review-series" or "review-archived"); settings.Events.Add(new("review-series", "Revisión semanal", start, end, RecurrenceIcs: ics));
        settings.Events.Add(new("review-archived", "Evento archivado de ejemplo", start.AddDays(-1), end.AddDays(-1), Archived: true)); ShowCalendarSeries();
    }
    void ReviewOccurrence()
    {
        var occurrence = CalendarSchedule.Between(settings.Events.Where(e => e.Id == "review-series"), DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1)).FirstOrDefault();
        if (occurrence != null) ShowEventEditor("review-series", occurrence);
    }
}
