namespace Brisa.Core;

public static class ShortcutDragLayout
{
    public static int Destination(int from, int slot, int count) =>
        from < 0 || from >= count || slot < 0 || slot > count ? -1 : Math.Clamp(slot > from ? slot - 1 : slot, 0, count - 1);

    // One 60 ms step. Only scroll while the pointer remains inside the viewport.
    public static double ScrollStep(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || width <= 0 || height <= 0 || x < 0 || x > width || y < 0 || y > height) return 0;
        double edge = Math.Min(28, height / 3);
        return y < edge ? -14 * (1 - y / edge) : y > height - edge ? 14 * (1 - (height - y) / edge) : 0;
    }
}
