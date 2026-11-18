namespace Brisa.Core;
public readonly record struct DragSlot(string Id, double X, double Width);
public static class WidgetDragMath
{
    public static int Destination(IReadOnlyList<DragSlot> slots, string source, double center, int current, double margin = 6)
    {
        if (slots.Count == 0) return 0;
        current = Math.Clamp(current, 0, slots.Count - 1);
        if (!double.IsFinite(center) || !slots.Any(s => s.Id == source) || slots.Any(s => !double.IsFinite(s.X) || !double.IsFinite(s.Width) || s.Width <= 0)) return current;
        int Before(double at) => slots.Count(s => s.Id != source && at > s.X + s.Width / 2);
        int next = Before(center);
        if (next != current) next = Before(center + (next > current ? -margin : margin));
        return Math.Clamp(next, 0, slots.Count - 1);
    }
    public static double ScrollOffset(double pointer, double width, double offset, double maximum, double seconds)
    {
        if (!double.IsFinite(pointer) || !double.IsFinite(width) || !double.IsFinite(offset) || !double.IsFinite(maximum) || !double.IsFinite(seconds) || width <= 0 || maximum <= 0) return offset;
        double edge = Math.Min(44, width / 3);
        double speed = pointer < edge ? -Math.Clamp((edge - pointer) / edge, 0, 1) : pointer > width - edge ? Math.Clamp((pointer - width + edge) / edge, 0, 1) : 0;
        return Math.Clamp(offset + speed * 625 * Math.Clamp(seconds, 0, .05), 0, maximum);
    }
}
