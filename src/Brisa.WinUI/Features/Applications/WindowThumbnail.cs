using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
namespace Brisa.WinUI;

internal sealed class WindowThumbnail : Grid
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct Size { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] struct Properties
    {
        public uint Flags; public Rect Destination, Source; public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
    }
    [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);
    [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi.dll")] static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out Size size);
    [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref Properties properties);
    readonly Func<Window?> owner;
    readonly AppWindowInfo source;
    readonly TextBlock placeholder;
    readonly List<ScrollViewer> viewports = [];
    bool viewportChanged;
    void ViewChanged(object? sender, ScrollViewerViewChangedEventArgs args) { viewportChanged = true; Update(); }
    nint thumbnail;
    PreviewRect? last;
    public WindowThumbnail(Func<Window?> owner, AppWindowInfo source)
    {
        this.owner = owner; this.source = source;
        Height = 142; Background = Ui.Brush(Ui.Light ? "#E3E9F1" : "#111824"); CornerRadius = new CornerRadius(10);
        placeholder = Ui.Text("Vista previa no disponible", 11, Ui.MutedColor); placeholder.HorizontalAlignment = HorizontalAlignment.Center; Children.Add(placeholder);
        Loaded += (_, _) => Connect(); Unloaded += (_, _) => Disconnect(); LayoutUpdated += (_, _) => Update();
    }
    void Connect()
    {
        Disconnect(); var window = owner(); if (window == null || !DesktopWindows.MatchesWindow(source)) return;
        if (DwmRegisterThumbnail(WinRT.Interop.WindowNative.GetWindowHandle(window), (nint)source.Handle, out thumbnail) != 0) thumbnail = 0;
        if (window is PanelWindow panel) { viewports.Add(panel.PreviewViewport); panel.PreviewViewport.ViewChanged += ViewChanged; }
        for (DependencyObject? parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(this); parent != null; parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent))
            if (parent is ScrollViewer viewport && !viewports.Contains(viewport)) { viewports.Add(viewport); viewport.ViewChanged += ViewChanged; }
        Update();
    }
    void Disconnect()
    {
        foreach (var viewport in viewports) viewport.ViewChanged -= ViewChanged; viewports.Clear();
        if (thumbnail != 0) DwmUnregisterThumbnail(thumbnail); thumbnail = 0; last = null;
    }
    void Update()
    {
        var window = owner();
        if (thumbnail == 0 || window?.Content is not FrameworkElement root || XamlRoot == null) return;
        if (DwmQueryThumbnailSourceSize(thumbnail, out var size) != 0) { Disconnect(); placeholder.Visibility = Visibility.Visible; return; }
        double dpi = Native.Dpi(window);
        PreviewRect Bounds(FrameworkElement element)
        {
            var rect = element.TransformToVisual(root).TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
            int x = (int)Math.Ceiling(rect.X * dpi), y = (int)Math.Ceiling(rect.Y * dpi);
            return new(x, y, Math.Max(0, (int)Math.Floor(rect.Right * dpi) - x), Math.Max(0, (int)Math.Floor(rect.Bottom * dpi) - y));
        }
        var slot = Bounds(this);
        var visible = new PreviewRect(0, 0, (int)(root.ActualWidth * dpi), (int)(root.ActualHeight * dpi));
        foreach (var viewport in viewports)
        {
            var clip = Bounds(viewport); int x = Math.Max(visible.X, clip.X), y = Math.Max(visible.Y, clip.Y);
            visible = new(x, y, Math.Max(0, Math.Min(visible.X + visible.Width, clip.X + clip.Width) - x), Math.Max(0, Math.Min(visible.Y + visible.Height, clip.Y + clip.Height) - y));
        }
        var fit = WindowPreviewLayout.Fit(size.Width, size.Height, slot, visible);
        if (viewportChanged && Environment.GetCommandLineArgs().Contains("--diagnostics"))
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-thumbnail-scroll.json"), System.Text.Json.JsonSerializer.Serialize(new { Fit = fit, Slot = slot, Viewport = visible, Scrolls = viewports.Select(v => new { v.VerticalOffset, v.HorizontalOffset, v.ZoomFactor }) })); } catch (IOException) { }
        viewportChanged = false;
        if (fit == last) return; last = fit;
        var props = new Properties { Flags = 1 | 4 | 8 | 16, Opacity = 255, Visible = fit.HasValue, ClientOnly = false };
        if (fit is PreviewRect r) props.Destination = new() { Left = r.X, Top = r.Y, Right = r.X + r.Width, Bottom = r.Y + r.Height };
        int result = DwmUpdateThumbnailProperties(thumbnail, ref props); bool success = result == 0;
        if (Environment.GetCommandLineArgs().Contains("--diagnostics"))
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-thumbnail-check.json"), System.Text.Json.JsonSerializer.Serialize(new { Fit = fit, Slot = slot, Viewport = visible, SourceWidth = size.Width, SourceHeight = size.Height, Dpi = dpi, RootWidth = root.ActualWidth, RootHeight = root.ActualHeight, Result = result })); } catch (IOException) { }
        placeholder.Visibility = success && fit.HasValue ? Visibility.Collapsed : Visibility.Visible;
    }
}
