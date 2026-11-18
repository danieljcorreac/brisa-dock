namespace Brisa.Core;

public static class AppLauncherPaths
{
    public const string UpdaterPrefix = "UPDATER|";
    public static string? UpdaterApplication(string launcher, string arguments)
    {
        if (!launcher.Replace('/', '\\').EndsWith("\\Update.exe", StringComparison.OrdinalIgnoreCase) || arguments.Length > 1024) return null;
        var match = System.Text.RegularExpressions.Regex.Match(arguments, "^\\s*--processStart(?:\\s+|=)(?:\"([^\"]+)\"|([^\\s\"]+))\\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        string name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return name.Length is > 4 and <= 200 && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !name.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c)) ? name : null;
    }
    public static string? UpdaterRoot(string executable)
    {
        string path = executable.Replace('/', '\\'); int file = path.LastIndexOf('\\'); if (file <= 0 || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
        int parent = path.LastIndexOf('\\', file - 1); if (parent < 0) return null;
        string folder = path[(parent + 1)..file];
        bool version = System.Text.RegularExpressions.Regex.IsMatch(folder, @"^app-[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return folder.Equals("current", StringComparison.OrdinalIgnoreCase) || version ? path[..parent] : null;
    }
    public static string UpdaterIdentity(string root, string application) => UpdaterPrefix + root.TrimEnd('\\', '/').Replace('/', '\\').ToUpperInvariant() + "|" + application.ToUpperInvariant();
    // A layout candidate only. The Windows adapter must verify both files and their metadata.
    public static string? DockerLauncher(string executable)
    {
        string path = executable.Replace('/', '\\');
        const string suffix = "\\frontend\\Docker Desktop.exe";
        if (!path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return null;
        string root = path[..^suffix.Length];
        if (!root.EndsWith("\\Docker", StringComparison.OrdinalIgnoreCase)) return null;
        return root + "\\Docker Desktop.exe";
    }
}
