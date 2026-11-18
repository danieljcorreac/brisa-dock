namespace Brisa.Core;

public sealed class PanelAnchor
{
    PreviewRect? surface;
    double scale;
    double? center;

    public PreviewRect Place(PreviewRect work, PreviewRect dock, double effectiveScale, bool wide, double desiredHeight,
        double? requestedCenter, bool visible, bool newInteraction)
    {
        bool begin = !visible || newInteraction || surface != work || Math.Abs(scale - effectiveScale) > .001;
        if (begin) center = requestedCenter;
        var bounds = PanelLayout.Fit(work, dock, effectiveScale, wide, desiredHeight, center);
        if (begin) center = bounds.X + bounds.Width / 2d;
        surface = work; scale = effectiveScale;
        return bounds;
    }
}
