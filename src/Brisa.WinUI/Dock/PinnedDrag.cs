using Brisa.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    bool draggingPinned;
    readonly List<(Button Tile, string? App, string? Group)> pinnedTargets = [];
    (Button Tile, string App, Action Activate)? pinnedSource;
    double pinnedOrigin;
    uint pinnedPointer;
    void SetupPinnedDrag()
    {
        row.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, args) =>
        {
            if (pinnedSource is not { } source || args.Pointer.PointerId != pinnedPointer) return;
            double x = args.GetCurrentPoint(row).Position.X;
            if (!draggingPinned && Math.Abs(x - pinnedOrigin) >= 6)
            {
                draggingPinned = true; source.Tile.Opacity = .5; dockBehavior?.Reveal();
            }
            if (draggingPinned) args.Handled = true;
        }), true);
        row.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, args) =>
        {
            if (pinnedSource is not { } source || args.Pointer.PointerId != pinnedPointer) return;
            bool moved = draggingPinned; var point = args.GetCurrentPoint(row).Position;
            var target = pinnedTargets.FirstOrDefault(t =>
            {
                if (t.Tile == source.Tile) return false;
                var position = t.Tile.TransformToVisual(row).TransformPoint(new(0, 0));
                return point.X >= position.X && point.X <= position.X + t.Tile.ActualWidth && point.Y >= position.Y && point.Y <= position.Y + t.Tile.ActualHeight;
            });
            source.Tile.Opacity = 1; pinnedSource = null; draggingPinned = false; row.ReleasePointerCaptures();
            args.Handled = true;
            if (!moved) { DispatcherQueue.TryEnqueue(() => { if (!closing) source.Activate(); }); return; }
            if (target.Tile == null) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (closing) return;
                if (target.Group != null) { AppGroups.Assign(settings, source.App, target.Group); Save(); BuildDock(); }
                else if (target.App != null) ShowGroupEditor(null, [source.App, target.App]);
            });
        }), true);
        row.PointerCanceled += (_, args) => { if (ReferenceEquals(args.OriginalSource, row)) CancelPinnedDrag(); };
        row.PointerCaptureLost += (_, args) => { if (ReferenceEquals(args.OriginalSource, row)) CancelPinnedDrag(); };
    }
    void CancelPinnedDrag() { if (pinnedSource is { } source) source.Tile.Opacity = 1; pinnedSource = null; draggingPinned = false; }
    void EnablePinnedDrag(Button tile, string? app, string? group, Action? activate = null)
    {
        pinnedTargets.Add((tile, app, group));
        if (app == null || activate == null) return;
        tile.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, args) =>
        {
            if (settings.DockLocked || editingDock || !args.GetCurrentPoint(tile).Properties.IsLeftButtonPressed) return;
            tile.ReleasePointerCaptures();
            if (!row.CapturePointer(args.Pointer)) return;
            pinnedSource = (tile, app, activate); pinnedPointer = args.Pointer.PointerId; pinnedOrigin = args.GetCurrentPoint(row).Position.X; args.Handled = true;
        }), true);
    }
}
