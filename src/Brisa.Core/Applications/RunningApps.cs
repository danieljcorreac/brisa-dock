namespace Brisa.Core;

public record AppWindowInfo(long Handle, string Title, string Executable, string AppName, string? HostExecutable = null, string? AppUserModelId = null, string? LaunchTarget = null, string? ApplicationIdentity = null);
public record RunningApp(string Key, string Name, string Executable, IReadOnlyList<AppWindowInfo> Windows)
{
    public string LaunchTarget => Windows.FirstOrDefault()?.LaunchTarget ?? (Windows.FirstOrDefault()?.AppUserModelId is string id ? AppTargets.AppsFolder + id : Executable);
}
public static class RunningApps
{
    public static string Key(string executable) => executable.Trim().Replace('/', '\\').ToUpperInvariant();
    public static IReadOnlyList<RunningApp> Group(IEnumerable<AppWindowInfo> windows) => windows
        .Where(w => w.Handle != 0 && !string.IsNullOrWhiteSpace(w.Executable) && !string.IsNullOrWhiteSpace(w.Title))
        .DistinctBy(w => w.Handle).GroupBy(w => w.AppUserModelId is string id ? AppTargets.Identity(AppTargets.AppsFolder + id) : w.ApplicationIdentity ?? (w.LaunchTarget != null ? AppTargets.Identity(w.LaunchTarget) : Key(w.Executable)))
        .Select(g => new RunningApp(g.Key, g.First().AppName, g.First().Executable, g.OrderBy(w => w.Handle).ToArray()))
        .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ThenBy(g => g.Key, StringComparer.Ordinal).ToArray();
    public static bool Matches(string target, string executable)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(executable)) return false;
        // URLs, documents and shortcuts need shell identity resolution; never guess their owning app.
        var candidate = target.Trim().Replace('/', '\\');
        if (!candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        if (candidate.Contains('\\') || candidate.Contains(':')) return Key(candidate) == Key(executable);
        return candidate.Equals(executable.Replace('/', '\\').Split('\\').Last(), StringComparison.OrdinalIgnoreCase);
    }
    public static bool Matches(string target, RunningApp app)
    {
        if (target.StartsWith(AppLauncherPaths.UpdaterPrefix, StringComparison.OrdinalIgnoreCase)) return app.Windows.Any(w => string.Equals(w.ApplicationIdentity, target, StringComparison.OrdinalIgnoreCase));
        if (AppTargets.AppId(target) is string id) return app.Windows.Any(w => string.Equals(w.AppUserModelId, id, StringComparison.OrdinalIgnoreCase));
        if (AppTargets.SteamId(target) is string game) return AppTargets.SteamId(app.LaunchTarget) == game;
        if (target.Equals("steam://open/main", StringComparison.OrdinalIgnoreCase)) return app.LaunchTarget.Equals(target, StringComparison.OrdinalIgnoreCase);
        return app.Windows.Any(w => Matches(target, w.Executable) || w.LaunchTarget != null && Matches(target, w.LaunchTarget));
    }
    public static RunningApp? ForTarget(string target, IEnumerable<RunningApp> apps)
    {
        var matches = apps.Where(a => Matches(target, a)).ToArray();
        if (matches.Length == 0) return null;
        return new(AppTargets.Identity(target), matches[0].Name, matches[0].Executable,
            matches.SelectMany(a => a.Windows).DistinctBy(w => w.Handle).OrderBy(w => w.Handle).ToArray());
    }
}
