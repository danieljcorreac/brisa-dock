using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
namespace Brisa.WinUI;
internal static class Native
{
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern nint GetWindow(nint hwnd, uint command);
    public static bool IsForeground(Window window) => GetForegroundWindow() == WinRT.Interop.WindowNative.GetWindowHandle(window);
    public static bool ForegroundOwnedBy(Window window)
    {
        nint foreground = GetForegroundWindow(), owner = WinRT.Interop.WindowNative.GetWindowHandle(window);
        for (int depth = 0; foreground != 0 && depth < 16; depth++, foreground = GetWindow(foreground, 4))
            if (foreground == owner) return true;
        return false;
    }
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref uint value, int size);
    [StructLayout(LayoutKind.Sequential)] struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);
    public static double Dpi(Window window) => GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window)) / 96d;
    public static void SetAppIcon(Window window)
    {
        string icon = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (System.IO.File.Exists(icon)) window.AppWindow.SetIcon(icon);
    }
    public static void EnsureZOrder(Window window, bool topmost)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (((GetWindowLongPtr(hwnd, -20).ToInt64() & 8) != 0) == topmost) return;
        var presenter = (OverlappedPresenter)window.AppWindow.Presenter;
        presenter.IsAlwaysOnTop = !topmost;
        presenter.IsAlwaysOnTop = topmost;
        bool changed = SetWindowPos(hwnd, topmost ? new nint(-1) : new nint(-2), 0, 0, 0, 0, 0x13);
        if (Environment.GetCommandLineArgs().Contains("--diagnostics"))
            System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "brisa-zorder-diagnostics.json"), System.Text.Json.JsonSerializer.Serialize(new { Changed = changed, Error = Marshal.GetLastWin32Error(), ExStyle = GetWindowLongPtr(hwnd, -20).ToInt64().ToString("X") }));
    }
    public static void Style(Window window, bool topmost = true)
    {
        var presenter = (OverlappedPresenter)window.AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = false; presenter.IsMaximizable = false; presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = topmost;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WindowSurface.Configure(hwnd);
        uint noBorder = 0xFFFFFFFE; DwmSetWindowAttribute(hwnd, 34, ref noBorder, 4);
        uint dark = Ui.Light ? 0u : 1u; DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
        uint corners = 2; DwmSetWindowAttribute(hwnd, 33, ref corners, 4);
        var margins = new Margins(); DwmExtendFrameIntoClientArea(hwnd, ref margins);
        SetWindowPos(hwnd, topmost ? -1 : -2, 0, 0, 0, 0, 0x33);
    }
    public static void Round(Window window, int width, int height, double radius = 24)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        uint corners = 2; DwmSetWindowAttribute(hwnd, 33, ref corners, 4);
        WindowSurface.Configure(hwnd);
        uint noBorder = 0xFFFFFFFE; DwmSetWindowAttribute(hwnd, 34, ref noBorder, 4);
    }
    public static RectInt32 WorkArea(Window window) => DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
    public static void Resize(Window window, int width, int height) { window.AppWindow.Resize(new SizeInt32(width, height)); Round(window, width, height); }
}
internal sealed class SystemStats
{
    [StructLayout(LayoutKind.Sequential)] struct Memory { public uint Length, Load; public ulong Total, Available, PageTotal, PageAvailable, VirtualTotal, VirtualAvailable, Extended; }
    [StructLayout(LayoutKind.Sequential)] struct Power { public byte AC, Flags, Percent, Saver; public uint Life, FullLife; }
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref Memory memory);
    [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out Power power);
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    long previousIdle, previousTotal, lastCpuSample;
    public double Cpu { get; private set; }
    public double MemoryPercent { get; private set; }
    public double TotalGb { get; private set; }
    public double UsedGb { get; private set; }
    public byte? Battery { get; private set; }
    public bool Plugged { get; private set; }
    public bool Charging { get; private set; }
    public bool BatterySaver { get; private set; }
    public bool PowerAvailable { get; private set; }
    public TimeSpan? BatteryRemaining { get; private set; }
    public string ProcessorName { get; } = ReadProcessorName();
    static string ReadProcessorName()
    {
        try { return Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", null)?.ToString()?.Trim() ?? "Procesador"; }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException) { return "Procesador"; }
    }
    public void Read()
    {
        var m = new Memory { Length = (uint)Marshal.SizeOf<Memory>() };
        if (GlobalMemoryStatusEx(ref m)) { MemoryPercent = m.Load; TotalGb = m.Total / 1073741824d; UsedGb = (m.Total - m.Available) / 1073741824d; }
        long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
        if ((lastCpuSample == 0 || System.Diagnostics.Stopwatch.GetElapsedTime(lastCpuSample, stamp) >= TimeSpan.FromMilliseconds(500)) && GetSystemTimes(out var idle, out var kernel, out var user)) { lastCpuSample = stamp; var total = kernel + user; var delta = total - previousTotal; if (previousTotal > 0 && delta > 0) Cpu = Math.Clamp(100d * (delta - (idle - previousIdle)) / delta, 0, 100); previousIdle = idle; previousTotal = total; }
        PowerAvailable = GetSystemPowerStatus(out var p);
        if (PowerAvailable)
        {
            Battery = (p.Flags & 128) == 0 && p.Percent <= 100 ? p.Percent : null; Plugged = p.AC == 1;
            Charging = Battery.HasValue && (p.Flags & 8) != 0; BatterySaver = p.Saver == 1;
            BatteryRemaining = Battery.HasValue && !Plugged && p.Life != uint.MaxValue ? TimeSpan.FromSeconds(p.Life) : null;
        }
        else { Battery = null; BatteryRemaining = null; Charging = false; }
    }
}
