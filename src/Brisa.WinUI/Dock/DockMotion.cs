using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Brisa.WinUI;

public sealed partial class MainWindow
{
    int openDockMenus;
    int dockLayoutVersion;
    readonly List<(FrameworkElement Tile, string Key)> appMotionTargets = [];
    Dictionary<string, double> CaptureAppPositions()
    {
        var result = new Dictionary<string, double>();
        if (!Ui.ReducedMotion)
            foreach (var entry in appMotionTargets)
                if (entry.Tile.XamlRoot != null && entry.Tile.Parent is UIElement parent && entry.Tile.ActualWidth > 0) result[entry.Key] = entry.Tile.TransformToVisual(parent).TransformPoint(new(0, 0)).X;
        return result;
    }
    void RemoveWidgetAnimated(string id)
    {
        if (!settings.Widgets.Remove(id)) return;
        Save();
        var host = row.Children.OfType<Border>().FirstOrDefault(b => b.Tag is string key && key == id);
        if (Ui.ReducedMotion || host == null) { BuildDock(); return; }
        host.IsHitTestVisible = false; Ui.Animate(host, "Opacity", 0, 140);
        var finish = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        finish.Tick += (_, _) => { finish.Stop(); if (!closing) BuildDock(); }; finish.Start();
    }

    void TrackDockMenu(MenuFlyout menu)
    {
        bool open = false;
        menu.Opened += (_, _) => { if (!open) { open = true; openDockMenus++; dockBehavior?.Reveal(); } };
        menu.Closed += (_, _) => { if (open) { open = false; openDockMenus = Math.Max(0, openDockMenus - 1); dockBehavior?.Reveal(); } };
    }

    Dictionary<string, double> CaptureWidgetPositions()
    {
        var positions = new Dictionary<string, double>();
        if (!Ui.ReducedMotion && row.XamlRoot != null)
            foreach (var host in row.Children.OfType<Border>())
                if (host.Tag is string id && host.ActualWidth > 0)
                    positions[id] = (host.Child is WidgetDragButton editor ? editor : (FrameworkElement)host).TransformToVisual(row).TransformPoint(new(0, 0)).X;
        return positions;
    }

    void AnimateWidgetPositions(Dictionary<string, double> previous, Dictionary<string, double> previousApps)
    {
        int version = ++dockLayoutVersion;
        if (Ui.ReducedMotion || previous.Count == 0) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (closing || version != dockLayoutVersion || Ui.ReducedMotion) return;
            row.UpdateLayout();
            foreach (var host in row.Children.OfType<Border>())
            {
                if (host.Tag is not string id) continue;
                if (previous.TryGetValue(id, out double before))
                {
                    double after = host.TransformToVisual(row).TransformPoint(new(0, 0)).X;
                    var shift = new TranslateTransform { X = before - after };
                    host.RenderTransform = shift;
                    Ui.Animate(shift, "X", 0, 230);
                }
                else Ui.Enter(host);
            }
            foreach (var entry in appMotionTargets)
            {
                if (entry.Tile.RenderTransform is not CompositeTransform transform || entry.Tile.Parent is not UIElement parent) continue;
                if (previousApps.TryGetValue(entry.Key, out double before))
                {
                    transform.TranslateX = before - entry.Tile.TransformToVisual(parent).TransformPoint(new(0, 0)).X;
                    Ui.Animate(transform, "TranslateX", 0, 230);
                }
                else { entry.Tile.Opacity = 0; Ui.Animate(entry.Tile, "Opacity", 1, 180); }
            }
        });
    }
}
