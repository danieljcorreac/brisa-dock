using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
namespace Brisa.WinUI;
internal static class WindowDiagnostics
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    static DateTimeOffset last;
    public static void Write(Window window, FrameworkElement content, PanelWindow? panel = null, int runningJobs = 0)
    {
        if (!Environment.GetCommandLineArgs().Contains("--diagnostics") || DateTimeOffset.Now - last < TimeSpan.FromSeconds(2)) return;
        last = DateTimeOffset.Now;
        try
        {
            nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            GetWindowRect(hwnd, out var rect); DwmGetWindowAttribute(hwnd, 14, out int cloaked, 4);
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            var state = new { BackgroundJobs = runningJobs, ManagedBytes = GC.GetTotalMemory(false), PrivateBytes = process.PrivateMemorySize64, WorkingBytes = process.WorkingSet64, Process = Environment.ProcessId, Handle = hwnd.ToInt64(), Visible = window.AppWindow.IsVisible, NativeVisible = IsWindowVisible(hwnd), Switchers = window.AppWindow.IsShownInSwitchers, Cloaked = cloaked, Topmost = (GetWindowLongPtr(hwnd, -20).ToInt64() & 8) != 0, ExStyle = GetWindowLongPtr(hwnd, -20).ToInt64().ToString("X"), Style = GetWindowLongPtr(hwnd, -16).ToInt64().ToString("X"), ManagedTopmost = ((Microsoft.UI.Windowing.OverlappedPresenter)window.AppWindow.Presenter).IsAlwaysOnTop, X = rect.Left, Y = rect.Top, Width = rect.Right - rect.Left, Height = rect.Bottom - rect.Top, ContentWidth = content.ActualWidth, ContentHeight = content.ActualHeight, Dpi = Native.Dpi(window), Panel = panel?.AppWindow.IsVisible == true ? new { panel.PageId, Position = panel.AppWindow.Position, Size = panel.AppWindow.Size } : null, Monitors = DockMonitors.Read().Select(m => new { m.Primary, m.Bounds, m.WorkArea }) };
            System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "brisa-window-diagnostics.json"), System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }));
        }
        catch { }
    }
}
