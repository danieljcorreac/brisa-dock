using Brisa.Core;
using System.Runtime.InteropServices;

namespace Brisa.WinUI;

internal static class ContrastColors
{
    [DllImport("user32.dll")] static extern uint GetSysColor(int index);
    static string Read(int index) { uint c = GetSysColor(index); return $"#{c & 255:X2}{c >> 8 & 255:X2}{c >> 16 & 255:X2}"; }
    [StructLayout(LayoutKind.Sequential)] struct Contrast { public uint Size, Flags; public nint Scheme; }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")] static extern bool SystemParametersInfo(uint action, uint size, ref Contrast value, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")] static extern bool SystemParametersInfo(uint action, uint size, ref int value, uint flags);
    public static bool Enabled { get { var value = new Contrast { Size = (uint)Marshal.SizeOf<Contrast>() }; return SystemParametersInfo(0x42, value.Size, ref value, 0) && (value.Flags & 1) != 0; } }
    public static bool AnimationsEnabled { get { int value = 1; return !SystemParametersInfo(0x1042, 0, ref value, 0) || value != 0; } }
    public static string Background => Read(5);
    public static string Foreground => Read(8);
    public static string Highlight => Read(13);
    public static string HighlightText => Read(14);
    public static bool Light => (GetSysColor(5) & 255) + (GetSysColor(5) >> 8 & 255) + (GetSysColor(5) >> 16 & 255) > 382;
}
public sealed partial class MainWindow
{
    string systemAccessibilitySignature = "";
    string SystemAccessibilitySignature() => $"{settings.FollowSystemAccessibility}:{settings.ReducedMotion}:{settings.Material}:{ContrastColors.Enabled}:{ContrastColors.AnimationsEnabled}:{ContrastColors.Background}:{ContrastColors.Foreground}:{ContrastColors.Highlight}:{ContrastColors.HighlightText}";
    void RefreshSystemAccessibility() => DispatcherQueue.TryEnqueue(() =>
    {
        if (closing || SystemAccessibilitySignature() == systemAccessibilitySignature) return;
        ApplyPreferences(); SetSurface(); BuildDock(); panel?.RefreshMaterial();
    });
    void ApplyAccessibilityPreferences()
    {
        bool animations = true, contrast = false;
        animations = ContrastColors.AnimationsEnabled; contrast = ContrastColors.Enabled;
        if (Environment.GetCommandLineArgs().Contains("--check-visual") && Environment.GetCommandLineArgs().Contains("--review-contrast")) contrast = true;
        var mode = AccessibilityPolicy.Resolve(settings.ReducedMotion, settings.FollowSystemAccessibility, animations, contrast, settings.Material == "translucent");
        Ui.HighContrast = mode.HighContrast; Ui.ReducedMotion = mode.ReducedMotion; Ui.Translucent = mode.Translucent;
        systemAccessibilitySignature = SystemAccessibilitySignature();
    }
}
