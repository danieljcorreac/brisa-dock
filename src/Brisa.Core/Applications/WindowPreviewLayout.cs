namespace Brisa.Core;
public readonly record struct PreviewRect(int X, int Y, int Width, int Height);
public static class WindowPreviewLayout
{
    public static PreviewRect? Fit(int sourceWidth, int sourceHeight, PreviewRect slot, PreviewRect viewport)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || slot.Width <= 0 || slot.Height <= 0) return null;
        double scale = Math.Min((double)slot.Width / sourceWidth, (double)slot.Height / sourceHeight);
        int width = Math.Max(1, (int)Math.Round(sourceWidth * scale)); int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        var result = new PreviewRect(slot.X + (slot.Width - width) / 2, slot.Y + (slot.Height - height) / 2, width, height);
        // Do not let a native thumbnail paint outside its XAML viewport while scrolling.
        return result.X >= viewport.X && result.Y >= viewport.Y && (long)result.X + result.Width <= (long)viewport.X + viewport.Width && (long)result.Y + result.Height <= (long)viewport.Y + viewport.Height ? result : null;
    }
}
