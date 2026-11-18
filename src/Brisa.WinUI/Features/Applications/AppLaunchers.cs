using Brisa.Core;
using System.Collections.Concurrent;
using System.Diagnostics;
namespace Brisa.WinUI;

internal static class AppLaunchers
{
    static readonly ConcurrentDictionary<string, (DateTimeOffset Stamp, string Target)> cache = new(StringComparer.OrdinalIgnoreCase);
    static readonly ConcurrentDictionary<string, (string Path, DateTime Stamp)> shortcuts = new(StringComparer.OrdinalIgnoreCase);
    public static string? Identity(string executable)
    {
        try
        {
            string? root = AppLauncherPaths.UpdaterRoot(executable);
            return root != null && Path.IsPathFullyQualified(root) && !root.StartsWith(@"\\") && File.Exists(Path.Combine(root, "Update.exe"))
                ? AppLauncherPaths.UpdaterIdentity(Path.GetFullPath(root), Path.GetFileName(executable)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
    public static string? UpdaterExecutable(string launcher, string arguments)
    {
        string? application = AppLauncherPaths.UpdaterApplication(launcher, arguments);
        if (application == null || !Path.IsPathFullyQualified(launcher) || launcher.StartsWith(@"\\") || !File.Exists(launcher)) return null;
        try
        {
            string root = Path.GetDirectoryName(Path.GetFullPath(launcher))!;
            string current = Path.Combine(root, "current", application); if (File.Exists(current)) return current;
            return Directory.EnumerateDirectories(root, "app-*").Take(64)
                .Select(folder => Path.Combine(folder, application)).Where(file => AppLauncherPaths.UpdaterRoot(file) != null && File.Exists(file))
                .OrderByDescending(file => Version.TryParse(Path.GetFileName(Path.GetDirectoryName(file)!)[4..].Split('-', '+')[0], out var version) ? version : new Version())
                .ThenByDescending(file => !Path.GetFileName(Path.GetDirectoryName(file)!)[4..].Contains('-')).FirstOrDefault();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
    public static string? Shortcut(string shortcut, ShortcutDetails link)
    {
        string? application = AppLauncherPaths.UpdaterApplication(link.Target, link.Arguments);
        if (application == null || !Path.IsPathFullyQualified(link.Target) || link.Target.StartsWith(@"\\") || !File.Exists(link.Target)) return null;
        try
        {
            string root = Path.GetDirectoryName(Path.GetFullPath(link.Target))!;
            if (UpdaterExecutable(link.Target, link.Arguments) == null) return null;
            string identity = AppLauncherPaths.UpdaterIdentity(root, application);
            if (shortcuts.Count > 512) shortcuts.Clear(); shortcuts[identity] = (shortcut, File.GetLastWriteTimeUtc(shortcut)); return identity;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
    public static string PreferredTarget(string executable)
    {
        string? identity = Identity(executable);
        if (identity != null && shortcuts.TryGetValue(identity, out var shortcut))
        {
            try
            {
                if (File.Exists(shortcut.Path))
                {
                    DateTime stamp = File.GetLastWriteTimeUtc(shortcut.Path);
                    if (stamp == shortcut.Stamp) return shortcut.Path;
                    if (ShortcutIdentity.Resolve(shortcut.Path) == identity) { shortcuts[identity] = (shortcut.Path, stamp); return shortcut.Path; }
                }
                shortcuts.TryRemove(identity, out _);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException) { shortcuts.TryRemove(identity, out _); }
        }
        return Resolve(executable);
    }
    public static string Resolve(string executable)
    {
        string? launcher = AppLauncherPaths.DockerLauncher(executable);
        if (launcher == null) return executable;
        if (cache.TryGetValue(executable, out var found) && DateTimeOffset.Now - found.Stamp < TimeSpan.FromMinutes(2)) return found.Target;
        string target = executable;
        try
        {
            if (File.Exists(executable) && File.Exists(launcher))
            {
                var app = FileVersionInfo.GetVersionInfo(executable); var start = FileVersionInfo.GetVersionInfo(launcher);
                if (app.CompanyName == "Docker Inc." && start.CompanyName == app.CompanyName && app.ProductName == "Docker Desktop" && start.ProductName == "Docker Desktop Launcher") target = launcher;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ArgumentException) { }
        if (cache.Count > 128) cache.Clear(); cache[executable] = (DateTimeOffset.Now, target); return target;
    }
}
