using Brisa.Core;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Graphics;
namespace Brisa.WinUI;

internal sealed class DockBehavior : IDisposable
{
    readonly Window window;
    readonly DockSettings settings;
    readonly Func<RectInt32> monitorBounds;
    readonly Func<bool> panelOpen;
    readonly Action hidePanel;
    readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromMilliseconds(200) };
    readonly DispatcherTimer animation = new() { Interval = TimeSpan.FromMilliseconds(16) };
    PointInt32 visible, destination, origin;
    DateTimeOffset animationStart, holdUntil = DateTimeOffset.Now.AddSeconds(2);
    DateTimeOffset explicitRevealUntil;
    bool revealed = true;
    bool fullscreenSuppressed;
    public bool Revealed => revealed;
    public DockBehavior(Window window, DockSettings settings, Func<RectInt32> monitorBounds, Func<bool> panelOpen, Action hidePanel)
    {
        this.window = window; this.settings = settings; this.monitorBounds = monitorBounds; this.panelOpen = panelOpen; this.hidePanel = hidePanel;
        visible = window.AppWindow.Position;
        poll.Tick += (_, _) => Evaluate(); animation.Tick += (_, _) => AnimateFrame(); poll.Start();
    }
    public void Place(PointInt32 position) { visible = position; animation.Stop(); window.AppWindow.Move(revealed ? position : Hidden()); }
    public void Reveal()
    {
        explicitRevealUntil = DateTimeOffset.Now.AddSeconds(8);
        holdUntil = DateTimeOffset.Now.AddSeconds(2);
        if (fullscreenSuppressed) { fullscreenSuppressed = false; window.AppWindow.Show(false); }
        Transition(true);
    }
    PointInt32 Hidden()
    {
        var a = monitorBounds();
        return new(visible.X, settings.Edge == "top" ? a.Y - window.AppWindow.Size.Height + 3 : a.Y + a.Height - 3);
    }
    void Evaluate()
    {
        bool panel = panelOpen();
        bool fullscreen = settings.HideInFullscreen && !panel && DateTimeOffset.Now >= explicitRevealUntil && DesktopState.Fullscreen(monitorBounds());
        if (fullscreen)
        {
            if (!fullscreenSuppressed) { animation.Stop(); hidePanel(); window.AppWindow.Hide(); fullscreenSuppressed = true; }
            return;
        }
        if (fullscreenSuppressed)
        {
            fullscreenSuppressed = false; revealed = true; window.AppWindow.Move(visible); window.AppWindow.Show(false); holdUntil = DateTimeOffset.Now.AddSeconds(1);
        }
        bool over = DesktopState.PointerIn(window.AppWindow.Position, window.AppWindow.Size);
        bool edge = DesktopState.PointerAtEdge(monitorBounds(), settings.Edge, visible.X, window.AppWindow.Size.Width);
        if (over || edge || panel) holdUntil = DateTimeOffset.Now.AddMilliseconds(1100);
        var c = new DockVisibilityContext(settings.AutoHide, settings.HideInFullscreen, false, edge, over, panel, settings.KeepOnDesktop, DesktopState.DesktopActive(), DateTimeOffset.Now < holdUntil);
        Transition(DockVisibilityPolicy.ShouldReveal(c));
    }
    void Transition(bool show)
    {
        if (revealed == show) return;
        revealed = show; origin = window.AppWindow.Position; destination = show ? visible : Hidden();
        if (Ui.ReducedMotion) { animation.Stop(); window.AppWindow.Move(destination); return; }
        animationStart = DateTimeOffset.Now; animation.Start();
    }
    void AnimateFrame()
    {
        double t = Ui.ReducedMotion ? 1 : Math.Clamp((DateTimeOffset.Now - animationStart).TotalMilliseconds / 210, 0, 1);
        double eased = 1 - Math.Pow(1 - t, 3);
        window.AppWindow.Move(new PointInt32((int)Math.Round(origin.X + (destination.X - origin.X) * eased), (int)Math.Round(origin.Y + (destination.Y - origin.Y) * eased)));
        if (t >= 1) animation.Stop();
    }
    public void Dispose() { poll.Stop(); animation.Stop(); }
}
internal static class DesktopState
{
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(nint hwnd, StringBuilder name, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    public static bool PointerIn(PointInt32 p, SizeInt32 size) => GetCursorPos(out var c) && c.X >= p.X && c.X < p.X + size.Width && c.Y >= p.Y && c.Y < p.Y + size.Height;
    public static bool PointerAtEdge(RectInt32 a, string edge, int left, int width)
    {
        if (!GetCursorPos(out var c) || c.X < left || c.X > left + width) return false;
        return edge == "top" ? c.Y >= a.Y && c.Y <= a.Y + 3 : c.Y >= a.Y + a.Height - 4 && c.Y < a.Y + a.Height;
    }
    static string ForegroundClass() { var name = new StringBuilder(128); GetClassName(GetForegroundWindow(), name, 128); return name.ToString(); }
    public static bool DesktopActive() => ForegroundClass() is "Progman" or "WorkerW";
    public static bool Fullscreen(RectInt32 a)
    {
        var hwnd = GetForegroundWindow(); GetWindowThreadProcessId(hwnd, out uint pid);
        if (hwnd == 0 || pid == Environment.ProcessId || ForegroundClass() is "Progman" or "WorkerW" or "Shell_TrayWnd" || !GetWindowRect(hwnd, out var r)) return false;
        bool coversScreen = r.Left <= a.X + 1 && r.Top <= a.Y + 1 && r.Right >= a.X + a.Width - 1 && r.Bottom >= a.Y + a.Height - 1;
        if (!coversScreen) return false;
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            if (process.ProcessName is "StartMenuExperienceHost" or "ShellExperienceHost" or "SearchHost" or "TextInputHost") return false;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
        return true;
    }
}
