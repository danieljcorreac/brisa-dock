namespace Brisa.Core;
public static class PanelLayout
{
    public static int Width(int workWidth, double effectiveScale, bool wide) => Math.Clamp((int)Math.Round((wide ? 760 : 410) * effectiveScale), 1, Math.Max(1, workWidth - 24));
    public static PreviewRect Fit(PreviewRect work, PreviewRect dock, double effectiveScale, bool wide, double desiredHeight, double? anchorX = null)
    {
        int width = Width(work.Width, effectiveScale, wide);
        int above = Math.Max(1, dock.Y - work.Y - 24);
        int below = Math.Max(1, work.Y + work.Height - dock.Y - dock.Height - 24);
        bool placeAbove = above >= below;
        int available = Math.Min(Math.Max(1, work.Height - 24), Math.Max(above, below));
        int height = Math.Clamp((int)Math.Ceiling(Math.Min(750, Math.Max(200, desiredHeight)) * effectiveScale), 1, available);
        int x = (int)Math.Round((anchorX ?? dock.X + dock.Width - width / 2d) - width / 2d);
        x = Math.Clamp(x, work.X + 12, Math.Max(work.X + 12, work.X + work.Width - width - 12));
        int y = placeAbove ? dock.Y - height - 12 : dock.Y + dock.Height + 12;
        y = Math.Clamp(y, work.Y + 12, Math.Max(work.Y + 12, work.Y + work.Height - height - 12));
        return new(x, y, width, height);
    }
}
