namespace Brisa.Core;

public static class TrayMenuLayout
{
    // Screen coordinates throughout: the dock's position is deliberately not an input.
    public static PreviewRect Fit(PreviewRect work, PreviewRect anchor, double scale, double logicalWidth, double logicalHeight)
    {
        if (!double.IsFinite(scale) || scale <= 0) scale = 1;
        int margin = Math.Min(10, Math.Max(0, (Math.Min(work.Width, work.Height) - 1) / 2));
        int width = Math.Clamp((int)Math.Ceiling(logicalWidth * scale), 1, Math.Max(1, work.Width - margin * 2));
        int height = Math.Clamp((int)Math.Ceiling(logicalHeight * scale), 1, Math.Max(1, work.Height - margin * 2));
        int gap = Math.Max(6, (int)Math.Round(6 * scale));
        int above = anchor.Y - gap - height;
        int below = anchor.Y + anchor.Height + gap;
        bool fitsAbove = above >= work.Y + margin;
        bool fitsBelow = below + height <= work.Y + work.Height - margin;
        int y = fitsAbove ? above : fitsBelow ? below : anchor.Y - work.Y > work.Y + work.Height - anchor.Y - anchor.Height ? above : below;
        int x = Math.Clamp(anchor.X + anchor.Width - width, work.X + margin, Math.Max(work.X + margin, work.X + work.Width - margin - width));
        y = Math.Clamp(y, work.Y + margin, Math.Max(work.Y + margin, work.Y + work.Height - margin - height));
        return new(x, y, width, height);
    }
}
