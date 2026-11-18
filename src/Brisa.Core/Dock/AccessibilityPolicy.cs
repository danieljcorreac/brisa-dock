namespace Brisa.Core;
public readonly record struct AccessibilityMode(bool ReducedMotion, bool HighContrast, bool Translucent);
public static class AccessibilityPolicy
{
    public static AccessibilityMode Resolve(bool reduceMotion, bool followWindows, bool animationsEnabled, bool highContrast, bool translucent) =>
        new(reduceMotion || followWindows && !animationsEnabled, followWindows && highContrast, translucent && !(followWindows && highContrast));
}
