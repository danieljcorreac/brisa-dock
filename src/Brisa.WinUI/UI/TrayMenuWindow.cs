using Brisa.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace Brisa.WinUI;

internal sealed class TrayMenuWindow : Window
{
    readonly StackPanel content = Ui.Stack(3);
    readonly List<Button> commands = [];
    readonly ScrollViewer scroll;
    readonly Grid layout;
    bool opened, closed;
    public TrayMenuWindow(IEnumerable<(string Label, string Glyph, Action? Action)> items, bool dismissOnDeactivate = true)
    {
        Title = "Accesos rápidos · Brisa";
        AppWindow.IsShownInSwitchers = false;
        Native.SetAppIcon(this);
        Native.Style(this);
        if (Ui.Translucent) SystemBackdrop = new GlassBackdrop(Ui.Light, Ui.TintStrength, Ui.BaseColor);
        var identity = Ui.Stack(3); identity.MinHeight = 48; identity.Margin = new Thickness(10, 4, 10, 12);
        identity.Children.Add(Ui.Text("Brisa", 17, bold: true));
        identity.Children.Add(Ui.Text("Accesos rápidos", 11, Ui.MutedColor)); content.Children.Add(identity);
        foreach (var item in items)
        {
            if (item.Action == null) { content.Children.Add(new Border { Height = 1, Margin = new Thickness(10, 6, 10, 6), Background = Ui.Brush(Ui.Surface) }); continue; }
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(20) }); row.ColumnDefinitions.Add(new());
            row.Children.Add(Ui.Glyph(item.Glyph, 16, Ui.Accent));
            var label = Ui.Text(item.Label, 13); Grid.SetColumn(label, 1); row.Children.Add(label);
            var command = Ui.Button(row, item.Label, () => { Dismiss(); item.Action(); });
            command.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            command.Background = Ui.Brush("#00FFFFFF"); command.BorderThickness = new Thickness(0);
            command.MinHeight = 44; command.Padding = new Thickness(10, 8, 10, 8); command.CornerRadius = new(10);
            ToolTipService.SetToolTip(command, null); commands.Add(command); content.Children.Add(command);
        }
        scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        layout = new Grid { Padding = new Thickness(10) }; layout.Children.Add(scroll);
        var root = new Border { Background = Ui.Brush(Ui.PanelBackground), RequestedTheme = Ui.Light ? ElementTheme.Light : ElementTheme.Dark,
            Child = new Viewbox { Child = layout, Stretch = Microsoft.UI.Xaml.Media.Stretch.Fill }, TabFocusNavigation = KeyboardNavigationMode.Cycle };
        Content = root;
        root.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Escape) { e.Handled = true; Dismiss(); return; }
            if (e.Key is not (VirtualKey.Up or VirtualKey.Down or VirtualKey.Home or VirtualKey.End) || commands.Count == 0) return;
            int index = commands.FindIndex(button => button.FocusState != FocusState.Unfocused);
            index = e.Key switch { VirtualKey.Home => 0, VirtualKey.End => commands.Count - 1, VirtualKey.Up => index < 0 ? commands.Count - 1 : (index + commands.Count - 1) % commands.Count, _ => (index + 1) % commands.Count };
            commands[index].Focus(FocusState.Keyboard); e.Handled = true;
        };
        Activated += (_, e) => { if (dismissOnDeactivate && opened && e.WindowActivationState == WindowActivationState.Deactivated) Dismiss(); };
        Closed += (_, _) => closed = true;
    }
    public void ShowAnchored(PreviewRect anchor, double userScale)
    {
        var area = DockMonitors.WorkAreaAt(anchor.X + anchor.Width / 2, anchor.Y + anchor.Height / 2);
        // Move first so WinUI/Win32 resolve the destination monitor's DPI, not the dock's.
        AppWindow.Move(new(area.X + 12, area.Y + 12));
        double scale = Native.Dpi(this) * userScale;
        double width = Math.Min(286, Math.Max(1, (area.Width - 20) / scale));
        layout.Width = width;
        layout.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
        var bounds = TrayMenuLayout.Fit(new(area.X, area.Y, area.Width, area.Height), anchor, scale, width, layout.DesiredSize.Height);
        layout.Height = bounds.Height / scale;
        Native.Resize(this, bounds.Width, bounds.Height); AppWindow.Move(new(bounds.X, bounds.Y));
        opened = true; Activate();
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (closed) return;
            // Native templates and the target monitor DPI settle after the first layout.
            // Re-measure once without moving through an intermediate screen position.
            double currentScale = Native.Dpi(this) * userScale;
            layout.Width = Math.Min(286, Math.Max(1, (area.Width - 20) / currentScale));
            layout.Height = double.NaN; content.InvalidateMeasure(); layout.InvalidateMeasure();
            layout.Measure(new Windows.Foundation.Size(layout.Width, double.PositiveInfinity));
            var settled = TrayMenuLayout.Fit(new(area.X, area.Y, area.Width, area.Height), anchor, currentScale, layout.Width, layout.DesiredSize.Height);
            layout.Height = settled.Height / currentScale;
            Native.Resize(this, settled.Width, settled.Height); AppWindow.Move(new(settled.X, settled.Y));
            if (commands.Count > 0) commands[0].Focus(FocusState.Programmatic);
        });
    }
    public void Dismiss() { if (!closed) { closed = true; Close(); } }
}
