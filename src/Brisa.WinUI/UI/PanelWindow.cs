using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using Brisa.Core;
namespace Brisa.WinUI;
internal sealed class PanelWindow : Window
{
    readonly Border shell;
    readonly ScrollViewer scroll;
    readonly Grid bodyHost;
    readonly Grid layout;
    readonly Grid header;
    readonly TextBlock heading;
    readonly TextBlock subtitle;
    readonly Button close;
    readonly Button back;
    Action? backAction;
    readonly Border identity;
    readonly DispatcherTimer dismissTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    Microsoft.UI.Xaml.Media.Animation.Storyboard? exitAnimation;
    readonly DispatcherTimer resizeTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    PreviewRect resizeFrom, resizeTo;
    long resizeStarted;
    double resizeScale;
    bool closed;
    int contentRevision;
    bool backdropLight = Ui.Light;
    bool backdropTranslucent = Ui.Translucent;
    double backdropTint = Ui.TintStrength;
    string backdropColor = Ui.BaseColor;
    bool lastWide;
    RectInt32? lastArea;
    double lastScale;
    int? lastDockY;
    int lastDockHeight;
    readonly PanelAnchor navigationAnchor = new();
    internal ScrollViewer PreviewViewport => scroll;
    public string PageId { get; private set; } = "";
    public event Action? Dismissed;
    public PanelWindow(Action<UIElement>? configureKeyboard = null)
    {
        Title = "Brisa · Tu espacio"; AppWindow.IsShownInSwitchers = false; Native.SetAppIcon(this); if (Ui.Translucent) SystemBackdrop = new GlassBackdrop(Ui.Light, Ui.TintStrength, Ui.BaseColor);
        layout = new Grid(); layout.RowDefinitions.Add(new() { Height = GridLength.Auto }); layout.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        header = new Grid { Padding = new Thickness(24, 24, 24, 16), ColumnSpacing = 12 }; header.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var titles = Ui.Stack(3); heading = Ui.Text("", 21, bold: true); subtitle = Ui.Text("", 10, Ui.MutedColor); titles.Children.Add(heading); titles.Children.Add(subtitle);
        var headingRow = new Grid { ColumnSpacing = 11 }; headingRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); headingRow.ColumnDefinitions.Add(new());
        identity = new Border { Width = 36, Height = 36, CornerRadius = new(11), Background = Ui.Brush(Ui.Surface), Child = Ui.Glyph("\uE8A9", 18, Ui.Accent) }; headingRow.Children.Add(identity); Grid.SetColumn(titles, 1); headingRow.Children.Add(titles); header.Children.Add(headingRow);
        back = Ui.Button(Ui.Glyph("\uE72B", 16), "Volver", () => backAction?.Invoke()); back.Width = 36; back.Height = 36; back.Padding = new Thickness(8); back.Visibility = Visibility.Collapsed; headingRow.Children.Add(back);
        heading.MaxLines = 2; heading.TextTrimming = TextTrimming.CharacterEllipsis;
        close = Ui.Button(Ui.Glyph("\uE8BB", 14), "Cerrar panel", Dismiss); close.Padding = new Thickness(9); close.VerticalAlignment = VerticalAlignment.Top; Grid.SetColumn(close, 1); header.Children.Add(close); layout.Children.Add(header);
        bodyHost = new Grid { Padding = new Thickness(24, 0, 24, 24) };
        scroll = new ScrollViewer { Content = bodyHost, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        shell = new Border { CornerRadius = new CornerRadius(0), BorderThickness = new Thickness(0), Background = Ui.Brush(Ui.PanelBackground), RequestedTheme = Ui.Light ? ElementTheme.Light : ElementTheme.Dark, Child = new Viewbox { Child = layout, Stretch = Stretch.Fill } };
        shell.TabFocusNavigation = Microsoft.UI.Xaml.Input.KeyboardNavigationMode.Cycle;
        Content = shell;
        configureKeyboard?.Invoke(shell);
        var goBack = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = VirtualKey.Left, Modifiers = VirtualKeyModifiers.Menu };
        goBack.Invoked += (_, e) => { if (backAction != null) { e.Handled = true; backAction(); } }; shell.KeyboardAccelerators.Add(goBack);
        shell.KeyDown += (_, e) => { if (e.Key == VirtualKey.Escape) { e.Handled = true; Dismiss(); } }; Native.Style(this);
        dismissTimer.Tick += (_, _) => { dismissTimer.Stop(); AppWindow.Hide(); Dismissed?.Invoke(); };
        resizeTimer.Tick += (_, _) => {
            double t = Ui.ReducedMotion ? 1 : Math.Clamp(System.Diagnostics.Stopwatch.GetElapsedTime(resizeStarted).TotalMilliseconds / 150, 0, 1); double eased = 1 - Math.Pow(1 - t, 3);
            int Mix(int a, int b) => (int)Math.Round(a + (b - a) * eased);
            ApplyBounds(new(Mix(resizeFrom.X, resizeTo.X), Mix(resizeFrom.Y, resizeTo.Y), Mix(resizeFrom.Width, resizeTo.Width), Mix(resizeFrom.Height, resizeTo.Height)), resizeScale);
            if (t >= 1) resizeTimer.Stop();
        };
        Closed += (_, _) => { closed = true; dismissTimer.Stop(); resizeTimer.Stop(); exitAnimation?.Stop(); };
    }
    void Dismiss()
    {
        if (dismissTimer.IsEnabled) return;
        resizeTimer.Stop();
        if (Ui.ReducedMotion) { AppWindow.Hide(); Dismissed?.Invoke(); return; }
        exitAnimation = Ui.Animate(layout, "Opacity", 0, 90); dismissTimer.Start();
    }
    void ApplyBounds(PreviewRect bounds, double scale)
    {
        layout.Width = bounds.Width / scale; layout.Height = bounds.Height / scale;
        Native.Resize(this, bounds.Width, bounds.Height); AppWindow.Move(new PointInt32(bounds.X, bounds.Y));
    }
    static string PageGlyph(string id) => id switch {
        "claude" or "chatgpt" or "ai-limits" => "\uE99A", "settings" or "dock-settings" or "layout-preferences" => "\uE713",
        "appearance" => "\uE790", "order" => "\uE70F", "calendar" => "\uE787", "tasks" => "\uE73E", "notes" => "\uE70B",
        "sound" => "\uE767", "music" => "\uE8D6", "weather" => "\uE753", "timer" or "stopwatch" or "focus" => "\uE916",
        "system" => "\uE9D9", "network" => "\uE839", "storage" => "\uEDA2", "battery" => "\uE83F", "world-time" => "\uE774",
        "day-progress" or "month-progress" or "year-progress" => "\uE787", "search" => "\uE721", "recycle-bin" => "\uE74D",
        "backups" or "backup-preview" or "backup-recovery" => "\uE74E", "windows-settings" => "\uE770",
        "task-editor" => "\uE70F", "event-editor" or "calendar-series" or "calendar-exceptions" or "event-archive" or "calendar-import" => "\uE787",
        "apps-manager" or "app-group" or "group-editor" or "recent-documents" => "\uE8B7", "quota-history" or "claude-setup" or "claude-direct-consent" => "\uE99A",
        "accessibility" => "\uE776", _ => id.StartsWith("folder-stack-") ? "\uE8B7" : "\uE8A9" };
    public void RefreshMaterial()
    {
        if (closed) return;
        bool translucent = Ui.Translucent;
        if (backdropLight != Ui.Light || backdropTranslucent != translucent || backdropTint != Ui.TintStrength || backdropColor != Ui.BaseColor)
        {
            if (translucent && SystemBackdrop is GlassBackdrop existing && existing.Matches(Ui.Light, Ui.BaseColor)) existing.UpdateTint(Ui.TintStrength);
            else SystemBackdrop = translucent ? new GlassBackdrop(Ui.Light, Ui.TintStrength, Ui.BaseColor) : null;
            backdropLight = Ui.Light; backdropTranslucent = translucent; backdropTint = Ui.TintStrength; backdropColor = Ui.BaseColor;
        }
        shell.RequestedTheme = Ui.Light ? ElementTheme.Light : ElementTheme.Dark; shell.Background = Ui.Brush(translucent ? Ui.PanelBackground : Ui.BaseColor);
        identity.Child = Ui.Glyph(PageGlyph(PageId ?? ""), 18, Ui.Accent); identity.Background = Ui.Brush(Ui.Surface);
        heading.Foreground = Ui.Brush(Ui.TextColor); subtitle.Foreground = Ui.Brush(Ui.MutedColor);
        close.Content = Ui.Glyph("\uE8BB", 14); close.Background = Ui.Brush(Ui.Surface);
        back.Content = Ui.Glyph("\uE72B", 16); back.Background = Ui.Brush(Ui.Surface);
    }
    public void SetBackNavigation(Action? action, string label)
    {
        backAction = action;
        back.Visibility = action == null ? Visibility.Collapsed : Visibility.Visible;
        identity.Visibility = action == null ? Visibility.Visible : Visibility.Collapsed;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(back, label); ToolTipService.SetToolTip(back, label);
    }
    public void ShowPage(string id, string title, string description, UIElement content, Window dock, RectInt32 area, bool wide = false, double? anchorX = null, double userScale = 1, bool newInteraction = false)
    {
        if (closed) return;
        bool replacement = bodyHost.Children.FirstOrDefault() != content;
        int revision = ++contentRevision;
        bool entering = !AppWindow.IsVisible || PageId != id;
        dismissTimer.Stop(); resizeTimer.Stop(); exitAnimation?.Stop(); layout.Opacity = 1;
        double effectiveScale = Native.Dpi(dock) * Math.Clamp(userScale, .75, 1.25);
        bool preserveFrame = !entering && !newInteraction && lastWide == wide && lastArea == area && Math.Abs(lastScale - effectiveScale) < .001 && lastDockY == dock.AppWindow.Position.Y && lastDockHeight == dock.AppWindow.Size.Height;
        var previousPosition = AppWindow.Position; var previousSize = AppWindow.Size;
        lastWide = wide; lastArea = area; lastScale = effectiveScale;
        lastDockY = dock.AppWindow.Position.Y; lastDockHeight = dock.AppWindow.Size.Height;
        double previousOffset = entering ? 0 : scroll.VerticalOffset;
        PageId = id; Title = title + " · Brisa"; heading.Text = title;
        subtitle.Text = Ui.Context(description);
        subtitle.Visibility = string.Equals(title, description, StringComparison.CurrentCultureIgnoreCase) ? Visibility.Collapsed : Visibility.Visible;
        identity.Child = Ui.Glyph(PageGlyph(id), 18, Ui.Accent); identity.Background = Ui.Brush(Ui.Surface);
        heading.Foreground = Ui.Brush(Ui.TextColor); subtitle.Foreground = Ui.Brush(Ui.MutedColor);
        close.Content = Ui.Glyph("\uE8BB", 14); close.Background = Ui.Brush(Ui.Surface);
        RefreshMaterial();
        if (replacement) { bodyHost.Children.Clear(); bodyHost.Children.Add(content); }
        var currentArea = Native.WorkArea(this);
        if (currentArea.X != area.X || currentArea.Y != area.Y)
            AppWindow.Move(new PointInt32(area.X + 24, area.Y + 24));
        double logicalWidth = PanelLayout.Width(area.Width, effectiveScale, wide) / effectiveScale;
        double padding = area.Height / effectiveScale < 600 || logicalWidth < 370 ? 16 : 24;
        header.Padding = new Thickness(padding, padding, padding, 12); bodyHost.Padding = new Thickness(padding, 0, padding, padding);
        header.Measure(new Windows.Foundation.Size(logicalWidth, double.PositiveInfinity));
        content.Measure(new Windows.Foundation.Size(Math.Max(1, logicalWidth - padding * 2 - 12), double.PositiveInfinity));
        var p = dock.AppWindow.Position; var s = dock.AppWindow.Size;
        var bounds = navigationAnchor.Place(new(area.X, area.Y, area.Width, area.Height), new(p.X, p.Y, s.Width, s.Height), effectiveScale, wide, content.DesiredSize.Height + header.DesiredSize.Height + padding, anchorX, AppWindow.IsVisible, newInteraction);
        // A rebuilt dock can lose its horizontal anchor. Keep that anchor, but
        // allow asynchronous loading/results pages to grow to their content.
        // The gallery keeps its full frame while adding/removing widgets.
        if (preserveFrame) bounds = id is "gallery" or "installed-apps"
            ? new(previousPosition.X, previousPosition.Y, previousSize.Width, previousSize.Height)
            : new(previousPosition.X, bounds.Y, previousSize.Width, bounds.Height);
        if (preserveFrame && !Ui.ReducedMotion && previousSize.Width == bounds.Width && previousSize.Height != bounds.Height)
        {
            resizeFrom = new(previousPosition.X, previousPosition.Y, previousSize.Width, previousSize.Height); resizeTo = bounds; resizeScale = effectiveScale; resizeStarted = System.Diagnostics.Stopwatch.GetTimestamp(); resizeTimer.Start();
        }
        else ApplyBounds(bounds, effectiveScale);
        Activate();
        DispatcherQueue.TryEnqueue(() => { if (PageId == id && AppWindow.IsVisible) { scroll.ChangeView(null, previousOffset, null, true); if (entering) close.Focus(FocusState.Keyboard); } });
        if (entering) Ui.Enter(layout, bounds.Y < dock.AppWindow.Position.Y ? 7 : -7);
        // Templates (notably expanders and multiline inputs) need one real
        // layout pass before their desired height is reliable.
        if ((replacement || entering) && id != "gallery") DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => {
            if (closed || dismissTimer.IsEnabled || revision != contentRevision || !AppWindow.IsVisible || PageId != id || lastArea != area || bodyHost.Children.FirstOrDefault() != content) return;
            content.InvalidateMeasure(); ShowPage(id, title, description, content, dock, area, wide, anchorX, userScale);
        });
    }
    public void Reflow(Window dock, RectInt32 area, double userScale)
    {
        if (AppWindow.IsVisible && bodyHost.Children.Count > 0) ShowPage(PageId, heading.Text, subtitle.Text, bodyHost.Children[0], dock, area, lastWide, userScale: userScale);
    }
}
