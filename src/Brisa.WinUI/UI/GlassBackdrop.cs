using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
namespace Brisa.WinUI;

// Custom acrylic uses the compositor's desktop blur rather than a translucent solid fill.
internal sealed class GlassBackdrop(bool light, double tintStrength, string baseColor) : SystemBackdrop
{
    DesktopAcrylicController? controller;
    SystemBackdropConfiguration? configuration;
    public bool Matches(bool isLight, string color) => light == isLight && baseColor == color;
    public void UpdateTint(double strength) { tintStrength = Math.Clamp(strength, .1, .8); if (controller != null) controller.TintOpacity = (float)tintStrength; }
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot root)
    {
        base.OnTargetConnected(target, root);
        if (!DesktopAcrylicController.IsSupported()) return;
        controller = new DesktopAcrylicController();
        configuration = GetDefaultSystemBackdropConfiguration(target, root); configuration.IsInputActive = true;
        controller.SetSystemBackdropConfiguration(configuration);
        controller.AddSystemBackdropTarget(target);
        controller.TintColor = Ui.Color(baseColor);
        controller.TintOpacity = (float)tintStrength;
        controller.LuminosityOpacity = light ? .2f : .12f;
        controller.FallbackColor = Ui.Color(baseColor);
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        base.OnTargetDisconnected(target);
        if (controller != null) { controller.RemoveSystemBackdropTarget(target); controller.Dispose(); controller = null; }
        configuration = null;
    }
    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot root)
    {
        // A dock keeps its material when another app has focus. Do not resolve a
        // detached target: queued notifications can outlive its controller.
        if (configuration != null) configuration.IsInputActive = true;
    }
}
