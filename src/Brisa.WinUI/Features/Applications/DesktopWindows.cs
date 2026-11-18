using Brisa.Core;
using System.Runtime.InteropServices;
using System.Text;
namespace Brisa.WinUI;

internal static class DesktopWindows
{
    delegate bool EnumWindow(nint hwnd, nint state);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback, nint state);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(nint parent, EnumWindow callback, nint state);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(nint hwnd, int command);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("kernel32.dll")] static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref uint length);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetApplicationUserModelId(nint process, ref uint length, StringBuilder? id);
    static string? ProcessAppId(uint pid)
    {
        nint process = OpenProcess(0x1000, false, pid); if (process == 0) return null;
        try { uint length = 0; if (GetApplicationUserModelId(process, ref length, null) != 122 || length > 513) return null; var id = new StringBuilder((int)length); return GetApplicationUserModelId(process, ref length, id) == 0 ? id.ToString() : null; }
        finally { CloseHandle(process); }
    }
    static readonly System.Collections.Concurrent.ConcurrentDictionary<(nint, uint), (DateTimeOffset Stamp, string? Id)> identities = new();
    static string? WindowAppId(nint window, uint pid)
    {
        if (identities.TryGetValue((window, pid), out var found) && DateTimeOffset.Now - found.Stamp < TimeSpan.FromMinutes(1)) return found.Id;
        string? id = RecentDocuments.AppId(window, null); if (identities.Count > 512) identities.Clear(); identities[(window, pid)] = (DateTimeOffset.Now, id); return id;
    }
    static nint lastForeground;
    static string ProcessPath(uint pid)
    {
        var process = OpenProcess(0x1000, false, pid); if (process == 0) return "";
        try { var buffer = new StringBuilder(32768); uint length = (uint)buffer.Capacity; return QueryFullProcessImageName(process, 0, buffer, ref length) ? buffer.ToString() : ""; }
        finally { CloseHandle(process); }
    }
    static string HostedExecutable(nint frame, uint hostPid)
    {
        string path = "";
        EnumChildWindows(frame, (child, _) =>
        {
            GetWindowThreadProcessId(child, out uint pid);
            if (pid == 0 || pid == hostPid || pid == Environment.ProcessId) return true;
            string candidate = ProcessPath(pid);
            if (candidate.Length == 0) return true;
            path = candidate; return false;
        }, 0);
        return path;
    }
    public static nint Foreground
    {
        get
        {
            var hwnd = GetForegroundWindow(); GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != Environment.ProcessId) lastForeground = hwnd;
            return lastForeground;
        }
    }
    public static IReadOnlyList<RunningApp> Read()
    {
        List<AppWindowInfo> windows = [];
        Dictionary<uint, string> paths = [];
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == Environment.ProcessId || pid == 0) return true;
            long style = GetWindowLongPtr(hwnd, -20).ToInt64();
            if ((style & 0x80) != 0 || (GetWindow(hwnd, 4) != 0 && (style & 0x40000) == 0)) return true;
            if (DwmGetWindowAttribute(hwnd, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
            var title = new StringBuilder(1024); if (GetWindowText(hwnd, title, title.Capacity) == 0) return true;
            if (!paths.TryGetValue(pid, out string? path))
            {
                path = ""; var process = OpenProcess(0x1000, false, pid);
                if (process != 0)
                {
                    try { var buffer = new StringBuilder(32768); uint length = (uint)buffer.Capacity; if (QueryFullProcessImageName(process, 0, buffer, ref length)) path = buffer.ToString(); }
                    finally { CloseHandle(process); }
                }
                paths[pid] = path;
            }
            if (path.Length == 0) return true;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            string? host = null; string? packageId = ProcessAppId(pid); string? appId = packageId ?? WindowAppId(hwnd, pid);
            if (name.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
            {
                host = path; path = HostedExecutable(hwnd, pid);
                EnumChildWindows(hwnd, (child, _) => { GetWindowThreadProcessId(child, out uint childPid); if (ProcessPath(childPid) == path) { packageId = ProcessAppId(childPid); appId = packageId ?? appId; } return appId == null; }, 0); if (path.Length == 0) return true;
                name = System.IO.Path.GetFileNameWithoutExtension(path);
            }
            if (name is "StartMenuExperienceHost" or "ShellExperienceHost" or "SearchHost" or "TextInputHost") return true;
            if (name.Equals("explorer", StringComparison.OrdinalIgnoreCase) && title.ToString() == "Program Manager") return true;
            string friendly = name.ToLowerInvariant() switch { "explorer" => "Explorador de archivos", "chrome" => "Google Chrome", "msedge" => "Microsoft Edge", "claude" => "Claude", "steamwebhelper" => "Steam", _ => name };
            if (appId?.StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) == true) friendly = "Codex";
            windows.Add(new(hwnd.ToInt64(), title.ToString(), path, friendly, host, appId, SteamApplications.LaunchTarget(path) ?? (packageId == null ? AppLaunchers.PreferredTarget(path) : AppTargets.AppsFolder + packageId), AppLaunchers.Identity(path))); return true;
        }, 0);
        return RunningApps.Group(windows);
    }
    [DllImport("user32.dll")] static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
    public static bool MatchesWindow(AppWindowInfo window)
    {
        nint hwnd = (nint)window.Handle; if (!IsWindow(hwnd)) return false;
        GetWindowThreadProcessId(hwnd, out uint pid); var process = OpenProcess(0x1000, false, pid); if (process == 0) return false;
        try
        {
            var path = new StringBuilder(32768); uint length = (uint)path.Capacity;
            if (!QueryFullProcessImageName(process, 0, path, ref length) || RunningApps.Key(path.ToString()) != RunningApps.Key(window.HostExecutable ?? window.Executable)) return false;
            return window.HostExecutable == null || RunningApps.Key(HostedExecutable(hwnd, pid)) == RunningApps.Key(window.Executable);
        }
        finally { CloseHandle(process); }
    }
    public static bool RequestClose(AppWindowInfo window) => MatchesWindow(window) && PostMessage((nint)window.Handle, 0x10, 0, 0);
    public static bool Activate(AppWindowInfo window, bool toggle, nint expectedForeground)
    {
        nint hwnd = (nint)window.Handle; if (!MatchesWindow(window)) return false;
        if (toggle && hwnd == expectedForeground && !IsIconic(hwnd)) { ShowWindowAsync(hwnd, 6); return true; }
        if (IsIconic(hwnd)) ShowWindowAsync(hwnd, 9);
        return SetForegroundWindow(hwnd);
    }
}
