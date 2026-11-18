using Microsoft.UI.Xaml;
using System.IO;
namespace Brisa.WinUI;
public partial class App : Application
{
    MainWindow? window;
    Mutex? singleton;
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => { try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "brisa-error.log"), DateTime.Now + " " + e.Exception + Environment.NewLine); } catch { } };
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string? packageCheck = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--check-package=", StringComparison.Ordinal));
        if (packageCheck != null)
        {
            object report;
            try { var package = MainWindow.VerifyLocalPackage(packageCheck[16..]); report = new { Passed = true, package.Version, Count = package.Files.Count }; }
            catch (Exception e) { report = new { Passed = false, Error = e.Message }; }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-package-check.json"), System.Text.Json.JsonSerializer.Serialize(report));
            Exit(); return;
        }
        if (Environment.GetCommandLineArgs().Contains("--check-tray-placement") || Environment.GetCommandLineArgs().Contains("--review-tray-menu")) { CheckTrayPlacement(Environment.GetCommandLineArgs().Contains("--review-tray-menu")); return; }
        if (Environment.GetCommandLineArgs().Contains("--check-reservation")) { CheckReservation(); return; }
        if (Environment.GetCommandLineArgs().Contains("--check-launchers"))
        {
            object report;
            try { report = ShellWorker.Run(LauncherChecks.Run).GetAwaiter().GetResult(); }
            catch (Exception e) { report = new { Passed = false, Error = e.ToString() }; }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-launchers-check.json"), System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Exit(); return;
        }
        if (Environment.GetCommandLineArgs().Contains("--check-apps"))
        {
            try
            {
                var apps = InstalledApplications.Read(true).GetAwaiter().GetResult();
                var sample = apps.Where(a => a.Source == "Steam").Take(2).Concat(apps.Where(a => a.Name.Contains("ChatGPT") || a.Name.Contains("Docker") || a.Name.Contains("Fork") || a.Name.Contains("Codex") || a.Name.Contains("Claude") || a.Name.Contains("Calcul") || a.Name.Contains("Chrome"))).DistinctBy(a => a.Target).ToArray();
                var report = new { Count = apps.Count, Sources = apps.GroupBy(a => a.Source).Select(g => new { Source = g.Key, Count = g.Count() }), Samples = sample.Select(a => new { a.Name, a.Source, Identity = Brisa.Core.AppTargets.Identity(ShortcutIdentity.Resolve(a.Target)), HasArguments = ShortcutIdentity.Describe(a.Target).Arguments.Length > 0, Icon = ShellIcons.Read(a.Target).GetAwaiter().GetResult() is { Width: > 0 } }), Windows = DesktopWindows.Read().Select(a => new { a.Name, AppId = a.Windows[0].AppUserModelId, Target = a.LaunchTarget, Matched = apps.Any(i => Brisa.Core.RunningApps.Matches(ShortcutIdentity.Resolve(i.Target), a)) }) };
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-apps-check.json"), System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-apps-check.json"), System.Text.Json.JsonSerializer.Serialize(new { Error = e.ToString() })); }
            Exit(); return;
        }
        if (Environment.GetCommandLineArgs().Contains("--check-recovery")) { CheckRecovery(); return; }
        if (Environment.GetCommandLineArgs().Contains("--check-notification")) { CheckNotification(); return; }
        if (Environment.GetCommandLineArgs().Contains("--check-recents"))
        {
            try
            {
                var report = Task.Run(() => DesktopWindows.Read().Select(app =>
                {
                    var documents = RecentDocuments.Read((nint)app.Windows[0].Handle, null);
                    return new { app.Name, documents.Identified, Count = documents.Items.Count };
                }).ToArray()).GetAwaiter().GetResult();
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-recents-check.json"), System.Text.Json.JsonSerializer.Serialize(report));
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-recents-check.json"), System.Text.Json.JsonSerializer.Serialize(new { Error = e.ToString() })); }
            Exit(); return;
        }
        if (Environment.GetCommandLineArgs().Contains("--check-audio"))
        {
            try
            {
                var outputs = Audio.Outputs();
                var checks = outputs.Select(output =>
                {
                    var before = Audio.Read(output.Id);
                    var sessions = Audio.ReadSessions(output.Id);
                    return new { output.Name, output.Default, Volume = before.Volume, Sessions = sessions.Select(s => new { s.Name, s.Volume, s.Muted, s.Active }) };
                }).ToArray();
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-audio-check.json"), System.Text.Json.JsonSerializer.Serialize(checks));
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-audio-check.json"), System.Text.Json.JsonSerializer.Serialize(new { Error = e.ToString() })); }
            Exit(); return;
        }
        if (Environment.GetCommandLineArgs().Contains("--check-visual"))
        {
            window = new MainWindow(Path.Combine(AppContext.BaseDirectory, "qa-data")); window.Title = "Brisa · Prueba visual"; window.Activate(); return;
        }
        singleton = new Mutex(true, "BrisaDock.WinUI.Local", out bool first);
        if (!first) { WindowsIntegration.RevealExisting(); Exit(); return; }
        window = new MainWindow(); window.Activate();
    }
    void CheckRecovery()
    {
        Window? probe = null; WindowsIntegration? integration = null;
        try
        {
            int checkpoints = 0, resumes = 0, layouts = 0, accessibility = 0, reveals = 0, menus = 0;
            probe = new Window { Title = "Brisa · Diagnóstico de recuperación" }; probe.AppWindow.Hide();
            integration = new(WinRT.Interop.WindowNative.GetWindowHandle(probe), () => reveals++, () => { }, () => { }, () => { }, () => layouts++, () => resumes++, () => { }, () => { }, () => checkpoints++, () => accessibility++);
            integration.QuickMenuRequested += _ => menus++;
            integration.ProbeLifecycleForChecks();
            bool trayRestored = integration.ProbeTrayRecoveryForChecks();
            integration.ProbeDisplayChangesForChecks();
            bool trayKeyboard = integration.ProbeTrayKeyboardForChecks();
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-recovery-check.json"), System.Text.Json.JsonSerializer.Serialize(new { Checkpoints = checkpoints, Resumes = resumes, TrayRestored = trayRestored, TrayKeyboard = trayKeyboard, Reveals = reveals, Menus = menus, Layouts = layouts, AccessibilityRefreshes = accessibility, Passed = checkpoints == 2 && resumes == 2 && trayRestored && trayKeyboard && reveals == 1 && menus == 1 && layouts == 2 && accessibility == 1, IsolatedWindow = true }));
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-recovery-check.json"), System.Text.Json.JsonSerializer.Serialize(new { Error = e.Message })); }
        finally { integration?.Dispose(); probe?.Close(); Exit(); }
    }
    async void CheckNotification()
    {
        Window? probe = null; WindowsIntegration? integration = null;
        try
        {
            probe = new Window { Title = "Brisa · Prueba de avisos" }; probe.AppWindow.Hide();
            integration = new WindowsIntegration(WinRT.Interop.WindowNative.GetWindowHandle(probe), () => { }, () => { }, () => { }, () => { }, () => { }, () => { }, () => { }, () => { });
            bool tray = integration.SetTray(true);
            bool accepted = integration.Notify("Brisa · Comprobación silenciosa", "Prueba técnica de presentación de avisos de Windows.", true);
            for (int i = 0; i < 40 && integration.LastNotificationShown == null; i++) await Task.Delay(250);
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-notification-check.json"), System.Text.Json.JsonSerializer.Serialize(new { TrayRegistered = tray, Accepted = accepted, Shown = integration.LastNotificationShown != null, Silent = true }));
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-notification-check.json"), System.Text.Json.JsonSerializer.Serialize(new { Error = e.Message })); }
        finally { integration?.Dispose(); probe?.Close(); Exit(); }
    }
}
