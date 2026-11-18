using Brisa.Core;
using Microsoft.Win32;
namespace Brisa.WinUI;
internal record SteamApplication(string Id, string Name, string Directory, string? Icon)
{ public string Target => "steam://rungameid/" + Id; }
internal static class SteamApplications
{
    static IReadOnlyList<SteamApplication> games = [];
    static DateTimeOffset stamp;
    static readonly object sync = new();
    public static string? Root { get { try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"); return key?.GetValue("SteamPath") is string path ? path.Replace('/', '\\') : null; } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return null; } } }
    public static string? Client => Root is string root ? Path.Combine(root, "steam.exe") : null;
    internal static string? Icon(string root, string id)
    {
        if (AppTargets.SteamId("steam://rungameid/" + id) != id) return null;
        string cache = Path.Combine(root, "appcache", "librarycache");
        string legacy = Path.Combine(cache, id + "_icon.jpg"); if (File.Exists(legacy)) return legacy;
        try
        {
            string folder = Path.Combine(cache, id); if (!Directory.Exists(folder)) return null;
            return Directory.EnumerateFiles(folder, "*_icon.jpg").Take(32).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
    static string? Read(string path) { try { var file = new FileInfo(path); return file.Exists && file.Length <= 1048576 ? File.ReadAllText(path) : null; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; } }
    public static IReadOnlyList<SteamApplication> Read(bool force = false)
    {
        lock (sync)
        {
            if (!force && DateTimeOffset.Now - stamp < TimeSpan.FromMinutes(5)) return games;
            stamp = DateTimeOffset.Now; var result = new List<SteamApplication>();
            if (Root is not string root) return games = [];
            var libraries = new List<string> { root };
            try { if (Read(Path.Combine(root, "steamapps", "libraryfolders.vdf")) is string folders) libraries.AddRange(SteamGameMetadata.Libraries(folders)); } catch (FormatException) { }
            foreach (string library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    string folder = Path.Combine(library, "steamapps"); if (!Directory.Exists(folder)) continue;
                    foreach (string file in Directory.EnumerateFiles(folder, "appmanifest_*.acf").Take(1000))
                    {
                        try
                        {
                            if (Read(file) is not string text || SteamGameMetadata.Parse(text) is not { } game) continue;
                            string installed = Path.Combine(folder, "common", game.InstallFolder); if (!Directory.Exists(installed)) continue;
                            result.Add(new(game.Id, game.Name, installed, Icon(root, game.Id)));
                        }
                        catch (Exception e) when (e is FormatException or ArgumentException or IOException or UnauthorizedAccessException) { }
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
            return games = result.DistinctBy(g => g.Id).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
    }
    public static string? LaunchTarget(string executable)
    {
        if (Client is string client && (string.Equals(client, executable, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(executable).Equals("steamwebhelper.exe", StringComparison.OrdinalIgnoreCase) && executable.StartsWith(Path.GetDirectoryName(client)! + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) return "steam://open/main";
        return Read().FirstOrDefault(g => executable.StartsWith(g.Directory.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))?.Target;
    }
}
