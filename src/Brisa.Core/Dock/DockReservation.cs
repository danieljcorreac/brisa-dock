namespace Brisa.Core;

public static class DockReservation
{
    public static bool Enabled(bool requested, bool autoHide) => requested && !autoHide;
    public static int Thickness(int dockHeight, double gap, int monitorHeight) =>
        Math.Clamp(dockHeight + (int)Math.Ceiling(Math.Max(0, double.IsFinite(gap) ? gap : 0)) * 2, 1, Math.Max(1, monitorHeight / 3));
    public static PreviewRect IncludeOwnSpace(PreviewRect work, PreviewRect band, bool top)
    {
        if (work.X >= band.X + band.Width || work.X + work.Width <= band.X) return work;
        if (top && Math.Abs(work.Y - (band.Y + band.Height)) <= 2)
            return work with { Y = band.Y, Height = work.Height + work.Y - band.Y };
        if (!top && Math.Abs(work.Y + work.Height - band.Y) <= 2)
            return work with { Height = band.Y + band.Height - work.Y };
        return work;
    }
}
