namespace Brisa.Core;

public readonly record struct DockVisibilityContext(bool AutoHide, bool HideFullscreen, bool Fullscreen, bool PointerAtEdge, bool PointerOverDock, bool PanelOpen, bool KeepOnDesktop, bool DesktopActive, bool HoldOpen);
public static class DockVisibilityPolicy
{
    public static bool ShouldReveal(DockVisibilityContext c)
    {
        if (c.HideFullscreen && c.Fullscreen) return false;
        if (!c.AutoHide) return true;
        return c.PointerAtEdge || c.PointerOverDock || c.PanelOpen || c.HoldOpen || (c.KeepOnDesktop && c.DesktopActive);
    }
}
public static class CalendarProgress
{
    public static DateTime GridStart(DateTime month)
    {
        var first = new DateTime(month.Year, month.Month, 1);
        return first.AddDays(-(((int)first.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));
    }
    public static double Day(DateTime now) => now.TimeOfDay.TotalSeconds / TimeSpan.FromDays(1).TotalSeconds * 100;
    public static double Month(DateTime now) => (now.Day - 1 + now.TimeOfDay.TotalDays) / DateTime.DaysInMonth(now.Year, now.Month) * 100;
    public static double Year(DateTime now) => (now - new DateTime(now.Year, 1, 1)).TotalSeconds / (new DateTime(now.Year + 1, 1, 1) - new DateTime(now.Year, 1, 1)).TotalSeconds * 100;
}
public sealed class Countdown
{
    TimeSpan remaining = TimeSpan.FromMinutes(5);
    DateTimeOffset? deadline;
    public bool Running => deadline.HasValue;
    public TimeSpan Remaining(DateTimeOffset now) => deadline.HasValue ? (deadline.Value > now ? deadline.Value - now : TimeSpan.Zero) : remaining;
    public void Set(TimeSpan value) { remaining = value < TimeSpan.Zero ? TimeSpan.Zero : value; deadline = null; }
    public void Start(DateTimeOffset now) { if (!Running && remaining > TimeSpan.Zero) deadline = now + remaining; }
    public void Pause(DateTimeOffset now) { remaining = Remaining(now); deadline = null; }
    public bool CompleteIfDue(DateTimeOffset now) { if (!deadline.HasValue || now < deadline.Value) return false; remaining = TimeSpan.Zero; deadline = null; return true; }
}
