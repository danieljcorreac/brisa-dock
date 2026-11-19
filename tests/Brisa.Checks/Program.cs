using Brisa.Core;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Net;

if (args.FirstOrDefault() == "github-update-probe")
{
    using var client = new GitHubUpdates();
    var release = await client.CheckAsync(Version.Parse(args[1]), CancellationToken.None);
    string? package = release == null || args.Length < 3 ? null : await client.DownloadAsync(release, args[2], CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(new { Available = release != null, release?.Version, Package = package }));
    return 0;
}
if (args.Contains("desktop-direct-probe"))
{
    var direct = await new ClaudeDesktopDirectProvider().RefreshAsync(CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(new { direct.State, direct.Message, direct.Source, direct.UpdatedAt, direct.Windows }));
    return direct.State == ConnectionState.Connected ? 0 : 1;
}
if (args.Contains("codex-probe"))
{
    var codex = await new CodexUsageProvider(args.Length > 1 ? args[1] : null).RefreshAsync(CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(new { codex.State, codex.Source, codex.Windows }));
    return codex.State == ConnectionState.Connected ? 0 : 1;
}
if (args.Contains("desktop-probe"))
{
    var desktop = await new ClaudeDesktopProvider().RefreshAsync(CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(new { desktop.State, desktop.Source, desktop.UpdatedAt, desktop.Windows }));
    return desktop.State is ConnectionState.Connected or ConnectionState.Stale ? 0 : 1;
}
if (args.Contains("app-server"))
{
    for (int i = 0; i < 512; i++) Console.Error.Write(new string('x', 4096));
    bool initialized = false;
    while (Console.ReadLine() is string line)
    {
        using var request = JsonDocument.Parse(line); var root = request.RootElement;
        string? method = root.GetProperty("method").GetString();
        if (method == "initialize") Console.WriteLine("{\"id\":1,\"result\":{}}");
        else if (method == "initialized") initialized = true;
        else if (method == "account/rateLimits/read" && initialized)
        {
            switch (Environment.GetEnvironmentVariable("BRISA_TEST_CODEX_REPLY"))
            {
                case "error": Console.WriteLine("{\"id\":2,\"error\":{\"code\":-32001,\"message\":\"Session expired fixture\"}}"); break;
                case "malformed": Console.WriteLine("{invalid-json"); break;
                case "exit": return 0;
                default:
            Console.WriteLine("{\"id\":2,\"result\":{\"rateLimits\":{\"primary\":{\"usedPercent\":37,\"windowDurationMins\":300,\"resetsAt\":" + DateTimeOffset.Now.AddHours(3).ToUnixTimeSeconds() + "}}}}");
                    break;
            }
        }
        else return 2;
    }
    return 0;
}

int checks = 0;
var backgroundErrors = new List<string>(); var jobs = new BackgroundWork((key, error) => backgroundErrors.Add(key + ":" + error.GetType().Name));
var slowGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); int slowCalls = 0; bool fastRan = false;
var slowJob = jobs.RunAsync("slow", _ => { slowCalls++; return slowGate.Task; }, CancellationToken.None);
await jobs.RunAsync("fast", _ => { fastRan = true; return Task.CompletedTask; }, CancellationToken.None);
Check(fastRan && !slowJob.IsCompleted && jobs.RunningCount == 1, "A slow background source does not delay an independent source");
await jobs.RunAsync("slow", _ => { slowCalls++; return Task.CompletedTask; }, CancellationToken.None);
Check(slowCalls == 1, "Background ticks cannot duplicate an in-flight source");
slowGate.SetResult(); await slowJob; Check(jobs.RunningCount == 0, "Completed background sources release their slot");
await jobs.RunAsync("fault", _ => Task.FromException(new IOException("isolated failure")), CancellationToken.None);
await jobs.RunAsync("fault", _ => Task.CompletedTask, CancellationToken.None);
Check(backgroundErrors.SequenceEqual(new[] { "fault:IOException" }) && jobs.RunningCount == 0, "A failed source is reported and can recover on a later tick");
using (var stoppedJobs = new CancellationTokenSource()) { stoppedJobs.Cancel(); bool ran = false; await jobs.RunAsync("stopped", _ => { ran = true; return Task.CompletedTask; }, stoppedJobs.Token); Check(!ran, "Closing the app prevents new background work"); }
using (var cancelledJob = new CancellationTokenSource()) { var pending = jobs.RunAsync("cancel", token => Task.Delay(Timeout.InfiniteTimeSpan, token), cancelledJob.Token); cancelledJob.Cancel(); await pending; Check(jobs.RunningCount == 0 && backgroundErrors.Count == 1, "Cancellation releases a pending source without reporting a shutdown error"); }
var dragSlots = new[] { new DragSlot("wide", 0, 190), new DragSlot("small", 194, 64), new DragSlot("last", 262, 80) };
Check(WidgetDragMath.Destination(dragSlots, "wide", 400, 0) == 2 && WidgetDragMath.Destination(dragSlots, "last", -30, 2) == 0, "Wide and narrow widgets can reach either end using their unclipped drag center");
Check(WidgetDragMath.Destination(dragSlots, "wide", 229, 0) == 0 && WidgetDragMath.Destination(dragSlots, "wide", 234, 0) == 1 && WidgetDragMath.Destination(dragSlots, "wide", 223, 1) == 1 && WidgetDragMath.Destination(dragSlots, "wide", 218, 1) == 0, "The drag midpoint margin prevents oscillation until the pointer crosses it");
var shiftedSlots = dragSlots.Select(slot => slot with { X = slot.X - 500 }).ToArray();
Check(WidgetDragMath.Destination(shiftedSlots, "wide", -100, 0) == 2 && WidgetDragMath.Destination(dragSlots, "wide", double.NaN, 1) == 1, "Drag destination handles negative origins and invalid pointer readings");
double oneFrame = WidgetDragMath.ScrollOffset(400, 400, 50, 500, .016); double twoFrames = WidgetDragMath.ScrollOffset(400, 400, WidgetDragMath.ScrollOffset(400, 400, 50, 500, .008), 500, .008);
Check(Math.Abs(oneFrame - twoFrames) < .001 && oneFrame > 50, "Edge scrolling has a stable speed across frame rates");
Check(WidgetDragMath.ScrollOffset(-20, 400, 2, 500, .016) == 0 && WidgetDragMath.ScrollOffset(450, 400, 498, 500, .016) == 500 && WidgetDragMath.ScrollOffset(200, 400, 50, 500, .016) == 50, "Edge scrolling stops at either boundary and stays still away from the edges");
Check(WidgetDragMath.ScrollOffset(400, 400, 50, 500, 5) <= 82, "A delayed UI frame cannot produce a large scroll jump");
var oldMidnightMaterial = JsonSerializer.Deserialize<DockSettings>("{\"Theme\":\"midnight\"}")!; oldMidnightMaterial.Normalize();
var oldGlassMaterial = JsonSerializer.Deserialize<DockSettings>("{\"Theme\":\"glass\"}")!; oldGlassMaterial.Normalize();
Check(oldMidnightMaterial.Material == "solid" && oldGlassMaterial.Material == "translucent", "Legacy themes preserve their material when the separate appearance preference is absent");
var independentAppearance = new DockSettings { Theme = "midnight", Material = "translucent", TintStrength = .45 }; independentAppearance.Normalize();
Check(independentAppearance.Material == "translucent" && independentAppearance.Theme == "midnight", "Translucency can be selected independently of the midnight color");
var materialBackup = SettingsBackup.Read(SettingsBackup.Export(independentAppearance)).Settings;
Check(materialBackup.Material == "translucent" && materialBackup.TintStrength == .45, "Portable copies preserve material and tint strength");
var invalidAppearance = new DockSettings { Theme = "light", Material = "unknown", TintStrength = double.NaN }; invalidAppearance.Normalize();
Check(invalidAppearance.Material == "translucent" && invalidAppearance.TintStrength == .3, "Invalid appearance data falls back to safe values");
independentAppearance.TintStrength = 5; independentAppearance.Normalize(); Check(independentAppearance.TintStrength == .8, "Tint strength remains within the readable customization range");
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
var shortWork = new PreviewRect(781, 2160, 1920, 550); var bottomDock = new PreviewRect(950, 2595, 1500, 103);
var panelCases = (from dpi in new[] { 1d, 1.5, 2d } from size in new[] { .85, 1d, 1.15 } from wide in new[] { false, true } select PanelLayout.Fit(shortWork, bottomDock, dpi * size, wide, 1200, 2600)).ToArray();
var navigationAnchor = new PanelAnchor();
var anchorWork = new PreviewRect(0, 0, 1920, 1080);
var originalDock = new PreviewRect(200, 960, 1500, 90);
var smallerDock = new PreviewRect(450, 960, 1000, 90);
var firstOptions = navigationAnchor.Place(anchorWork, originalDock, 1, false, 500, 1650, false, true);
var optionsAfterToggle = navigationAnchor.Place(anchorWork, smallerDock, 1, false, 500, null, true, false);
var preferencesAfterBack = navigationAnchor.Place(anchorWork, smallerDock, 1, false, 650, 1100, true, false);
Check(firstOptions.X == optionsAfterToggle.X && firstOptions.X == preferencesAfterBack.X, "Options keep their horizontal position after dock resize and back navigation");
Check(PanelLayout.Fit(anchorWork, smallerDock, 1, false, 650).X != firstOptions.X, "Regression scenario would move the panel when using the rebuilt dock anchor");
var wideNavigation = navigationAnchor.Place(anchorWork, smallerDock, 1, true, 650, null, true, false);
var narrowReturn = navigationAnchor.Place(anchorWork, smallerDock, 1, false, 500, null, true, false);
Check(wideNavigation.X >= anchorWork.X && wideNavigation.X + wideNavigation.Width <= anchorWork.Width && narrowReturn.X == firstOptions.X, "Wide panels fit the monitor and return to the original narrow position");
var differentWidget = navigationAnchor.Place(anchorWork, smallerDock, 1, false, 500, 650, true, true);
Check(differentWidget.X != firstOptions.X && differentWidget.X == 445, "Clicking a different dock element starts a fresh panel anchor");
var reopenedOptions = navigationAnchor.Place(anchorWork, smallerDock, 1, false, 500, 1400, false, false);
Check(reopenedOptions.X == 1195, "Reopening a closed panel uses its new opening anchor");
var movedMonitor = navigationAnchor.Place(new(-1920, 0, 1920, 1080), new(-1700, 960, 1200, 90), 1, false, 500, -750, true, false);
Check(movedMonitor.X < 0 && movedMonitor.X >= -1908 && movedMonitor.X + movedMonitor.Width <= -12, "Monitor changes reset panel placement within the new work area");
var changedScale = navigationAnchor.Place(anchorWork, smallerDock, 1.5, false, 500, 1400, true, false);
Check(changedScale.Width == 615 && changedScale.X >= 12 && changedScale.X + changedScale.Width <= 1908, "DPI changes recalculate panel placement without retaining stale physical coordinates");
Check(panelCases.All(p => p.X >= shortWork.X + 12 && p.Y >= shortWork.Y + 12 && p.X + p.Width <= shortWork.X + shortWork.Width - 12 && p.Y + p.Height <= bottomDock.Y - 12), "Panels fit above a bottom dock on a short secondary monitor at all three sizes and DPI scales");
var topWork = new PreviewRect(-1920, 0, 1920, 1080); var topDock = new PreviewRect(-1700, 12, 1400, 104);
var topPanel = PanelLayout.Fit(topWork, topDock, 1.725, true, 900, -1900);
Check(topPanel.X >= topWork.X + 12 && topPanel.Y >= topDock.Y + topDock.Height + 12 && topPanel.X + topPanel.Width <= 0 && topPanel.Y + topPanel.Height <= 1068, "Panel placement handles negative monitor coordinates, top docks and off-center anchors");
Check(PanelLayout.Width(1920, .85, false) < PanelLayout.Width(1920, 1, false) && PanelLayout.Width(1920, 1, false) < PanelLayout.Width(1920, 1.15, false), "Panel widths follow the selected small, normal and large UI sizes");
var tinyPanel = PanelLayout.Fit(new(0, 0, 320, 240), new(20, 190, 280, 38), 2.3, true, 2000);
Check(tinyPanel.Width <= 296 && tinyPanel.Y >= 12 && tinyPanel.Y + tinyPanel.Height <= 178, "Oversized panel content is bounded on a small screen instead of covering the dock");
var taskNow = DateTimeOffset.UtcNow;
var editableTask = new TodoItem("edit-fixture", "Original", RemindAt: taskNow.AddHours(-1), ReminderSent: true);
var renamedTask = TaskEditing.Change(editableTask, "  Renamed  ", editableTask.RemindAt, taskNow);
Check(renamedTask.Text == "Renamed" && renamedTask.Id == editableTask.Id && renamedTask.ReminderSent, "Renaming a task keeps its identity and does not resend an unchanged reminder");
var rescheduledTask = TaskEditing.Change(editableTask, "Rescheduled", taskNow.AddHours(1), taskNow);
Check(!rescheduledTask.ReminderSent && rescheduledTask.RemindAt == taskNow.AddHours(1), "Rescheduling a task prepares a new reminder");
Check(TaskEditing.Change(editableTask, "Without reminder", null, taskNow).RemindAt == null, "Removing a task reminder preserves the task");
try { TaskEditing.Change(editableTask, "Invalid reminder", taskNow.AddMinutes(-1), taskNow); Check(false, "A newly scheduled reminder must be future"); } catch (ArgumentException) { Check(true, "Task edits reject a new reminder in the past"); }
try { TaskEditing.Change(editableTask, "   ", null, taskNow); Check(false, "Task text cannot be empty"); } catch (ArgumentException) { Check(true, "Task edits reject empty text without altering the original"); }
var archivedTasks = TaskEditing.ArchiveCompleted([editableTask, editableTask with { Id = "completed", Done = true }, editableTask with { Id = "archived", Done = true, Archived = true }]);
Check(!archivedTasks[0].Archived && archivedTasks[1].Archived && archivedTasks[2].Archived && archivedTasks.Count == 3, "Archiving completed tasks retains all records and leaves pending tasks active");
var taskBackup = SettingsBackup.Read(SettingsBackup.Export(new DockSettings { Tasks = archivedTasks }));
Check(taskBackup.Settings.Tasks.Count(t => t.Archived) == 2 && taskBackup.Settings.Tasks.Single(t => t.Id == "completed").Done, "Task archives survive portable backup roundtrip with completion state intact");
Check(JsonSerializer.Deserialize<TodoItem>("{\"Id\":\"legacy\",\"Text\":\"Keep me\",\"Done\":true}") is { Archived: false, Done: true }, "Existing task data remains active when the archive field is absent");
var pollTime = DateTimeOffset.UtcNow; var poll = new UsagePollSchedule();
Check(poll.TryStart("claude-direct", pollTime, TimeSpan.FromMinutes(5), false) && poll.TryStart("codex", pollTime, TimeSpan.FromMinutes(5), false), "Claude and Codex have independent initial polling budgets");
Check(!poll.TryStart("codex", pollTime.AddMinutes(10), TimeSpan.FromMinutes(5), true), "In-flight quota refresh cannot queue a duplicate even after its interval");
poll.Complete("codex"); poll.Complete("claude-direct");
Check(!poll.TryStart("codex", pollTime.AddSeconds(1), TimeSpan.FromMinutes(5), true) && !poll.TryStart("codex", pollTime.AddMinutes(2), TimeSpan.FromMinutes(5), false), "One-second ticks and early automatic refreshes do not trigger quota queries");
Check(poll.TryStart("codex", pollTime.AddSeconds(30), TimeSpan.FromMinutes(5), true) && !poll.TryStart("claude-direct", pollTime.AddSeconds(30), TimeSpan.FromMinutes(5), false), "Manual Codex refresh becomes available after thirty seconds without refreshing Claude");
poll.Complete("codex");
Check(poll.TryStart("claude-direct", pollTime.AddMinutes(5), TimeSpan.FromMinutes(5), false), "Automatic quota refresh becomes due at its five-minute boundary"); poll.Complete("claude-direct");
poll.SetAutomaticDue("claude-direct", pollTime.AddMinutes(20));
Check(!poll.TryStart("claude-direct", pollTime.AddMinutes(10), TimeSpan.FromMinutes(5), false), "Provider rate-limit backoff also postpones its automatic scheduling");
Check(poll.TryStart("local-file", pollTime, TimeSpan.FromSeconds(30), false), "Local bridge polling uses its own file-only interval"); poll.Complete("local-file");
Check(!poll.TryStart("local-file", pollTime.AddSeconds(29), TimeSpan.FromSeconds(30), false) && poll.TryStart("local-file", pollTime.AddSeconds(30), TimeSpan.FromSeconds(30), false), "Local file polling waits thirty seconds without involving network providers");
var desktopNow = DateTimeOffset.FromUnixTimeMilliseconds(1794823200000);
string DesktopFixture(object[] samples, object? version = null) => JsonSerializer.Serialize(new { version = version ?? (object)2, samples });
UsageSnapshot DesktopParse(string text) { using var json = JsonDocument.Parse(text); return ClaudeDesktopProvider.Parse(json.RootElement, desktopNow); }
var desktopSample = new { t = desktopNow.AddMinutes(-2).ToUnixTimeMilliseconds(), org = "fixture-org", u = new { fh = 0, sd = 60 } };
var desktopReading = DesktopParse(DesktopFixture([desktopSample]));
var directNow = DateTimeOffset.UtcNow;
var midnightReset = DateTimeOffset.Parse("2026-11-21T22:59:59.999+00:00"); var midnightWindow = new QuotaWindow("Semana", 67, midnightReset);
Check(midnightWindow.DisplayReset == DateTimeOffset.Parse("2026-11-21T23:00:00Z") && midnightWindow.Expired(midnightReset), "Reset display rounds the minute boundary without delaying actual expiry");
Check(new QuotaWindow("Sesión", 2, null).DisplayReset == null && new QuotaWindow("Semana", 67, DateTimeOffset.Parse("2026-11-21T23:00:00Z")).DisplayReset == DateTimeOffset.Parse("2026-11-21T23:00:00Z"), "Missing or exact-minute reset dates are not fabricated or shifted");
var directPayload = JsonSerializer.Serialize(new { five_hour = new { utilization = 2, resets_at = directNow.AddHours(5) }, seven_day = new { utilization = 67, resets_at = directNow.AddDays(7) }, limits = new[] { new { kind = "weekly_scoped", percent = 26, resets_at = directNow.AddDays(7), scope = new { model = new { id = (string?)null, display_name = "Fable" } } } } });
using var directJson = JsonDocument.Parse(directPayload);
var directParsed = UsageParser.ClaudeDirect(directJson.RootElement, directNow);
Check(directParsed.Windows.Select(w => w.UsedPercent).SequenceEqual(new double[] { 2, 67, 26 }) && directParsed.Windows[2].Label == "Fable" && directParsed.Windows.All(w => w.ResetsAt.HasValue), "Direct Desktop parser preserves session, week and Fable quotas with reset dates even when model ID is null");
using var limitsOnlyJson = JsonDocument.Parse(JsonSerializer.Serialize(new { limits = new[] { new { kind = "session", percent = 3, resets_at = directNow.AddHours(5) }, new { kind = "weekly_all", percent = 68, resets_at = directNow.AddDays(7) } } }));
Check(UsageParser.ClaudeDirect(limitsOnlyJson.RootElement, directNow).Windows.Select(w => w.UsedPercent).SequenceEqual(new double[] { 3, 68 }), "Direct parser accepts named limits without duplicating legacy windows");
using var malformedQuotaJson = JsonDocument.Parse("{\"five_hour\":{\"utilization\":101},\"limits\":[null,{\"kind\":\"weekly_scoped\",\"percent\":-1}]}");
Check(UsageParser.ClaudeDirect(malformedQuotaJson.RootElement, directNow).Windows.Count == 0, "Direct parser rejects malformed and invalid percentages without inventing missing Fable usage");
string CredentialKey(string account = "account", string org = "org", string host = "https://api.anthropic.com") => $"acct:{account}|code:{org}:{host}:user:profile user:inference";
var credentialData = new Dictionary<string, object?> { [CredentialKey()] = new { token = "fixture-secret", expiresAt = directNow.AddHours(1).ToUnixTimeMilliseconds() } };
using var credentialJson = JsonDocument.Parse(JsonSerializer.Serialize(credentialData));
var selectedCredential = ClaudeDesktopSession.Select("account", [credentialJson.RootElement], directNow);
Check(selectedCredential.Identity == "account:org" && !selectedCredential.ToString().Contains("fixture-secret"), "Desktop credential selection matches the signed-in account and redacts its printable representation");
using var revokedCredential = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?> { [CredentialKey()] = null }));
try { ClaudeDesktopSession.Select("account", [credentialJson.RootElement, revokedCredential.RootElement], directNow); Check(false, "Revoked Desktop credential must fail"); } catch (ClaudeDesktopSessionException) { Check(true, "V2 tombstones cannot resurrect a revoked V1 credential"); }
credentialData[CredentialKey(org: "other")] = new { token = "other-fixture-secret", expiresAt = directNow.AddHours(1).ToUnixTimeMilliseconds() };
using var ambiguousCredential = JsonDocument.Parse(JsonSerializer.Serialize(credentialData));
try { ClaudeDesktopSession.Select("account", [ambiguousCredential.RootElement], directNow); Check(false, "Ambiguous Desktop credential must fail"); } catch (ClaudeDesktopSessionException) { Check(true, "Desktop direct credentials reject multiple organizations"); }
using var wrongCredential = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?> { [CredentialKey(host: "https://other.example")] = new { token = "fixture-secret", expiresAt = directNow.AddHours(1).ToUnixTimeMilliseconds() } }));
try { ClaudeDesktopSession.Select("account", [wrongCredential.RootElement], directNow); Check(false, "Foreign credential host must fail"); } catch (ClaudeDesktopSessionException) { Check(true, "Desktop credential selector rejects tokens for a different host"); }
try { ClaudeDesktopSession.Select("other-account", [credentialJson.RootElement], directNow); Check(false, "Wrong account must fail"); } catch (ClaudeDesktopSessionException) { Check(true, "Desktop direct connection never selects another signed-in account"); }
var aesKey = RandomNumberGenerator.GetBytes(32); var nonce = RandomNumberGenerator.GetBytes(12); var clear = Encoding.UTF8.GetBytes(credentialJson.RootElement.GetRawText()); var cipher = new byte[clear.Length]; var tag = new byte[16];
using (var aes = new AesGcm(aesKey, 16)) aes.Encrypt(nonce, clear, cipher, tag);
var cacheBytes = "v10"u8.ToArray().Concat(nonce).Concat(cipher).Concat(tag).ToArray();
using (var decrypted = ClaudeDesktopSession.DecryptCache(Convert.ToBase64String(cacheBytes), aesKey)) Check(ClaudeDesktopSession.Select("account", [decrypted.RootElement], directNow).Identity == "account:org", "Desktop AES-GCM cache decrypts only an authenticated payload and retains readable JSON after buffer cleanup");
cacheBytes[^1] ^= 1;
try { using var invalid = ClaudeDesktopSession.DecryptCache(Convert.ToBase64String(cacheBytes), aesKey); Check(false, "Tampered cache must fail"); } catch (CryptographicException) { Check(true, "Tampered Desktop credential caches fail authentication"); }
CryptographicOperations.ZeroMemory(aesKey); CryptographicOperations.ZeroMemory(clear);
int directRequests = 0; bool correctDestination = true;
using var directClient = new HttpClient(new QuotaTestHttpHandler(request => { directRequests++; correctDestination &= request.Method == HttpMethod.Get && request.RequestUri!.AbsoluteUri == "https://api.anthropic.com/api/oauth/usage" && request.Headers.Authorization?.Parameter == "fixture-secret"; return new(HttpStatusCode.OK) { Content = new StringContent(directPayload) }; }));
var directProvider = new ClaudeDesktopDirectProvider(directClient, () => selectedCredential, () => directNow);
var firstDirect = await directProvider.RefreshAsync(CancellationToken.None); var secondDirect = await directProvider.RefreshAsync(CancellationToken.None);
Check(directRequests == 1 && correctDestination && firstDirect.Windows.Count == 3 && secondDirect.UpdatedAt == firstDirect.UpdatedAt, "Direct quota connection sends only authenticated GET to its fixed destination and caches repeated refreshes");
int throttledRequests = 0;
using var throttledClient = new HttpClient(new QuotaTestHttpHandler(_ => { throttledRequests++; return new(HttpStatusCode.TooManyRequests); }));
var throttledProvider = new ClaudeDesktopDirectProvider(throttledClient, () => selectedCredential, () => directNow);
await throttledProvider.RefreshAsync(CancellationToken.None); directNow = directNow.AddMinutes(6); await throttledProvider.RefreshAsync(CancellationToken.None);
Check(throttledRequests == 1, "Desktop usage rate limiting enforces at least fifteen minutes of backoff");
using var unauthorizedClient = new HttpClient(new QuotaTestHttpHandler(_ => new(HttpStatusCode.Unauthorized)));
Check((await new ClaudeDesktopDirectProvider(unauthorizedClient, () => selectedCredential).RefreshAsync(CancellationToken.None)).Windows.Count == 0, "Expired Desktop authorization shows no cached quotas as current");
var preservedConsent = new DockSettings { ClaudeDesktopDirect = false }; SettingsBackup.Apply(preservedConsent, new DockSettings { ClaudeDesktopDirect = true });
Check(!preservedConsent.ClaudeDesktopDirect, "Portable backup restoration cannot silently grant Desktop session access");
Check(desktopReading.State == ConnectionState.Connected && desktopReading.Windows.Count == 2 && desktopReading.Windows[0].UsedPercent == 0 && desktopReading.Windows[1].UsedPercent == 60 && desktopReading.Windows.All(w => w.ResetsAt == null), "Desktop quota reads real zero and weekly percentages without inventing reset dates");
var oldDesktop = desktopSample with { t = desktopNow.AddMinutes(-16).ToUnixTimeMilliseconds() };
Check(DesktopParse(DesktopFixture([oldDesktop])).State == ConnectionState.Stale, "Old Desktop quota remains visibly stale instead of becoming a fresh reading");
Check(DesktopParse(DesktopFixture([desktopSample, oldDesktop])).UpdatedAt == desktopReading.UpdatedAt, "Desktop selects the latest sample by timestamp rather than file order");
Check(DesktopParse(DesktopFixture([desktopSample, desktopSample with { org = "other-org" }])).State == ConnectionState.Unavailable, "Desktop refuses ambiguous histories from multiple organizations");
Check(DesktopParse(DesktopFixture([desktopSample], "2")).State == ConnectionState.Unavailable && DesktopParse(DesktopFixture([desktopSample], 3)).State == ConnectionState.Unavailable, "Desktop rejects unsupported or malformed schema versions without crashing");
Check(DesktopParse(DesktopFixture([new { t = desktopSample.t, org = "fixture-org", u = new { fh = -1, sd = 101 } }])).Windows.Count == 0, "Desktop rejects percentages outside the valid range");
Check(DesktopParse(DesktopFixture([new { t = desktopSample.t, org = "fixture-org", u = new { sd = 42 } }])).Windows.Count == 1 && DesktopParse(DesktopFixture([])).Windows.Count == 0, "Missing Desktop windows remain missing rather than appearing as zero");
Check(DesktopParse(DesktopFixture([desktopSample with { t = desktopNow.AddHours(1).ToUnixTimeMilliseconds() }])).Windows.Count == 0, "Desktop ignores implausible future timestamps");
var sourceSettings = new DockSettings { ClaudeSource = "desktop" }; sourceSettings.Normalize();
Check(JsonSerializer.Deserialize<DockSettings>(JsonSerializer.Serialize(sourceSettings))!.ClaudeSource == "desktop", "Claude source selection survives settings roundtrip");
sourceSettings.ClaudeSource = "unknown"; sourceSettings.Normalize(); Check(sourceSettings.ClaudeSource == "code", "Unknown Claude sources fall back to the existing Code connector");
var restoreConnection = new DockSettings { ClaudeSource = "off" }; SettingsBackup.Apply(restoreConnection, new DockSettings { ClaudeSource = "desktop" });
Check(restoreConnection.ClaudeSource == "off", "Restoring a backup cannot silently enable the Desktop quota reader");
var sourceHistory = new List<QuotaSample>();
UsageHistory.Append(sourceHistory, desktopReading, desktopNow); UsageHistory.Append(sourceHistory, desktopReading, desktopNow.AddMinutes(1));
Check(sourceHistory.Count == 2 && sourceHistory.All(s => s.Source == desktopReading.Source), "Repeated Desktop polls retain source attribution without duplicating unchanged samples");
var sourceTrend = UsageHistory.Trend([new("claude", "Sesión", 20, desktopNow, null, "Code"), new("claude", "Sesión", 21, desktopNow.AddMinutes(1), null, "Desktop")], "claude", "Sesión");
Check(sourceTrend.Count == 2, "Quota graphs separate samples from different Claude sources");
var icsEvent = new CalendarEvent(Guid.NewGuid().ToString("N"), "Reunión, equipo; \\ revisión\n" + new string('ñ', 75) + " ☀", DateTimeOffset.Parse("2026-11-16T09:00:00+01:00"), DateTimeOffset.Parse("2026-11-16T10:00:00+01:00"));
var icsText = ICalendar.Export([icsEvent]); var icsRoundtrip = ICalendar.Parse(icsText);
Check(icsRoundtrip.Events.Count == 1 && icsRoundtrip.Events[0].Id == icsEvent.Id && icsRoundtrip.Events[0].Title == icsEvent.Title && icsRoundtrip.Events[0].Start == icsEvent.Start, "iCalendar preserves UID, escaped Unicode text and UTC instants across export/import");
Check(icsText.Split("\r\n").All(line => System.Text.Encoding.UTF8.GetByteCount(line) <= 75), "iCalendar folds physical lines to at most 75 UTF-8 octets without splitting Unicode");
var calendarDate = new DateTimeOffset(DateTime.Today); var dayExport = ICalendar.Parse(ICalendar.Export([icsEvent with { Start = calendarDate, End = new DateTimeOffset(DateTime.Today.AddDays(1)), AllDay = true }]));
Check(dayExport.Events.Single().AllDay && dayExport.Events[0].End.LocalDateTime.Date == DateTime.Today.AddDays(1), "All-day iCalendar export preserves exclusive local end date");
var tzIcs = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:zone-fixture\r\nSUMMARY:Zone\r\nDTSTART;TZID=Europe/Madrid:20261116T090000\r\nDTEND;TZID=Europe/Madrid:20261116T100000\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
Check(ICalendar.Parse(tzIcs).Events.Single().Start.Offset == TimeSpan.FromHours(1), "iCalendar resolves an IANA timezone using the event date");
Check(ICalendar.Parse(tzIcs.Replace("Europe/Madrid", "Unknown/Zone")).Omitted == 1 && ICalendar.Parse(tzIcs.Replace("SUMMARY:Zone", "RRULE:FREQ=SECONDLY\r\nSUMMARY:Zone")).Events.Count == 0, "Unknown zones and unsupported sub-day recurrence rules are omitted rather than shifted or flattened");
var weeklyImport = ICalendar.Parse(tzIcs.Replace("SUMMARY:Zone", "RRULE:FREQ=WEEKLY;COUNT=3\r\nSUMMARY:Zone"));
var weeklyOccurrences = CalendarSchedule.Between(weeklyImport.Events, DateTimeOffset.Parse("2026-11-15T00:00:00Z"), DateTimeOffset.Parse("2026-12-10T00:00:00Z"));
Check(weeklyImport.Events.Count == 1 && weeklyImport.Events[0].RecurrenceIcs != null && weeklyOccurrences.Count == 3 && weeklyOccurrences[1].Start == weeklyOccurrences[0].Start.AddDays(7), "Weekly recurrence remains one stored series and expands only the requested interval");
var exceptionIcs = tzIcs.Replace("SUMMARY:Zone", "RRULE:FREQ=WEEKLY;COUNT=3\r\nEXDATE;TZID=Europe/Madrid:20261123T090000\r\nSUMMARY:Zone");
Check(CalendarSchedule.Between(ICalendar.Parse(exceptionIcs).Events, DateTimeOffset.Parse("2026-11-15T00:00:00Z"), DateTimeOffset.Parse("2026-12-10T00:00:00Z")).Count == 2, "Recurring imports honor excluded dates");
var dstIcs = tzIcs.Replace("20261116", "20261019").Replace("SUMMARY:Zone", "RRULE:FREQ=WEEKLY;COUNT=3\r\nSUMMARY:Zone");
var dstOccurrences = CalendarSchedule.Between(ICalendar.Parse(dstIcs).Events, DateTimeOffset.Parse("2026-10-18T00:00:00Z"), DateTimeOffset.Parse("2026-11-10T00:00:00Z"));
Check(dstOccurrences.Count == 3 && dstOccurrences[0].Start.UtcDateTime.Hour == 7 && dstOccurrences[1].Start.UtcDateTime.Hour == 8, "Recurring wall time survives the Madrid daylight-saving transition");
var monthly = CalendarSchedule.WithRule(icsEvent with { Start = DateTimeOffset.Parse("2026-01-31T09:00:00+01:00"), End = DateTimeOffset.Parse("2026-01-31T10:00:00+01:00") }, "FREQ=MONTHLY;COUNT=3");
var monthlyOccurrences = CalendarSchedule.Between([monthly], DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-06-01T00:00:00Z"));
Check(monthlyOccurrences.Count == 3 && monthlyOccurrences[1].Start.Month == 3 && monthlyOccurrences[2].Start.Month == 5, "Monthly series omit nonexistent month days without shifting appointments");
Check(CalendarSchedule.Between(ICalendar.Parse(ICalendar.Export(weeklyImport.Events)).Events, DateTimeOffset.Parse("2026-11-15T00:00:00Z"), DateTimeOffset.Parse("2026-12-10T00:00:00Z")).Count == 3, "Recurring series survive iCalendar export and reimport");
Check(weeklyOccurrences.Select(o => o.Key).Distinct().Count() == 3, "Calendar reminder identity is distinct for each occurrence of a series");
var overrideIcs = tzIcs.Replace("SUMMARY:Zone", "RRULE:FREQ=WEEKLY;COUNT=3\r\nSUMMARY:Zone").Replace("END:VCALENDAR", "BEGIN:VEVENT\r\nUID:zone-fixture\r\nRECURRENCE-ID;TZID=Europe/Madrid:20261123T090000\r\nSUMMARY:Moved\r\nDTSTART;TZID=Europe/Madrid:20261123T110000\r\nDTEND;TZID=Europe/Madrid:20261123T120000\r\nEND:VEVENT\r\nEND:VCALENDAR");
var overrides = CalendarSchedule.Between(ICalendar.Parse(overrideIcs).Events, DateTimeOffset.Parse("2026-11-15T00:00:00Z"), DateTimeOffset.Parse("2026-12-10T00:00:00Z"));
Check(overrides.Count == 3 && overrides.Count(o => o.Title == "Moved") == 1 && overrides.Single(o => o.Title == "Moved").Start.UtcDateTime.Hour == 10, "A recurrence override replaces its original occurrence without duplicating it");
var cancelIcs = overrideIcs.Replace("SUMMARY:Moved", "STATUS:CANCELLED\r\nSUMMARY:Moved");
Check(CalendarSchedule.Between(ICalendar.Parse(cancelIcs).Events, DateTimeOffset.Parse("2026-11-15T00:00:00Z"), DateTimeOffset.Parse("2026-12-10T00:00:00Z")).Count == 2, "Cancelled recurrence overrides do not appear in the calendar");
var recurringDay = CalendarSchedule.WithRule(icsEvent with { Start = new DateTimeOffset(DateTime.Today), End = new DateTimeOffset(DateTime.Today.AddDays(1)), AllDay = true }, "FREQ=DAILY;COUNT=2");
Check(CalendarSchedule.Between([recurringDay], new DateTimeOffset(DateTime.Today.AddDays(1)), new DateTimeOffset(DateTime.Today.AddDays(2))).Count == 1, "All-day recurring events retain exclusive local midnight boundaries");
var invalidSeries = recurringDay with { RecurrenceIcs = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\nUID:bad\r\nDTSTART;TZID=Invalid/Zone:20261117T090000\r\nRRULE:FREQ=DAILY\r\nEND:VEVENT\r\nEND:VCALENDAR" };
Check(CalendarSchedule.Between([invalidSeries], DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1)).Count == 0, "A damaged stored recurrence cannot crash the calendar view");
var mergedEvents = new List<CalendarEvent> { icsRoundtrip.Events[0] with { Archived = true, Reminder = true, ReminderSent = true } };
var selectedOccurrence = weeklyOccurrences[1];
var singleReplacement = selectedOccurrence.Series with { Title = "Only this appointment", Start = selectedOccurrence.Start.AddDays(2), End = selectedOccurrence.End.AddDays(2) };
var editedSeries = CalendarSchedule.ChangeOccurrence(selectedOccurrence, singleReplacement);
var editedOccurrences = CalendarSchedule.Between([editedSeries], weeklyOccurrences[0].Start.AddDays(-1), weeklyOccurrences[^1].End.AddDays(4));
Check(editedOccurrences.Count == 3 && editedOccurrences.Count(o => o.Title == singleReplacement.Title) == 1 && editedOccurrences.Single(o => o.Title == singleReplacement.Title).Start == singleReplacement.Start && editedOccurrences.Count(o => o.Title == "Zone") == 2, "Editing one appointment moves only that occurrence and preserves the rest of the series");
var movedOccurrence = editedOccurrences.Single(o => o.Title == singleReplacement.Title);
var editedTwice = CalendarSchedule.ChangeOccurrence(movedOccurrence, singleReplacement with { Start = singleReplacement.Start.AddHours(1), End = singleReplacement.End.AddHours(1) });
Check(CalendarSchedule.Exceptions(editedTwice).Count == 1 && CalendarSchedule.Exceptions(editedTwice)[0].OriginalStart == selectedOccurrence.Start, "Repeated edits replace the same original recurrence identity instead of adding duplicates");
var omittedSeries = CalendarSchedule.ChangeOccurrence(movedOccurrence, null);
Check(CalendarSchedule.Between([omittedSeries], weeklyOccurrences[0].Start.AddDays(-1), weeklyOccurrences[^1].End.AddDays(4)).Count == 2 && CalendarSchedule.Exceptions(omittedSeries).Single().Cancelled, "Omitting a moved appointment cancels its original occurrence without altering the series");
var restoredSeries = CalendarSchedule.RestoreOccurrence(omittedSeries, selectedOccurrence.Start);
Check(CalendarSchedule.Exceptions(restoredSeries).Count == 0 && CalendarSchedule.Between([restoredSeries], weeklyOccurrences[0].Start.AddDays(-1), weeklyOccurrences[^1].End.AddDays(4)).Select(o => o.Start).SequenceEqual(weeklyOccurrences.Select(o => o.Start)), "Restoring an omitted appointment recovers its original date and removes the exception");
var editedRoundtrip = ICalendar.Parse(ICalendar.Export([editedTwice]));
Check(CalendarSchedule.Exceptions(editedRoundtrip.Events.Single()).Count == 1 && CalendarSchedule.Between(editedRoundtrip.Events, weeklyOccurrences[0].Start.AddDays(-1), weeklyOccurrences[^1].End.AddDays(4)).Count == 3, "Individual appointment edits survive iCalendar export and reimport");
var otherOccurrence = editedOccurrences.First(o => o.Title == "Zone") with { Series = editedSeries };
var twoExceptions = CalendarSchedule.ChangeOccurrence(otherOccurrence, null);
var oneExceptionRestored = CalendarSchedule.RestoreOccurrence(twoExceptions, selectedOccurrence.Start);
Check(CalendarSchedule.Exceptions(oneExceptionRestored).Count == 1 && CalendarSchedule.Exceptions(oneExceptionRestored).Single().Cancelled && CalendarSchedule.Between([oneExceptionRestored], weeklyOccurrences[0].Start.AddDays(-1), weeklyOccurrences[^1].End.AddDays(4)).Count == 2, "Restoring one exception preserves the master rule and all other exceptions");
var dayOccurrence = CalendarSchedule.Between([recurringDay], calendarDate.AddDays(-1), calendarDate.AddDays(3)).First();
var omittedDay = CalendarSchedule.ChangeOccurrence(dayOccurrence, null);
Check(CalendarSchedule.Between([omittedDay], calendarDate.AddDays(-1), calendarDate.AddDays(3)).Count == 1 && CalendarSchedule.Exceptions(omittedDay).Single().OriginalStart == dayOccurrence.Start, "All-day appointment omission uses the original date rather than a UTC-shifted day");
var merged = ICalendar.Merge(mergedEvents, icsRoundtrip.Events); Check(merged.Added == 0 && merged.Updated == 1 && mergedEvents.Count == 1 && mergedEvents[0].Archived && mergedEvents[0].Reminder && mergedEvents[0].ReminderSent, "Repeated calendar imports update by UID while preserving archive and reminder preferences");
try { ICalendar.Parse("BEGIN:VCALENDAR\r\nBEGIN:VEVENT"); Check(false, "Truncated calendar must fail"); } catch (InvalidDataException) { Check(true, "Truncated iCalendar input is rejected before applying events"); }
var fullCalendar = Enumerable.Range(0, 1000).Select(i => icsEvent with { Id = "existing-" + i }).ToList();
try { ICalendar.Merge(fullCalendar, [icsEvent]); Check(false, "Full calendar must reject additions"); } catch (InvalidDataException) { Check(fullCalendar.Count == 1000, "Calendar capacity rejection leaves existing events untouched"); }
var backupCurrent = new DockSettings { CodexEnabled = false, CodexExecutable = "trusted.exe" };
var backupIncoming = new DockSettings { Widgets = ["notes", "unknown", "notes"], CodexEnabled = true, CodexExecutable = "other.exe", Notes = [new("backup-note", "Preserve text", DateTimeOffset.UtcNow)], Events = [icsEvent] };
var backupParsed = SettingsBackup.Read(SettingsBackup.Export(backupIncoming));
Check(backupParsed.Settings.Widgets.SequenceEqual(new[] { "notes" }) && backupParsed.Settings.Notes.Single().Text == "Preserve text" && backupParsed.Settings.Events.Count == 1, "Backup validates and normalizes widgets while preserving notes and calendar data");
SettingsBackup.Apply(backupCurrent, backupParsed.Settings); backupParsed.Settings.Notes.Clear();
Check(!backupCurrent.CodexEnabled && backupCurrent.CodexExecutable == "trusted.exe" && backupCurrent.Notes.Count == 1, "Backup restore preserves local connector settings and clones mutable collections");
try { SettingsBackup.Read("{\"Format\":\"Other\",\"Version\":1,\"Settings\":{}}"); Check(false, "Foreign backup must fail"); } catch (InvalidDataException) { Check(true, "Unrecognized backup format is rejected"); }
try { SettingsBackup.Read(new string(' ', SettingsBackup.MaxBytes + 1)); Check(false, "Oversize backup must fail"); } catch (InvalidDataException) { Check(true, "Oversize backup is rejected before parsing"); }
var hosted = RunningApps.Group([new(10, "App A", "a.exe", "A", "ApplicationFrameHost.exe"), new(11, "App B", "b.exe", "B", "ApplicationFrameHost.exe")]);
Check(hosted.Count == 2 && hosted.All(a => a.Windows[0].HostExecutable == "ApplicationFrameHost.exe"), "Store applications hosted by one process keep distinct identities and validation hosts");
var localDay = DateTime.Today; var localEvent = new CalendarEvent("fixture", "Event", new DateTimeOffset(localDay.AddHours(9)), new DateTimeOffset(localDay.AddHours(10)), Reminder: true);
var trendAt = DateTimeOffset.UtcNow;
QuotaSample[] trendFixture = [new("claude", "weekly", 10, trendAt, trendAt.AddDays(1)), new("claude", "weekly", 20, trendAt.AddMinutes(1), trendAt.AddDays(1)), new("claude", "weekly", 5, trendAt.AddMinutes(2), trendAt.AddDays(8)), new("claude", "weekly", double.NaN, trendAt.AddMinutes(3), null), new("codex", "weekly", 90, trendAt, null)];
var trendSegments = UsageHistory.Trend(trendFixture.Reverse(), "claude", "weekly");
Check(trendSegments.Count == 2 && trendSegments[0].Count == 2 && trendSegments[1].Single().UsedPercent == 5, "Quota trend orders real readings and splits at reset changes without mixing providers or invalid values");
Check(UsageHistory.Trend([], "claude", "weekly").Count == 0 && UsageHistory.Trend(trendFixture.Take(1), "claude", "weekly").Single().Count == 1, "Empty and single-reading quota trends preserve missing data without manufacturing a curve");
Check(UsageHistory.Trend(Enumerable.Range(0, 100).Select(i => new QuotaSample("claude", "weekly", i, trendAt.AddMinutes(i), null)), "claude", "weekly").SelectMany(s => s).Count() == 80, "Quota chart bounds retained points while preserving chronological order");
Check(CalendarEvents.OccursOn(localEvent, localDay) && !CalendarEvents.OccursOn(localEvent, localDay.AddDays(1)), "Calendar events occur only on overlapping local dates");
var allDayEvent = localEvent with { Start = new DateTimeOffset(localDay), End = new DateTimeOffset(localDay.AddDays(1)), AllDay = true };
Check(CalendarEvents.OccursOn(allDayEvent, localDay) && !CalendarEvents.OccursOn(allDayEvent, localDay.AddDays(1)), "All-day event end is exclusive at midnight");
Check(CalendarEvents.ReminderDue(localEvent, localEvent.Start.AddMinutes(-15)) && !CalendarEvents.ReminderDue(localEvent, localEvent.Start.AddMinutes(-16)) && !CalendarEvents.ReminderDue(localEvent with { ReminderSent = true }, localEvent.Start) && !CalendarEvents.ReminderDue(localEvent, localEvent.End), "Calendar reminders respect lead time, sent state and event expiry");
Check(!CalendarEvents.OccursOn(localEvent with { Archived = true }, localDay) && CalendarEvents.Normalize([localEvent, localEvent, localEvent with { Id = "invalid", End = localEvent.Start }]).Count == 1, "Calendar archive and normalization preserve valid unique events");
var history = new List<QuotaSample>(); var historyTime = DateTimeOffset.UtcNow;
var realFixture = new UsageSnapshot("claude", ConnectionState.Connected, "", "fixture", historyTime, [new("5 h", 80, historyTime.AddHours(2))]);
Check(UsageHistory.Append(history, realFixture, historyTime) && !UsageHistory.Append(history, realFixture, historyTime.AddMinutes(2)) && history.Count == 1, "Quota history avoids duplicate unchanged readings");
Check(!UsageHistory.Append(history, realFixture with { State = ConnectionState.Error }, historyTime) && !UsageHistory.Append(history, realFixture, historyTime.AddMinutes(20)), "Quota history never records disconnected or stale readings as fresh");
Check(UsageHistory.AlertKey("claude", realFixture.Windows[0]) != UsageHistory.AlertKey("claude", realFixture.Windows[0] with { ResetsAt = historyTime.AddHours(3) }), "Quota alert identity changes at a new reset window");
var layoutSettings = new DockSettings { Apps = [new("a", "A", "a.exe"), new("b", "B", "b.exe"), new("c", "C", "c.exe")], AppGroups = [new("g", "Group", ["b"])], PinnedOrder = ["app:a", "group:g", "app:b", "app:c", "app:a", "missing"] };
layoutSettings.Normalize();
Check(PinnedLayout.Visible(layoutSettings).SequenceEqual(new[] { "app:a", "group:g", "app:c" }), "Unified pinned layout removes invalid references and hides grouped members without losing them");
Check(PinnedLayout.Move(layoutSettings, "group:g", 1) && PinnedLayout.Visible(layoutSettings).SequenceEqual(new[] { "app:a", "app:c", "group:g" }) && layoutSettings.PinnedOrder.Contains("app:b"), "Groups move between shortcuts while retaining hidden member positions");
layoutSettings.AppGroups.Clear(); Check(PinnedLayout.Visible(layoutSettings).Contains("app:b") && !PinnedLayout.Move(layoutSettings, "missing", 1), "Dissolving a group restores every shortcut and invalid moves are ignored");
var cachePlace = new WeatherLocation("Fixture", "Test", 1, 2);
var cachedSettings = new DockSettings { WeatherLocation = cachePlace, WeatherCache = new(cachePlace, new(12, 8, 15, 0, true, DateTimeOffset.UnixEpoch)) };
var cachedRestored = JsonSerializer.Deserialize<DockSettings>(JsonSerializer.Serialize(cachedSettings))!; cachedRestored.Normalize();
Check(cachedRestored.WeatherCache?.Reading.Temperature == 12, "Last weather reading survives restart with its original timestamp");
cachedRestored.WeatherLocation = cachePlace with { Latitude = 3 }; cachedRestored.Normalize(); Check(cachedRestored.WeatherCache == null, "Weather cache from a different city is discarded");
var dualMonitors = new MonitorChoice[] { new("secondary", false), new("primary", true) };
Check(MonitorSelection.Resolve(dualMonitors, "SECONDARY") == "secondary", "Preferred monitor is selected by stable identity, independent of enumeration order");
string savedMonitor = "secondary";
Check(MonitorSelection.Resolve([new("primary", true)], savedMonitor) == "primary" && MonitorSelection.Resolve(dualMonitors, savedMonitor) == "secondary", "Disconnected monitor falls back temporarily and restores on reconnection");
Check(MonitorSelection.Resolve(dualMonitors, null) == "primary" && MonitorSelection.Resolve([], savedMonitor) == null, "Default follows the primary display and empty topology is safe");
var monitorRoundtrip = JsonSerializer.Deserialize<DockSettings>(JsonSerializer.Serialize(new DockSettings { MonitorId = "monitor-interface-id" }))!;
monitorRoundtrip.Normalize(); Check(monitorRoundtrip.MonitorId == "monitor-interface-id", "Monitor preference survives settings roundtrip and normalization");
Check(RunningApps.Matches("explorer.exe", @"C:\Windows\EXPLORER.EXE"), "Bare executable matches case-insensitive process name");
Check(RunningApps.Matches(@"C:\Apps\Tool.exe", "c:/apps/tool.exe") && !RunningApps.Matches(@"D:\Other\Tool.exe", @"C:\Apps\Tool.exe"), "Absolute executable identity does not conflate same-name programs");
Check(!RunningApps.Matches("https://bing.com", @"C:\Chrome\chrome.exe") && !RunningApps.Matches(@"C:\Apps\Tool.lnk", @"C:\Apps\Tool.exe"), "URLs and shortcuts never guess the owning running app");
var groupedApps = RunningApps.Group([new(2, "Second", @"C:\Apps\Tool.exe", "Tool"), new(1, "First", "c:/apps/tool.exe", "Tool"), new(1, "Duplicate", @"C:\Apps\Tool.exe", "Tool"), new(0, "Invalid", "x.exe", "x"), new(3, "", "x.exe", "x")]);
Check(groupedApps.Count == 1 && groupedApps[0].Windows.Select(w => w.Handle).SequenceEqual(new long[] { 1, 2 }), "Running windows grouped by executable with stable ordering and duplicate filtering");
var groupedSettings = new DockSettings { Apps = [new("a", "App A", "a.exe"), new("b", "App B", "b.exe")], AppGroups = [new("work", " Work ", ["a", "missing", "a"], "wrong"), new("play", "Play", ["a", "b"]), new("work", "Duplicate", ["b"])] };
groupedSettings.Normalize();
Check(groupedSettings.AppGroups.Count == 2 && groupedSettings.AppGroups[0].Name == "Work" && groupedSettings.AppGroups[0].Accent == "mint" && groupedSettings.AppGroups[0].AppIds.SequenceEqual(new[] { "a" }) && groupedSettings.AppGroups[1].AppIds.SequenceEqual(new[] { "b" }), "Groups normalize duplicate identities, missing shortcuts and exclusive membership");
Check(!AppGroups.Assign(groupedSettings, "a", "missing") && groupedSettings.AppGroups[0].AppIds.Contains("a"), "Invalid group assignment preserves existing membership");
Check(AppGroups.Assign(groupedSettings, "a", "play") && groupedSettings.AppGroups[0].AppIds.Count == 0 && groupedSettings.AppGroups[1].AppIds.SequenceEqual(new[] { "b", "a" }), "Moving a shortcut transfers membership without losing pinned app");
var roundtripGroups = JsonSerializer.Deserialize<DockSettings>(JsonSerializer.Serialize(groupedSettings))!; roundtripGroups.Normalize();
Check(roundtripGroups.AppGroups[1].AppIds.SequenceEqual(new[] { "b", "a" }) && roundtripGroups.Apps.Count == 2, "Group names, order and members survive settings roundtrip");
AppGroups.RemoveShortcut(groupedSettings, "a");
Check(groupedSettings.Apps.Count == 1 && groupedSettings.AppGroups.All(g => !g.AppIds.Contains("a")), "Unpinning removes group references without affecting other shortcuts");
roundtripGroups.AppGroups.Clear(); Check(roundtripGroups.Apps.Count == 2, "Dissolving groups preserves all pinned shortcuts");
Check(AppGroups.Normalize([new("empty", "", null!, null!)], []).Single() is { Name: "Grupo", Accent: "mint", AppIds.Count: 0 }, "Malformed empty group data receives safe defaults");
Check(WindowPreviewLayout.Fit(1920, 1080, new(10, 10, 300, 150), new(0, 0, 800, 600)) == new PreviewRect(26, 10, 267, 150), "Window preview preserves aspect ratio and centers inside slot");
Check(WindowPreviewLayout.Fit(100, 200, new(0, 0, 300, 150), new(0, 0, 800, 600)) == new PreviewRect(112, 0, 75, 150), "Portrait window preview fits without distortion");
Check(WindowPreviewLayout.Fit(1920, 1080, new(10, -40, 300, 150), new(0, 0, 800, 600)) == null && WindowPreviewLayout.Fit(0, 1080, new(0, 0, 100, 100), new(0, 0, 800, 600)) == null, "Native previews stay hidden outside viewport or for invalid source size");
Check(StartupCommand.Build(@"C:\My Apps\BrisaDock.exe") == "\"C:\\My Apps\\BrisaDock.exe\"", "Startup executable path is quoted without shell expansion");
try { StartupCommand.Build("C:\\Bad\"name.exe"); Check(false, "Invalid startup path must fail"); } catch (ArgumentException) { Check(true, "Startup rejects embedded quotes"); }
try { StartupCommand.Build("C:\\" + new string('a', 260) + ".exe"); Check(false, "Overlong startup path must fail"); } catch (ArgumentException) { Check(true, "Startup rejects command beyond Windows Run limit"); }
var focusTest = new FocusSession(new(1, 1, 2, 2)); var focusTime = DateTimeOffset.FromUnixTimeSeconds(2000);
focusTest.Toggle(focusTime); Check(focusTest.Tick(focusTime.AddSeconds(60)) && focusTest.Phase == FocusPhase.ShortBreak && focusTest.Completed == 1 && !focusTest.Running, "Focus work completion enters a paused short break");
focusTest.Toggle(focusTime.AddSeconds(60)); focusTest.Tick(focusTime.AddSeconds(120)); focusTest.Toggle(focusTime.AddSeconds(120)); focusTest.Tick(focusTime.AddSeconds(180));
Check(focusTest.Phase == FocusPhase.LongBreak && focusTest.Completed == 2 && focusTest.Remaining(focusTime.AddSeconds(180)).TotalMinutes == 2, "Focus long break follows configured completed rounds");
var automaticFocus = new FocusSession(new(1, 1, 2, 2, true)); automaticFocus.Toggle(focusTime); automaticFocus.Tick(focusTime.AddSeconds(60));
Check(automaticFocus.Running && !automaticFocus.Tick(focusTime.AddSeconds(60)), "Automatic focus transition starts next phase exactly once");
var resumedFocus = new FocusSession(automaticFocus.Options); resumedFocus.Restore(automaticFocus.Snapshot(focusTime.AddSeconds(70)));
Check(!resumedFocus.Running && resumedFocus.Phase == FocusPhase.ShortBreak && resumedFocus.Remaining(focusTime.AddHours(3)).TotalSeconds == 50, "Focus checkpoint restores paused without counting app-closed time");
Check(AlertPolicy.IsQuiet(new(2026, 11, 15, 23, 0, 0), true, 22, 8) && AlertPolicy.IsQuiet(new(2026, 11, 15, 7, 59, 0), true, 22, 8) && !AlertPolicy.IsQuiet(new(2026, 11, 15, 8, 0, 0), true, 22, 8) && !AlertPolicy.IsQuiet(DateTime.Now, false, 22, 8), "Quiet hours handle overnight boundaries and disabled state");
var migratedNotes = new DockSettings { Note = "My existing note" }; migratedNotes.Normalize(); migratedNotes.Normalize();
Check(migratedNotes.Notes.Count == 1 && migratedNotes.Notes[0].Text == "My existing note", "Legacy note migrates once without duplication or text loss");
var archivedNotes = new DockSettings { Notes = [new("one", "Keep me", focusTime, true)], Note = "old" }; archivedNotes.Normalize();
Check(archivedNotes.Notes.Count == 1 && archivedNotes.Notes[0].Archived && archivedNotes.Notes[0].Text == "Keep me", "Archived notes retain content and do not recreate legacy copies");
Check(NoteText.Normalize("First\rSecond\r\nThird\nFourth") == "First\nSecond\nThird\nFourth", "Notes preserve every line across Windows CR and CRLF representations");
Check(NoteText.Title("\r\nFirst line\rSecond line") == "First line" && NoteText.Title(new string('a', 100)).Length == 60, "Note titles use first nonempty line and fit collection controls");
var orderTest = new List<string> { "apps", "clock", "cpu", "notes" };
Check(WidgetOrder.MoveRelative(orderTest, "apps", "cpu", true) && orderTest.SequenceEqual(new[] { "clock", "cpu", "apps", "notes" }), "Dragging a widget forward inserts after target without offset error");
Check(WidgetOrder.MoveRelative(orderTest, "notes", "clock", false) && orderTest.SequenceEqual(new[] { "notes", "clock", "cpu", "apps" }), "Dragging a widget backward inserts before target");
var orderBeforeInvalid = orderTest.ToArray();
Check(!WidgetOrder.Apply(orderTest, ["notes", "clock", "cpu", "cpu"]) && !WidgetOrder.MoveRelative(orderTest, "foreign", "clock", true) && !WidgetOrder.MoveTo(orderTest, "notes", -1) && orderTest.SequenceEqual(orderBeforeInvalid), "Invalid or partial reorder never removes or duplicates widgets");
Check(WidgetOrder.InsertAfter(orderTest, "timer", null) && orderTest[0] == "timer" && WidgetOrder.InsertAfter(orderTest, "battery", "clock") && orderTest.IndexOf("battery") == orderTest.IndexOf("clock") + 1 && !WidgetOrder.InsertAfter(orderTest, "timer", "apps"), "New widgets can be inserted at start or chosen anchor without duplicates");
Check(WidgetOrder.Apply(orderTest, orderTest.AsEnumerable().Reverse()) && orderTest.SequenceEqual(new[] { "apps", "cpu", "battery", "clock", "notes", "timer" }), "Organizer applies complete permutation while preserving every widget");
var baseline = new DockVisibilityContext(true, true, false, false, false, false, false, false, false);
Check(!DockVisibilityPolicy.ShouldReveal(baseline), "Auto-hide hides an unattended dock");
Check(DockVisibilityPolicy.ShouldReveal(baseline with { PointerAtEdge = true }), "Screen edge reveals dock");
Check(DockVisibilityPolicy.ShouldReveal(baseline with { PointerOverDock = true }), "Pointer over dock preserves interaction");
Check(DockVisibilityPolicy.ShouldReveal(baseline with { PanelOpen = true }), "Open panel holds dock visible");
Check(DockVisibilityPolicy.ShouldReveal(baseline with { KeepOnDesktop = true, DesktopActive = true }), "Optional desktop visibility");
Check(!DockVisibilityPolicy.ShouldReveal(baseline with { Fullscreen = true, PointerAtEdge = true }), "Fullscreen guard overrides edge reveal");
Check(DockVisibilityPolicy.ShouldReveal(baseline with { AutoHide = false }), "Disabling auto-hide always reveals dock");
Check(Math.Abs(CalendarProgress.Year(new DateTime(2024, 7, 2)) - 50) < .001, "Year progress includes leap day");
Check(CalendarProgress.Day(new DateTime(2026, 11, 15, 12, 0, 0)) == 50 && CalendarProgress.Month(new DateTime(2026, 11, 1)) == 0, "Day and month boundaries");
Check(CalendarProgress.GridStart(new DateTime(2026, 11, 15)) == new DateTime(2026, 10, 26) && CalendarProgress.GridStart(new DateTime(2026, 11, 15)).AddDays(6) == new DateTime(2026, 11, 1), "November 2026 starts in Sunday column of Monday-first calendar");
Check(CalendarProgress.GridStart(new DateTime(2024, 2, 1)).DayOfWeek == DayOfWeek.Monday && Enumerable.Range(0, 42).Select(i => CalendarProgress.GridStart(new DateTime(2024, 2, 1)).AddDays(i)).Count(d => d.Month == 2) == 29, "Calendar grid includes every leap-February day");
var countdown = new Countdown(); var start = DateTimeOffset.FromUnixTimeSeconds(1000); countdown.Set(TimeSpan.FromSeconds(10)); countdown.Start(start); countdown.Pause(start.AddSeconds(3));
Check(countdown.Remaining(start.AddHours(1)).TotalSeconds == 7 && !countdown.Running, "Timer pause freezes remaining duration");
countdown.Start(start.AddHours(1)); Check(countdown.CompleteIfDue(start.AddHours(1).AddSeconds(8)) && countdown.Remaining(start.AddHours(2)) == TimeSpan.Zero && !countdown.CompleteIfDue(start.AddHours(3)), "Timer completes once after deadline");
var networkMeter = new NetworkMeter(); networkMeter.Sample(1000, 500, start); var networkRate = networkMeter.Sample(3000, 1500, start.AddSeconds(2));
Check(networkRate.Down == 1000 && networkRate.Up == 500, "Network rate is byte deltas divided by elapsed time");
Check(networkMeter.Sample(0, 0, start.AddSeconds(3)).Down == 0, "Adapter counter reset never creates negative network speed");
var oldPreferences = JsonSerializer.Deserialize<DockSettings>("""{"Version":2,"Widgets":["clock"],"Note":"existing"}""")!;
oldPreferences.Normalize();
Check(oldPreferences.Note == "existing" && !oldPreferences.AutoHide && oldPreferences.HideInFullscreen && oldPreferences.TimerSeconds == 300, "Older settings retain content and receive safe defaults");
var invalidPreferences = new DockSettings { WidgetSpacing = double.NaN, WidthPercent = 42, EdgeDistance = -500, Magnification = double.PositiveInfinity, WorldTimeZone = "not-a-zone", TimerPresets = [0, 300, 300, 400000] };
invalidPreferences.Normalize();
Check(invalidPreferences.WidgetSpacing == 4 && invalidPreferences.WidthPercent == 0 && invalidPreferences.EdgeDistance == 0 && invalidPreferences.Magnification == 1.18 && invalidPreferences.WorldTimeZone == "UTC" && invalidPreferences.TimerPresets.SequenceEqual(new[] { 300 }), "Normalize malformed layout, timezone and timer preferences");
UsageSnapshot Codex(string text) { using var json = JsonDocument.Parse(text); return UsageParser.Codex(json.RootElement, DateTimeOffset.FromUnixTimeSeconds(1000)); }
UsageSnapshot Claude(string text) { using var json = JsonDocument.Parse(text); return UsageParser.ClaudeStatusLine(json.RootElement, DateTimeOffset.FromUnixTimeSeconds(1000)); }

var quota = Codex("""{"rateLimits":{"primary":{"usedPercent":23,"windowDurationMins":300,"resetsAt":2000},"secondary":{"usedPercent":61,"windowDurationMins":10080,"resetsAt":5000}}}""");
Check(quota.State == ConnectionState.Connected && quota.Windows.Count == 2 && quota.Windows[0].UsedPercent == 23 && quota.Windows[1].Label == "Semana", "Codex windows and percentages");
Check(quota.WithFreshness(DateTimeOffset.FromUnixTimeSeconds(2001)).State == ConnectionState.Stale, "Expired quota is stale, never reset to zero");
Check(quota.WithFreshness(DateTimeOffset.FromUnixTimeSeconds(1950)).State == ConnectionState.Stale, "Old readings are marked stale");
var multi = Codex("""{"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":42,"windowDurationMins":60,"resetsAt":9999}}},"rateLimits":{"primary":{"usedPercent":5}}}""");
Check(multi.Windows[0].UsedPercent == 42, "Prefer Codex named bucket over legacy view");
Check(Codex("""{"rateLimits":null}""").State == ConnectionState.Unavailable, "Null quota is unavailable");
Check(Codex("""{"rateLimits":{"primary":{"usedPercent":null},"secondary":{"usedPercent":101}}}""").Windows.Count == 0, "Reject invalid percentages");
var claude = Claude("""{"rate_limits":{"five_hour":{"used_percentage":12.5,"resets_at":3000},"seven_day":{"used_percentage":76,"resets_at":5000}},"session_id":"private","transcript_path":"private"}""");
Check(claude.Windows.Count == 2 && claude.Windows[0].UsedPercent == 12.5 && claude.Source.Contains("statusLine"), "Claude official statusLine fields");
Check(!JsonSerializer.Serialize(claude).Contains("private"), "Snapshot excludes session and transcript metadata");
Check(Claude("""{"rate_limits":{"five_hour":null,"seven_day":{"used_percentage":0,"resets_at":null}}}""").Windows.Count == 1, "Independent Claude windows and unknown reset");
Check(Claude("{}").State == ConnectionState.Unavailable, "Missing data is unavailable, not zero usage");
Check(Claude("null").State == ConnectionState.Unavailable && Codex("[]").State == ConnectionState.Unavailable, "Unexpected JSON shapes do not crash parsers");
using (var weatherJson = JsonDocument.Parse("""{"current":{"temperature_2m":18.4,"weather_code":2,"is_day":1},"daily":{"temperature_2m_max":[21.5],"temperature_2m_min":[11.2]}}"""))
{
    var reading = OpenMeteoWeather.Parse(weatherJson.RootElement, DateTimeOffset.Now);
    Check(reading.Temperature == 18.4 && reading.Low == 11.2 && reading.High == 21.5 && reading.Description == "Parcialmente nublado", "Weather current and daily fields with WMO description");
}
using (var weatherJson = JsonDocument.Parse("""{"current":{"temperature_2m":-2,"weather_code":71},"daily":{"temperature_2m_min":[null]}}"""))
    Check(OpenMeteoWeather.Parse(weatherJson.RootElement, DateTimeOffset.Now).Low == null, "Missing forecast data remains unknown");
try { using var invalid = JsonDocument.Parse("{}"); OpenMeteoWeather.Parse(invalid.RootElement, DateTimeOffset.Now); Check(false, "Missing temperature should fail"); }
catch (JsonException) { Check(true, "Weather does not manufacture temperature when unavailable"); }
var settings = new DockSettings { Widgets = ["claude", "claude", "future-widget", "notes"], Theme = "invalid", Scale = 99, Tasks = [new("one", "Tarea ñ")], Note = "Nota €" };
settings.Normalize();
Check(settings.Widgets.SequenceEqual(new[] { "claude", "notes" }) && settings.Theme == "glass" && settings.Scale == 1.25, "Normalize unknown widgets, duplicates and preferences");
string directory = Path.Combine(Path.GetTempPath(), "BrisaChecks-" + Guid.NewGuid().ToString("N"));
var store = new SettingsStore(directory); store.Save(settings); var restored = store.Load();
Check(restored.Note == "Nota €" && restored.Tasks[0].Text == "Tarea ñ" && restored.Widgets.SequenceEqual(settings.Widgets), "Settings roundtrip retains content and widget order");
var folderFixture = Path.Combine(directory, "folder-preview"); Directory.CreateDirectory(Path.Combine(folderFixture, "Z Carpeta")); File.WriteAllText(Path.Combine(folderFixture, "A archivo.txt"), "fixture"); File.WriteAllText(Path.Combine(folderFixture, "B archivo.txt"), "fixture");
var folderListing = FolderBrowser.Read(folderFixture);
Check(folderListing.Entries.Count == 3 && folderListing.Entries[0].Directory && folderListing.Entries[1].Name == "A archivo.txt", "Folder preview keeps directories first and sorts file names without changing files");
Check(FolderBrowser.Read(folderFixture, 1) is { Truncated: true, Entries.Count: 1 }, "Folder preview bounds large listings and reports a partial view");
Check(FolderBrowser.Read(Path.Combine(folderFixture, "Z Carpeta")).Entries.Count == 0, "An empty folder has an explicit empty listing");
var folderSettings = new DockSettings { Apps = [new("folder", "Mi carpeta", folderFixture, Folder: true)] };
Check(SettingsBackup.Read(SettingsBackup.Export(folderSettings)).Settings.Apps.Single().Folder, "Portable backup retains the folder shortcut type");
Check(JsonSerializer.Deserialize<AppShortcut>("{\"Id\":\"old\",\"Name\":\"Anterior\",\"Target\":\"test.exe\"}") is { Folder: false }, "Existing application shortcuts remain applications when the folder field is absent");
var recoveryDirectory = Path.Combine(directory, "recovery"); var recoveryStore = new SettingsStore(recoveryDirectory);
recoveryStore.Save(new DockSettings { Note = "Keep this", MonitorId = "preferred-screen" }); recoveryStore.Save(new DockSettings { Note = "Second version" });
File.WriteAllText(Path.Combine(recoveryDirectory, "dock.json"), "damaged"); var recovered = recoveryStore.Load();
Check(recovered.Note == "Keep this" && recovered.MonitorId == "preferred-screen" && recoveryStore.Warning != null, "Corrupt primary settings recover the previous valid data and monitor preference");
recoveryStore.Save(recovered); Check(File.ReadAllText(Directory.GetFiles(recoveryDirectory, "*.backup-*").Single()) == "damaged" && recoveryStore.Load().Note == "Keep this", "Recovery preserves the damaged original and writes valid settings atomically");
File.WriteAllText(Path.Combine(directory, "dock.json"), "broken-json"); restored = store.Load();
Check(store.Warning != null && restored.Version == 2, "Corrupt settings handled explicitly");
store.Save(restored); Check(Directory.GetFiles(directory, "*.backup-*").Length == 1, "Corrupt settings preserved before replacement");
var empty = await new ClaudeFileProvider(directory).RefreshAsync(CancellationToken.None);
var transactionalStore = new SettingsStore(Path.Combine(directory, "restore-transaction"));
var transactionalCurrent = new DockSettings { Note = "Original state", ClaudeSource = "desktop", ClaudeDesktopDirect = true, CodexEnabled = true, MonitorId = "saved-screen", Tasks = [new("keep", "Original task")] };
transactionalCurrent.Normalize(); transactionalStore.Save(transactionalCurrent);
var transactionalIncoming = new DockSettings { Note = "Incoming state", ClaudeSource = "off", ClaudeDesktopDirect = false, CodexEnabled = false, Tasks = [new("restored", "Restored task")] };
transactionalIncoming.Normalize();
if (OperatingSystem.IsWindows())
{
    using (var heldConfig = new FileStream(Path.Combine(transactionalStore.DirectoryPath, "dock.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        try { SettingsBackup.RestoreToStore(transactionalStore, transactionalCurrent, transactionalIncoming); Check(false, "Locked restore commit should fail"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Check(transactionalCurrent.Note == "Original state" && transactionalCurrent.Tasks.Single().Id == "keep", "A failed backup commit cannot replace the live task and note collections"); }
    }
    Check(transactionalStore.Load().Note == "Original state", "A failed backup commit leaves the previous settings file intact");
}
string recoveryCopy = SettingsBackup.RestoreToStore(transactionalStore, transactionalCurrent, transactionalIncoming);
Check(transactionalCurrent.Note == "Incoming state" && transactionalStore.Load().Tasks.Single().Id == "restored" && transactionalCurrent.ClaudeDesktopDirect && transactionalCurrent.CodexEnabled, "Backup commit updates disk and live data together while preserving local account connections");
var beforeRestore = SettingsBackup.Read(File.ReadAllText(recoveryCopy));
Check(beforeRestore.Settings.Note == "Original state" && beforeRestore.Settings.Tasks.Single().Id == "keep", "Restore creates a readable recovery copy before replacing the settings file");
SettingsBackup.RestoreToStore(transactionalStore, transactionalCurrent, beforeRestore.Settings);
Check(transactionalStore.Load().Note == "Original state" && transactionalCurrent.Tasks.Single().Id == "keep", "The automatically saved pre-restore copy can undo a restoration");
Check(empty.State == ConnectionState.Disconnected, "Missing Claude bridge file stays disconnected");
var desktopPath = Path.Combine(directory, "plan-usage-history.json");
Check((await new ClaudeDesktopProvider(desktopPath).RefreshAsync(CancellationToken.None)).Windows.Count == 0, "Missing Desktop history is handled without reading any other session files");
await File.WriteAllTextAsync(desktopPath, "{broken");
Check((await new ClaudeDesktopProvider(desktopPath).RefreshAsync(CancellationToken.None)).State == ConnectionState.Error, "A partial Desktop history write returns a recoverable read error");
await File.WriteAllTextAsync(desktopPath, DesktopFixture([desktopSample]));
Check((await new ClaudeDesktopProvider(desktopPath).RefreshAsync(CancellationToken.None)).Windows.Count == 2, "Desktop reader recovers after a partial history write is completed");
await File.WriteAllTextAsync(desktopPath, new string(' ', ClaudeDesktopProvider.MaxBytes + 1));
Check((await new ClaudeDesktopProvider(desktopPath).RefreshAsync(CancellationToken.None)).Windows.Count == 0, "Desktop reader bounds file size before parsing");
if (OperatingSystem.IsWindows())
{
    var throughProtocol = await new CodexUsageProvider(Environment.ProcessPath).RefreshAsync(CancellationToken.None);
    Check(throughProtocol.State == ConnectionState.Connected && throughProtocol.Windows[0].UsedPercent == 37, "Codex stdio initialize/initialized/account read protocol against isolated fake process");
}

Check(AppTargets.AppId(@"shell:AppsFolder\Vendor.App_123!Main") == "Vendor.App_123!Main", "Packaged application target preserves AUMID");
Check(AppTargets.AppId(@"shell:AppsFolder\..\bad") == null && AppTargets.AppId("shell:AppsFolder\\") == null, "Application target rejects malformed identity");
Check(AppTargets.Identity("steam://run/620") == AppTargets.Identity("steam://rungameid/620"), "Steam shortcut aliases share game identity");
Check(new[] { "steam://user@run/620", "steam://run:12/620", "steam://run/+620", "steam://run/0", "steam://run/620?x=1", "steam://run/620/1", "https://run/620" }.All(t => AppTargets.SteamId(t) == null), "Steam game identity rejects invalid IDs and additional commands");
var packagedApps = RunningApps.Group(new[] { new AppWindowInfo(901, "One", "host.exe", "One", AppUserModelId: "Package!One"), new AppWindowInfo(902, "Two", "host.exe", "Two", AppUserModelId: "Package!Two") });
Check(packagedApps.Count == 2 && packagedApps.All(a => RunningApps.Matches(a.LaunchTarget, a)), "Distinct packaged identities do not merge under one host");
var steamApps = RunningApps.Group(new[] { new AppWindowInfo(903, "Game", @"C:\Games\Game.exe", "Game", LaunchTarget: "steam://rungameid/620"), new AppWindowInfo(904, "Steam", @"C:\Steam\steamwebhelper.exe", "Steam", LaunchTarget: "steam://open/main"), new AppWindowInfo(905, "Steam", @"C:\Steam\steam.exe", "Steam", LaunchTarget: "steam://open/main") });
Check(steamApps.Count == 2 && RunningApps.Matches("steam://run/620", steamApps.First(a => a.Name == "Game")), "Steam helpers group together without merging games");
Check(!RunningApps.Matches(@"shell:AppsFolder\Package!Three", packagedApps[0]), "Unrelated packaged app does not match");
var steamMeta = SteamGameMetadata.Parse("\"AppState\" { \"appid\" \"620\" \"name\" \"Portal 2\" \"installdir\" \"Portal 2\" }");
Check(steamMeta is { Id: "620", Name: "Portal 2", InstallFolder: "Portal 2" }, "Steam installed game manifest");
Check(SteamGameMetadata.Parse("AppState { appid 620 name Game installdir ../escape }") == null, "Steam manifest cannot escape install directory");
Check(ValveKeyValues.Parse("// ignored\n root { key \"quoted \\\"value\\\"\" }")[0].Children[0].Value == "quoted \"value\"", "Valve metadata comments and escaped quotes");
bool rejectedVdf = false; try { ValveKeyValues.Parse("root { key value"); } catch (FormatException) { rejectedVdf = true; }
Check(rejectedVdf, "Truncated Valve metadata rejected");
if (OperatingSystem.IsWindows()) Check(SteamGameMetadata.Libraries("libraryfolders { 0 { path \"C:\\\\Steam\" } 1 \"D:\\\\Steam\" }").Count == 2, "Modern and legacy Steam library paths");

var dockerFrontend = @"C:\Program Files\Docker\Docker\frontend\Docker Desktop.exe";
var dockerLauncher = @"C:\Program Files\Docker\Docker\Docker Desktop.exe";
Check(AppLauncherPaths.DockerLauncher(dockerFrontend) == dockerLauncher, "Docker frontend layout resolves to launcher candidate");
Check(AppLauncherPaths.DockerLauncher(@"D:/Apps/Docker/frontend/Docker Desktop.exe") == @"D:\Apps\Docker\Docker Desktop.exe", "Docker layout allows another installation drive");
Check(AppLauncherPaths.DockerLauncher(@"C:\Other\frontend\Docker Desktop.exe") == null && AppLauncherPaths.DockerLauncher(@"C:\Docker\frontend\Other.exe") == null, "Unrelated frontends are not Docker launchers");
var dockerRunning = RunningApps.Group(new[] { new AppWindowInfo(1001, "Containers", dockerFrontend, "Docker", LaunchTarget: dockerLauncher) });
Check(RunningApps.Matches(dockerLauncher, dockerRunning[0]) && RunningApps.Matches(dockerFrontend, dockerRunning[0]), "Docker pinned launcher matches real frontend without changing its executable");
Check(!RunningApps.Matches(@"D:\Other\Docker Desktop.exe", dockerRunning[0]), "Same filename in another directory is not a launcher match");
var browserProfiles = RunningApps.Group(new[] { new AppWindowInfo(1002, "Default", @"C:\Chrome\chrome.exe", "Chrome", AppUserModelId: "Chrome"), new AppWindowInfo(1003, "Work", @"C:\Chrome\chrome.exe", "Chrome", AppUserModelId: "Chrome.Work") });
var genericBrowser = RunningApps.ForTarget(@"C:\Chrome\chrome.exe", browserProfiles);
Check(genericBrowser?.Windows.Count == 2, "Generic browser shortcut includes all matching profiles");
Check(RunningApps.ForTarget(@"shell:AppsFolder\Chrome.Work", browserProfiles)?.Windows.Single().Handle == 1003, "Specific browser shortcut preserves its own profile");
Check(RunningApps.ForTarget(@"C:\Missing\chrome.exe", browserProfiles) == null, "Unrelated browser path remains unmatched");
Check(RunningApps.ForTarget(dockerLauncher, dockerRunning)?.Windows[0].Executable == dockerFrontend, "Launcher grouping preserves executable for HWND validation");
Check(WindowPreviewLayout.Fit(1920, 1080, new(20, 30, 300, 142), new(0, 80, 410, 400)) == null, "Scrolled native preview cannot overlap panel header");
Check(WindowPreviewLayout.Fit(1920, 1080, new(20, 420, 300, 142), new(0, 80, 410, 400)) == null, "Scrolled native preview cannot overlap panel footer");

Check(AppLauncherPaths.UpdaterApplication(@"C:\Apps\Tool\Update.exe", "--processStart Tool.exe") == "Tool.exe", "Updater shortcut selects its application rather than Update.exe");
Check(AppLauncherPaths.UpdaterApplication(@"C:\Apps\Tool\Update.exe", "--processStart \"My Tool.exe\"") == "My Tool.exe", "Updater supports quoted application names");
Check(AppLauncherPaths.UpdaterApplication(@"C:\Apps\Tool\Update.exe", "--processStart=Tool.exe") == "Tool.exe", "Updater accepts equals argument form");
Check(new[] { "--processStart Tool.exe --process-start-args profile", "--uninstall", "--processStart ..\\Other.exe", "--processStart C:\\Other.exe", "--processStart \"Tool.exe\" --other", "--processStart .exe" }.All(a => AppLauncherPaths.UpdaterApplication(@"C:\Apps\Tool\Update.exe", a) == null), "Updater identity rejects other commands, profiles and external paths");
Check(AppLauncherPaths.UpdaterApplication(@"C:\Apps\Tool\Other.exe", "--processStart Tool.exe") == null, "Unrelated launchers are not treated as updaters");
Check(AppLauncherPaths.UpdaterRoot(@"C:\Apps\Tool\app-1.2.3-beta.1\Tool.exe") == @"C:\Apps\Tool" && AppLauncherPaths.UpdaterRoot(@"C:\Apps\Tool\current\Tool.exe") == @"C:\Apps\Tool", "Updater identity supports version folders and current links");
Check(AppLauncherPaths.UpdaterRoot(@"C:\Apps\Tool\app-work\Tool.exe") == null, "Arbitrary app folders cannot become updater identities");
var updaterIdentity = AppLauncherPaths.UpdaterIdentity(@"C:\Apps\Tool", "Tool.exe");
var updatingApp = RunningApps.Group(new[] { new AppWindowInfo(1101, "Old", @"C:\Apps\Tool\app-1.0.0\Tool.exe", "Tool", ApplicationIdentity: updaterIdentity), new AppWindowInfo(1102, "New", @"C:\Apps\Tool\app-2.0.0\Tool.exe", "Tool", ApplicationIdentity: updaterIdentity) });
Check(updatingApp.Count == 1 && RunningApps.ForTarget(updaterIdentity, updatingApp)?.Windows.Count == 2, "Updating an application keeps both versions under its original shortcut");
Check(!RunningApps.Matches(AppLauncherPaths.UpdaterIdentity(@"D:\Another\Tool", "Tool.exe"), updatingApp[0]), "Same updater application name in another installation remains separate");
Check(updatingApp[0].Windows[0].Executable.EndsWith(@"app-1.0.0\Tool.exe"), "Updater matching preserves the real process executable for window validation");

Check(!DockReservation.Enabled(false, false) && !DockReservation.Enabled(true, true) && DockReservation.Enabled(true, false), "Reservation is opt-in and releases its space for auto-hide");
Check(DockReservation.Thickness(90, 12, 1080) == 114 && DockReservation.Thickness(200, 50, 550) == 183, "Reserved strip includes both gaps and stays bounded on a short monitor");
Check(DockReservation.IncludeOwnSpace(new(0, 114, 1920, 926), new(0, 0, 1920, 114), true) == new PreviewRect(0, 0, 1920, 1040), "Top reservation does not progressively shrink the dock work area");
Check(DockReservation.IncludeOwnSpace(new(-1920, 0, 1920, 900), new(-1920, 900, 1920, 100), false) == new PreviewRect(-1920, 0, 1920, 1000), "Bottom reservation handles monitors with negative origins");
Check(DockReservation.IncludeOwnSpace(new(0, 0, 1920, 1080), new(1920, 0, 1920, 100), true) == new PreviewRect(0, 0, 1920, 1080), "Reservation cannot expand a different monitor work area");

Check(AccessibilityPolicy.Resolve(false, true, false, false, true).ReducedMotion, "Windows disabled animations apply without changing the user preference");
Check(!AccessibilityPolicy.Resolve(false, false, false, true, true).ReducedMotion && !AccessibilityPolicy.Resolve(false, false, true, true, true).HighContrast, "Following Windows accessibility can be disabled explicitly");
Check(AccessibilityPolicy.Resolve(true, false, true, false, false).ReducedMotion, "Explicit reduced motion always applies");
Check(AccessibilityPolicy.Resolve(false, true, true, true, true) is { HighContrast: true, Translucent: false }, "High contrast uses an opaque readable surface");
Check(AccessibilityPolicy.Resolve(false, true, true, false, true) is { HighContrast: false, ReducedMotion: false, Translucent: true }, "Ending Windows contrast returns the requested material");
var windowsPrefs = System.Text.Json.JsonSerializer.Deserialize<DockSettings>("{\"ReserveSpace\":true,\"FollowSystemAccessibility\":false}")!;
Check(windowsPrefs.ReserveSpace && !windowsPrefs.FollowSystemAccessibility && !new DockSettings().ReserveSpace && new DockSettings().FollowSystemAccessibility, "Windows preferences preserve opt-in space and system accessibility defaults");
var hiddenShortcuts = new DockSettings { Widgets = ["claude", "ram"], Apps = [new("saved", "Saved", "calc.exe")] };
var dragOrder = new DockSettings { Apps = [new("a", "A", "a.exe"), new("b", "B", "b.exe"), new("c", "C", "c.exe"), new("d", "D", "d.exe")], AppGroups = [new("g", "G", ["b"])], PinnedOrder = ["app:a", "app:b", "group:g", "app:c", "app:d"] };
Check(PinnedLayout.ApplyVisibleOrder(dragOrder, ["app:d", "app:a", "group:g", "app:c"]) && PinnedLayout.Visible(dragOrder).SequenceEqual(new[] { "app:d", "app:a", "group:g", "app:c" }), "Dropping the last shortcut at the start inserts it without swapping unrelated entries");
Check(dragOrder.PinnedOrder[1] == "app:b" && dragOrder.AppGroups[0].AppIds.SequenceEqual(new[] { "b" }) && dragOrder.Apps.Select(a => a.Id).SequenceEqual(new[] { "a", "b", "c", "d" }), "Reordering visible shortcuts preserves hidden member positions, group membership and application records");
var savedDragOrder = dragOrder.PinnedOrder.ToArray();
Check(!PinnedLayout.ApplyVisibleOrder(dragOrder, ["app:a"]) && dragOrder.PinnedOrder.SequenceEqual(savedDragOrder), "A transient partial drag collection cannot remove saved shortcuts");
Check(!PinnedLayout.ApplyVisibleOrder(dragOrder, ["app:d", "app:d", "group:g", "app:c"]) && !PinnedLayout.ApplyVisibleOrder(dragOrder, ["app:d", "app:a", "group:g", "foreign"]) && dragOrder.PinnedOrder.SequenceEqual(savedDragOrder), "Duplicate and foreign drop identifiers are rejected without modifying order");
Check(!PinnedLayout.ApplyVisibleOrder(dragOrder, savedDragOrder.Where(k => k != "app:b")), "Cancelled or unchanged drag does not write the settings");
Check(PinnedLayout.ApplyVisibleOrder(dragOrder, ["app:a", "group:g", "app:c", "app:d"]) && PinnedLayout.Visible(dragOrder).SequenceEqual(new[] { "app:a", "group:g", "app:c", "app:d" }), "Dragging groups and shortcuts back restores the chosen visible order");
Check(AppTargets.ShortcutKey("calc.exe", "C:\\Windows") == AppTargets.ShortcutKey("shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "C:\\Windows"), "Calculator quick access and installed application share one pin identity");
Check(AppTargets.ShortcutKey("C:\\Windows\\System32\\calc.exe", "C:\\Windows") == AppTargets.ShortcutKey("calc.exe", "C:\\Windows"), "System Calculator path matches its quick access");
Check(AppTargets.ShortcutKey("explorer.exe", "C:\\Windows") == AppTargets.ShortcutKey("C:\\Windows\\explorer.exe", "C:\\Windows"), "Explorer path and quick access share one pin identity");
Check(AppTargets.ShortcutKey("D:\\Downloads\\calc.exe", "C:\\Windows") != AppTargets.ShortcutKey("calc.exe", "C:\\Windows") && AppTargets.ShortcutKey("D:\\Tools\\explorer.exe", "C:\\Windows") != AppTargets.ShortcutKey("explorer.exe", "C:\\Windows"), "Unrelated executable names never impersonate system quick accesses");
Check(PinnedLayout.EnsureVisible(hiddenShortcuts) && hiddenShortcuts.Widgets.SequenceEqual(new[] { "apps", "claude", "ram" }), "Pinning can reveal the existing applications section without reordering other widgets");
Check(!PinnedLayout.EnsureVisible(hiddenShortcuts) && hiddenShortcuts.Widgets.Count(w => w == "apps") == 1 && hiddenShortcuts.Apps.Count == 1, "Revealing shortcuts is idempotent and preserves saved shortcuts");
hiddenShortcuts.Widgets = ["claude", "apps", "ram"];
Check(!PinnedLayout.EnsureVisible(hiddenShortcuts) && hiddenShortcuts.Widgets[1] == "apps", "Already visible shortcuts keep their chosen widget position");
foreach (string page in new[] { "appearance", "layout-preferences", "widget-preferences", "apps-manager", "windows-settings", "installation", "backups", "accessibility", "dock-settings", "installed-apps", "shortcut-editor", "group-editor", "app-group", "app-windows", "recent-documents", "folder-stack-12", "health", "update-preview", "restore-version", "backup-preview", "backup-recovery", "gallery", "order" })
{
    string? current = page; var visited = new HashSet<string>();
    while (current != null && visited.Add(current)) current = PanelNavigation.Parent(current);
    Check(current == null && visited.Contains("settings"), "Options have a cycle-free return to preferences: " + page);
}
Check(PanelNavigation.Parent("settings") == null && PanelNavigation.Parent("claude") == null, "Root pages do not create misleading back destinations");
Check(PanelNavigation.Parent("task-editor") == "tasks" && PanelNavigation.Parent("calendar-exceptions") == "calendar" && PanelNavigation.Parent("claude-direct-consent") == "claude", "Editors and setup return to their relevant widget");
Check(ShortcutDragLayout.Destination(19, 0, 20) == 0 && ShortcutDragLayout.Destination(0, 20, 20) == 19, "Long lists insert in both directions at their ends");
Check(ShortcutDragLayout.Destination(7, 7, 20) == 7 && ShortcutDragLayout.Destination(7, 8, 20) == 7, "Adjacent insertion slots around the source do not move it");
Check(ShortcutDragLayout.Destination(-1, 0, 20) == -1 && ShortcutDragLayout.Destination(0, 0, 0) == -1 && ShortcutDragLayout.Destination(0, 21, 20) == -1, "Stale source or invalid slot cannot reorder a list");
Check(ShortcutDragLayout.ScrollStep(-1, 295, 360, 300) == 0 && ShortcutDragLayout.ScrollStep(100, 301, 360, 300) == 0, "Dragging outside a list stops its automatic scrolling");
Check(ShortcutDragLayout.ScrollStep(100, 150, 360, 300) == 0 && ShortcutDragLayout.ScrollStep(100, 0, 360, 300) == -14 && ShortcutDragLayout.ScrollStep(100, 300, 360, 300) == 14, "Scrolling only runs at viewport edges and has a bounded speed");
Check(ShortcutDragLayout.ScrollStep(100, 290, 360, 300) > ShortcutDragLayout.ScrollStep(100, 275, 360, 300) && ShortcutDragLayout.ScrollStep(0, double.NaN, 360, 300) == 0, "Edge scrolling accelerates gradually and rejects invalid coordinates");
// Failure and recovery use fake credentials and a deterministic HTTP handler only.
var recoveryNow = directNow;
HttpStatusCode nextStatus = HttpStatusCode.OK; bool brokenPayload = false, offlineQuota = false; string quotaIdentity = "fixture-account";
using var recoveryHttp = new HttpClient(new QuotaTestHttpHandler(_ => {
    if (offlineQuota) throw new HttpRequestException("Simulated network loss");
    return new(nextStatus) { Content = new StringContent(brokenPayload ? "{broken" : directPayload) };
}));
var recoveryProvider = new ClaudeDesktopDirectProvider(recoveryHttp, () => new ClaudeDesktopCredential("fixture-secret", quotaIdentity), () => recoveryNow);
var goodQuota = await recoveryProvider.RefreshAsync(CancellationToken.None);
nextStatus = HttpStatusCode.ServiceUnavailable; recoveryNow = recoveryNow.AddMinutes(6);
var offlineReading = await recoveryProvider.RefreshAsync(CancellationToken.None);
Check(offlineReading.IsPreviousReading && offlineReading.Windows.SequenceEqual(goodQuota.Windows) && offlineReading.UpdatedAt == goodQuota.UpdatedAt, "Temporary server failure keeps the last reading explicitly marked as previous without changing its timestamp");
var failureHistory = new List<QuotaSample>();
Check(!UsageHistory.Append(failureHistory, offlineReading, recoveryNow), "Retained failure readings cannot become fresh history samples or quota alerts");
nextStatus = HttpStatusCode.Unauthorized; recoveryNow = recoveryNow.AddMinutes(6);
Check((await recoveryProvider.RefreshAsync(CancellationToken.None)).Windows.Count == 0, "Expired authorization clears previously retained quotas");
nextStatus = HttpStatusCode.OK; recoveryNow = recoveryNow.AddMinutes(6);
Check((await recoveryProvider.RefreshAsync(CancellationToken.None)).State == ConnectionState.Connected, "A renewed session recovers automatically on the next permitted attempt");
offlineQuota = true; recoveryNow = recoveryNow.AddMinutes(6);
Check((await recoveryProvider.RefreshAsync(CancellationToken.None)).IsPreviousReading, "Network loss preserves a labelled previous reading");
offlineQuota = false; brokenPayload = true; recoveryNow = recoveryNow.AddMinutes(6);
Check((await recoveryProvider.RefreshAsync(CancellationToken.None)).IsPreviousReading, "Malformed upstream JSON cannot replace valid percentages with invented values");
quotaIdentity = "another-fixture-account"; recoveryNow = recoveryNow.AddMinutes(6);
Check((await recoveryProvider.RefreshAsync(CancellationToken.None)).Windows.Count == 0, "An account change discards the previous account reading even when the new response fails");
foreach (var malformedBackup in new[] {
    "{\"Format\":\"BrisaDock.Backup\",\"Version\":1,\"Settings\":{\"Note\":\"first\",\"Note\":\"second\"}}",
    "{\"Format\":\"BrisaDock.Backup\",\"Version\":1,\"Version\":1,\"Settings\":{}}" })
{
    try { SettingsBackup.Read(malformedBackup); Check(false, "Ambiguous backup should be rejected"); }
    catch (InvalidDataException) { Check(true, "Repeated backup fields are rejected before preview or restoration"); }
}
foreach (var malformedBackup in new[] { "{", "null", "[]", "{\"Format\":\"BrisaDock.Backup\",\"Version\":1,\"Settings\":null}", "{\"Format\":\"BrisaDock.Backup\",\"Version\":1,\"Settings\":{\"Version\":999}}" })
{
    try { SettingsBackup.Read(malformedBackup); Check(false, "Damaged or unsupported backup should be rejected"); }
    catch (Exception e) when (e is JsonException or InvalidDataException) { Check(true, "Damaged, empty and future backups fail without altering live settings"); }
}
var dedupStore = new SettingsStore(Path.Combine(directory, "dedup-store")); var dedupSettings = new DockSettings { Note = "Keep this value" };
dedupStore.Save(dedupSettings); string dedupPath = Path.Combine(dedupStore.DirectoryPath, "dock.json");
var fixedWriteTime = DateTime.UtcNow.AddDays(-1); File.SetLastWriteTimeUtc(dedupPath, fixedWriteTime); var observedWriteTime = File.GetLastWriteTimeUtc(dedupPath);
dedupStore.Save(dedupSettings);
Check(File.GetLastWriteTimeUtc(dedupPath) == observedWriteTime && !File.Exists(dedupPath + ".previous"), "An unchanged save does not rewrite settings or rotate the recovery file");
dedupSettings.Note = "Changed value"; dedupStore.Save(dedupSettings);
Check(dedupStore.Load().Note == "Changed value" && JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(dedupPath + ".previous"))!.Note == "Keep this value", "A real change is committed and preserves the prior settings after save deduplication");
var backoffNow = directNow; int longBackoffCalls = 0;
using var longBackoffHttp = new HttpClient(new QuotaTestHttpHandler(_ => {
    longBackoffCalls++;
    var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
    response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromHours(2)); return response;
}));
var longBackoffProvider = new ClaudeDesktopDirectProvider(longBackoffHttp, () => selectedCredential, () => backoffNow);
await longBackoffProvider.RefreshAsync(CancellationToken.None); backoffNow = backoffNow.AddMinutes(61);
await longBackoffProvider.RefreshAsync(CancellationToken.None);
Check(longBackoffCalls == 1 && longBackoffProvider.NextAttemptAt > backoffNow, "Server Retry-After longer than an hour is respected without premature polling");
backoffNow = backoffNow.AddMinutes(60); await longBackoffProvider.RefreshAsync(CancellationToken.None);
Check(longBackoffCalls == 2, "Polling becomes available again after the full server delay");
try { SettingsBackup.Read(new string('á', SettingsBackup.MaxBytes / 2 + 1)); Check(false, "UTF8 limit must count bytes"); }
catch (InvalidDataException) { Check(true, "Backup size limit counts UTF8 bytes rather than characters"); }
using var alreadyCancelled = new CancellationTokenSource(); alreadyCancelled.Cancel();
try { await recoveryProvider.RefreshAsync(alreadyCancelled.Token); Check(false, "A cancelled quota request should not start"); }
catch (OperationCanceledException) { Check(true, "Quota cancellation propagates without starting a request or replacing the last snapshot"); }
if (Environment.ProcessPath is { } fakeCodexPath && Path.GetFileNameWithoutExtension(fakeCodexPath) != "dotnet")
{
    string? originalReplyMode = Environment.GetEnvironmentVariable("BRISA_TEST_CODEX_REPLY");
    try
    {
        foreach (string mode in new[] { "error", "malformed", "exit" })
        {
            Environment.SetEnvironmentVariable("BRISA_TEST_CODEX_REPLY", mode);
            var failedCodex = await new CodexUsageProvider(fakeCodexPath).RefreshAsync(CancellationToken.None);
            Check(failedCodex.State == ConnectionState.Error && failedCodex.Windows.Count == 0 && !failedCodex.Message.Contains("fixture"), "Codex protocol failure is bounded, clears unavailable quotas and uses a local message: " + mode);
        }
    }
    finally { Environment.SetEnvironmentVariable("BRISA_TEST_CODEX_REPLY", originalReplyMode); }
}
MemoryStream ArchiveFixture(string? extra = null, bool damaged = false, bool duplicate = false)
{
    var memory = new MemoryStream();
    using (var zip = new System.IO.Compression.ZipArchive(memory, System.IO.Compression.ZipArchiveMode.Create, true))
    {
        var payloads = new Dictionary<string, string> { ["BrisaDock.exe"] = "inert fixture", ["maintenance/Install-Brisa.ps1"] = "inert installer fixture" };
        if (extra != null && extra != "unlisted.txt") payloads.Add(extra, "extra");
        foreach (var pair in payloads) { using var writer = new StreamWriter(zip.CreateEntry(pair.Key).Open()); writer.Write(damaged && pair.Key == "BrisaDock.exe" ? "altered" : pair.Value); }
        if (extra == "unlisted.txt") { using var writer = new StreamWriter(zip.CreateEntry(extra).Open()); writer.Write("extra"); }
        if (duplicate) { using var writer = new StreamWriter(zip.CreateEntry("BRISADOCK.exe").Open()); writer.Write("duplicate"); }
        using var manifest = new StreamWriter(zip.CreateEntry("brisa-package.json").Open());
        manifest.Write(JsonSerializer.Serialize(new { Product = "Brisa Dock", Format = 1, Platform = "win-x64", Version = "1.2.3", Files = payloads.Keys.ToArray(), Hashes = payloads.ToDictionary(p => p.Key, p => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p.Value)))) }));
    }
    memory.Position = 0; return memory;
}
using (var archiveFixture = ArchiveFixture()) Check(ReleaseArchive.Verify(archiveFixture) is { Version: "1.2.3", FileCount: 2 }, "Distribution accepts a complete manifest and matching payload");
foreach (var unsafeName in new[] { "../escape.txt", "/absolute.txt", "C:/outside.txt", "folder/../escape.txt", "folder/CON.txt", "folder/trailing. ", "data/dock.json", "qa-data/dock.json", "private.key", "private.pem", "auth.json", "credentials.json", ".env", "unlisted.txt" })
{
    using var archiveFixture = ArchiveFixture(extra: unsafeName);
    try { ReleaseArchive.Verify(archiveFixture); Check(false, "Unsafe package must fail"); }
    catch (InvalidDataException) { Check(true, "Distribution rejects unsafe, private or unlisted payload: " + unsafeName); }
}
using (var archiveFixture = ArchiveFixture(damaged: true))
{
    try { ReleaseArchive.Verify(archiveFixture); Check(false, "Altered payload must fail"); }
    catch (InvalidDataException) { Check(true, "Distribution rejects content that differs from its manifest"); }
}
using (var archiveFixture = ArchiveFixture(duplicate: true))
{
    try { ReleaseArchive.Verify(archiveFixture); Check(false, "Case duplicate must fail"); }
    catch (InvalidDataException) { Check(true, "Distribution rejects case-insensitive duplicate paths"); }
}
string extractedFixture = Path.Combine(directory, "extracted-release");
using (var archiveFixture = ArchiveFixture()) ReleaseArchive.Extract(archiveFixture, extractedFixture);
Check(File.ReadAllText(Path.Combine(extractedFixture, "BrisaDock.exe")) == "inert fixture", "A validated distribution extracts only its declared inert files");
using (var archiveFixture = ArchiveFixture())
{
    try { ReleaseArchive.Extract(archiveFixture, extractedFixture); Check(false, "Existing extraction destination must fail"); }
    catch (IOException) { Check(true, "Distribution extraction cannot overwrite an existing directory"); }
}
// Tray popups follow their Shell anchor, independently of the dock monitor.
{
    var work = new PreviewRect(0, 0, 1920, 1040);
    var icon = new PreviewRect(1830, 1050, 24, 24);
    var menu = TrayMenuLayout.Fit(work, icon, 1, 286, 390);
    Check(menu.X >= 0 && menu.X + menu.Width <= 1920 && menu.Y + menu.Height < icon.Y, "Tray menu opens above its icon on the icon monitor");
    var negative = TrayMenuLayout.Fit(new(-1600, -300, 1600, 860), new(-100, -295, 24, 24), 1.5, 286, 390);
    Check(negative.X >= -1590 && negative.X + negative.Width <= -10 && negative.Y >= -263, "Tray menu handles negative monitor origins, top taskbars and mixed DPI");
    var small = TrayMenuLayout.Fit(new(0, 0, 260, 190), new(250, 190, 24, 24), 2, 286, 500);
    Check(small.Width <= 240 && small.Height <= 170 && small.X >= 10 && small.Y >= 10, "Tray menu stays inside small work areas instead of following the dock");
    var leftEdge = TrayMenuLayout.Fit(work, new(0, 1040, 20, 20), 1, 286, 390);
    Check(leftEdge.X == 10, "Tray menu clamps at the left screen edge");
    var fallback = TrayMenuLayout.Fit(work, icon, double.NaN, 286, 390);
    Check(fallback == menu, "Tray menu falls back to a valid scale for invalid DPI input");
}

// Remote updates use fixed GitHub endpoints and reject unverifiable release payloads.
{
    using var fixture = ArchiveFixture();
    byte[] bytes = fixture.ToArray();
    string digest = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    string Metadata(string hash, bool draft = false, bool prerelease = false, string tag = "v9.8.7", string name = "Brisa-9.8.7-win-x64.zip", long? size = null) => JsonSerializer.Serialize(new {
        tag_name = tag, draft, prerelease, body = "Release fixture", assets = new[] { new { id = 123L, name, state = "uploaded", size = size ?? bytes.Length, digest = hash } }
    });
    var available = GitHubUpdate.Parse(Metadata(digest), new Version(1, 0, 0));
    Check(available is { Version: "9.8.7", AssetId: 123 }, "A stable release exposes exactly the matching Windows x64 asset");
    Check(GitHubUpdate.Parse(Metadata(digest), new Version(9, 8, 7, 0)) == null, "Remote checking normalizes the installed four-part version");
    Check(GitHubUpdate.Parse(Metadata(digest), new Version(10, 0, 0)) == null, "Remote checking never offers a downgrade");
    Check(GitHubUpdate.Parse(Metadata(digest, draft: true), new Version(1, 0, 0)) == null && GitHubUpdate.Parse(Metadata(digest, prerelease: true), new Version(1, 0, 0)) == null, "Draft and prerelease versions are never offered automatically");
    foreach (string invalid in new[] { Metadata(""), Metadata(digest, tag: "v9.8.7-beta"), Metadata(digest, name: "Other.exe"), Metadata(digest, size: 268435457), "{}", "[]" })
    {
        try { GitHubUpdate.Parse(invalid, new Version(1, 0, 0)); Check(false, "Invalid release must fail"); }
        catch (InvalidDataException) { Check(true, "Remote checking rejects malformed or unverifiable release metadata"); }
    }
    using var http = new HttpClient(new QuotaTestHttpHandler(request => {
        Check(request.RequestUri!.AbsoluteUri == "https://api.github.com/repos/danieljcorreac/brisa-dock/releases/latest" && request.Headers.Authorization == null, "Public release checking uses only the fixed repository without credentials");
        return new(HttpStatusCode.OK) { Content = new StringContent(Metadata(digest)) };
    }));
    using var publicUpdates = new GitHubUpdates(http);
    Check(await publicUpdates.CheckAsync(new Version(1, 0, 0), CancellationToken.None) != null, "Public update metadata works without GitHub CLI");
    using var unavailableHttp = new HttpClient(new QuotaTestHttpHandler(_ => new(HttpStatusCode.NotFound)));
    using var unavailableUpdates = new GitHubUpdates(unavailableHttp);
    try { await unavailableUpdates.CheckAsync(new Version(1, 0, 0), CancellationToken.None); Check(false, "Unavailable release must fail clearly"); }
    catch (IOException error) { Check(!error.Message.Contains("CLI", StringComparison.Ordinal), "An unavailable public release never asks for CLI authentication"); }
    using var limitedHttp = new HttpClient(new QuotaTestHttpHandler(_ => { var response = new HttpResponseMessage(HttpStatusCode.Forbidden); response.Headers.Add("X-RateLimit-Remaining", "0"); return response; }));
    using var limitedUpdates = new GitHubUpdates(limitedHttp);
    try { await limitedUpdates.CheckAsync(new Version(1, 0, 0), CancellationToken.None); Check(false, "Rate-limited release query must fail clearly"); }
    catch (IOException error) { Check(error.Message.Contains("temporalmente", StringComparison.Ordinal), "Public API rate limits produce a retry-later message instead of an authentication flow"); }
    using var oversizedHttp = new HttpClient(new QuotaTestHttpHandler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[2097153]) }));
    using var oversizedUpdates = new GitHubUpdates(oversizedHttp);
    try { await oversizedUpdates.CheckAsync(new Version(1, 0, 0), CancellationToken.None); Check(false, "Oversized release metadata must fail"); }
    catch (InvalidDataException) { Check(true, "Anonymous metadata downloads enforce their size limit before parsing"); }
    using var binaryHttp = new HttpClient(new QuotaTestHttpHandler(request => {
        Check(request.RequestUri!.AbsolutePath.EndsWith("/releases/assets/123", StringComparison.Ordinal) && request.Headers.Authorization == null, "Anonymous asset download uses the fixed repository and verified asset identifier without credentials");
        return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    }));
    using var downloads = new GitHubUpdates(binaryHttp);
    // ArchiveFixture's own version is authoritative; a different release tag must be rejected.
    try { await downloads.DownloadAsync(available!, Path.Combine(directory, "remote-mismatch"), CancellationToken.None); Check(false, "Mismatched package must fail"); }
    catch (InvalidDataException) { Check(!Directory.EnumerateFileSystemEntries(Path.Combine(directory, "remote-mismatch")).Any(), "Mismatched archive versions are rejected and their temporary files removed"); }
    using var versionStream = new MemoryStream(bytes);
    string fixtureVersion = ReleaseArchive.Verify(versionStream).Version;
    string source = await downloads.DownloadAsync(available! with { Version = fixtureVersion }, Path.Combine(directory, "remote-valid"), CancellationToken.None);
    Check(File.ReadAllText(Path.Combine(source, "BrisaDock.exe")) == "inert fixture", "A matching GitHub digest and archive version extract a verified inert update");
    try { using var broken = new MemoryStream(bytes[..^1]); available!.Verify(broken); Check(false, "Truncated download must fail"); }
    catch (InvalidDataException) { Check(true, "Remote checking rejects truncated downloads before extraction"); }
    try { using var altered = new MemoryStream(bytes); (available! with { Sha256 = new string('0', 64) }).Verify(altered); Check(false, "Changed digest must fail"); }
    catch (InvalidDataException) { Check(true, "Remote checking rejects a mismatched GitHub SHA-256 digest"); }
}

Console.WriteLine($"{checks} checks passed. Test artifacts: {directory}");
return 0;
