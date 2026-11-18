using Brisa.Core;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
namespace Brisa.WinUI;
internal record ShortcutDetails(string Target, string Arguments = "", string? AppId = null, string? Icon = null);
internal static class ShortcutIdentity
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")] class ShellLink { }
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ILink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, nint data, uint flags);
        void GetIDList(out nint pidl); void SetIDList(nint pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count); void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count); void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count); void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetHotkey(out ushort key); void SetHotkey(ushort key); void GetShowCmd(out int command); void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count, out int index);
    }
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Stamp, ShortcutDetails Details)> cache = new(StringComparer.OrdinalIgnoreCase);
    public static ShortcutDetails Describe(string path)
    {
        if (!File.Exists(path)) return new(path, AppId: AppTargets.AppId(path));
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) return new(path);
        try
        {
            var stamp = File.GetLastWriteTimeUtc(path); if (cache.TryGetValue(path, out var cached) && cached.Stamp == stamp) return cached.Details;
            ShortcutDetails details = new(path);
            if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            {
                if (new FileInfo(path).Length <= 65536)
                {
                    string? url = null, icon = null; bool section = false;
                    foreach (string line in File.ReadLines(path))
                    {
                        string value = line.Trim(); if (value.StartsWith('[')) { section = value.Equals("[InternetShortcut]", StringComparison.OrdinalIgnoreCase); continue; }
                        if (!section) continue;
                        if (value.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)) url = value[4..].Trim();
                        if (value.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase)) icon = Environment.ExpandEnvironmentVariables(value[9..].Trim().Trim('"'));
                    }
                    if (url != null) details = new(url, Icon: icon);
                }
            }
            else
            {
                object? instance = null;
                try
                {
                    instance = new ShellLink(); ((IPersistFile)instance).Load(path, 0); var link = (ILink)instance;
                    var target = new StringBuilder(32768); var arguments = new StringBuilder(32768); var icon = new StringBuilder(32768);
                    link.GetPath(target, target.Capacity, 0, 4); link.GetArguments(arguments, arguments.Capacity); link.GetIconLocation(icon, icon.Capacity, out _);
                    string candidate = Environment.ExpandEnvironmentVariables(target.ToString());
                    string? id = RecentDocuments.AppId(0, path);
                    details = new(candidate.Length == 0 ? path : candidate, arguments.ToString(), id, icon.Length == 0 ? null : Environment.ExpandEnvironmentVariables(icon.ToString()));
                }
                finally { if (instance != null) Marshal.ReleaseComObject(instance); }
            }
            if (cache.Count > 256) cache.Clear(); cache[path] = (stamp, details); return details;
        }
        catch (Exception e) when (e is COMException or IOException or UnauthorizedAccessException or ArgumentException) { return new(path); }
    }
    public static string Resolve(string path)
    {
        var details = Describe(path);
        if (AppTargets.AppId(details.Target) != null) return details.Target;
        if (AppTargets.SteamId(details.Target) is string game) return "steam://rungameid/" + game;
        if (AppLaunchers.Shortcut(path, details) is string updater) return updater;
        if (SteamApplications.Client is string steam && string.Equals(details.Target, steam, StringComparison.OrdinalIgnoreCase))
        {
            var gameMatch = System.Text.RegularExpressions.Regex.Match(details.Arguments, @"^\s*-applaunch\s+(\d+)\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (gameMatch.Success && AppTargets.SteamId("steam://rungameid/" + gameMatch.Groups[1].Value) is string steamGame) return "steam://rungameid/" + steamGame;
        }
        if (details.AppId is string id && (details.Arguments.Length > 0 || !details.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))) return AppTargets.AppsFolder + id;
        if (details.Arguments.Length > 0) return path;
        if (SteamApplications.Client is string client && string.Equals(details.Target, client, StringComparison.OrdinalIgnoreCase)) return "steam://open/main";
        return AppLaunchers.Identity(details.Target) ?? AppLaunchers.Resolve(details.Target);
    }
}
