using Brisa.Core;
using System.Runtime.InteropServices;
namespace Brisa.WinUI;

internal static class LauncherChecks
{
    public static object Run()
    {
        var checks = new List<string>();
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add(name); }
        string root = Path.Combine(AppContext.BaseDirectory, "qa-launchers", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string updater = Path.Combine(root, "Update.exe"), old = Path.Combine(root, "app-1.0.0", "Sample.exe"), next = Path.Combine(root, "app-2.0.0", "Sample.exe"), shortcut = Path.Combine(root, "Sample.lnk");
        Directory.CreateDirectory(Path.GetDirectoryName(old)!); File.WriteAllText(updater, "Brisa QA metadata; never executed"); File.WriteAllText(old, "Brisa QA metadata; never executed");
        object? shell = null, link = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            link = ((dynamic)shell!).CreateShortcut(shortcut); ((dynamic)link).TargetPath = updater; ((dynamic)link).Arguments = "--processStart Sample.exe";
            ((dynamic)link).WorkingDirectory = root; ((dynamic)link).Save();
        }
        finally { if (link != null) Marshal.ReleaseComObject(link); if (shell != null) Marshal.ReleaseComObject(shell); }
        string identity = ShortcutIdentity.Resolve(shortcut);
        Check(identity.StartsWith(AppLauncherPaths.UpdaterPrefix), "Native .lnk resolves to updater application");
        Check(AppLaunchers.Identity(old) == identity, "Original application window matches shortcut");
        Check(AppLaunchers.PreferredTarget(old) == shortcut, "Pinning keeps original updater shortcut");
        Directory.CreateDirectory(Path.GetDirectoryName(next)!); File.WriteAllText(next, "Brisa QA metadata; never executed"); File.Delete(old);
        Check(AppLaunchers.UpdaterExecutable(updater, "--processStart Sample.exe") == next, "Application icon resolves from installed version after update");
        Check(AppLaunchers.Identity(next) == identity, "Next installed version keeps the same identity");
        Check(AppLaunchers.PreferredTarget(old) == shortcut, "Missing previous executable recovers through original shortcut");
        Check(ShortcutIdentity.Describe(shortcut).Arguments == "--processStart Sample.exe", "Original launch arguments remain intact");
        Check(AppLaunchers.Shortcut(shortcut, new(updater, "--processStart Sample.exe --process-start-args profile")) == null, "Profile arguments are not merged");
        string icons = Path.Combine(root, "appcache", "librarycache", "620"); Directory.CreateDirectory(icons);
        string modernIcon = Path.Combine(icons, "sample_icon.jpg"); File.WriteAllText(modernIcon, "Metadata lookup fixture");
        Check(SteamApplications.Icon(root, "620") == modernIcon, "Modern Steam icon cache is discovered");
        string legacyIcon = Path.Combine(Path.GetDirectoryName(icons)!, "620_icon.jpg"); File.WriteAllText(legacyIcon, "Metadata lookup fixture");
        Check(SteamApplications.Icon(root, "620") == legacyIcon, "Legacy Steam icon cache remains supported");
        Check(SteamApplications.Icon(root, "../620") == null, "Steam icon lookup rejects an invalid game ID");
        foreach (string version in new[] { "2.9.0", "2.10.0", "2.10.0-beta.1" })
        {
            string executable = Path.Combine(root, "app-" + version, "Sample.exe"); Directory.CreateDirectory(Path.GetDirectoryName(executable)!); File.WriteAllText(executable, "Version selection fixture");
        }
        Check(AppLaunchers.UpdaterExecutable(updater, "--processStart Sample.exe") == Path.Combine(root, "app-2.10.0", "Sample.exe"), "Icon lookup compares numeric versions and prefers stable version");
        Check(AppLaunchers.UpdaterExecutable(updater, "--processStart Other.exe") == null, "Another executable is not substituted for a missing application");
        void RewriteShortcut(string arguments)
        {
            object? host = null, entry = null;
            try { host = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!); entry = ((dynamic)host!).CreateShortcut(shortcut); ((dynamic)entry).Arguments = arguments; ((dynamic)entry).Save(); File.SetLastWriteTimeUtc(shortcut, DateTime.UtcNow.AddSeconds(2)); }
            finally { if (entry != null) Marshal.ReleaseComObject(entry); if (host != null) Marshal.ReleaseComObject(host); }
        }
        RewriteShortcut("--processStart Sample.exe --process-start-args profile");
        Check(AppLaunchers.PreferredTarget(next) == next, "Changed shortcut with profile arguments invalidates the old mapping");
        RewriteShortcut("--processStart Sample.exe");
        Check(ShortcutIdentity.Resolve(shortcut) == identity && AppLaunchers.PreferredTarget(next) == shortcut, "Restored updater shortcut can be registered again");
        File.Delete(shortcut);
        Check(AppLaunchers.PreferredTarget(next) == next, "Removed shortcuts cannot remain preferred launch targets");
        return new { Passed = true, Count = checks.Count, Checks = checks };
    }
}
