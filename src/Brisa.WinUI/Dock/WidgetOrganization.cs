using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;

namespace Brisa.WinUI;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class WidgetOrderRow(string id, string name, string category, string glyph)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Category { get; } = category;
    public string Glyph { get; } = glyph;
    public override string ToString() => Name;
}

public sealed partial class MainWindow
{
    readonly List<(string Id, WidgetDragButton Host, TranslateTransform Motion)> editTargets = [];
    Action? cancelWidgetDrag;
    readonly Dictionary<TranslateTransform, Microsoft.UI.Xaml.Media.Animation.Storyboard> editTransitions = [];
    void StopEditorTransitions() { foreach (var transition in editTransitions.Values) transition.Stop(); editTransitions.Clear(); }
    void PreviewEditorPosition(TranslateTransform motion, double x)
    {
        double current = motion.X;
        if (editTransitions.Remove(motion, out var previous)) previous.Stop();
        motion.X = current;
        editTransitions[motion] = Ui.Animate(motion, "X", x, 150);
    }
    bool editingDock;
    string? galleryInsertAfter;
    bool galleryPositionChosen;
    string galleryQuery = "";

    void SetDockEditing(bool enabled)
    {
        cancelWidgetDrag?.Invoke();
        editingDock = enabled;
        panel?.AppWindow.Hide();
        dockBehavior?.Reveal();
        BuildDock();
        if (!enabled) { _ = RefreshAppsAsync(); RestoreDockFocus(); }
        else DispatcherQueue.TryEnqueue(() => editTargets.FirstOrDefault().Host?.Focus(FocusState.Keyboard));
    }

    FrameworkElement WidgetEditor(WidgetDefinition definition, FrameworkElement tile)
    {
        // Preserve the live widget exactly as it appears normally. Only the
        // editor receives input; inner actions remain inactive during editing.
        void ExcludeFromTab(UIElement element)
        {
            AutomationProperties.SetAccessibilityView(element, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
            if (element is Control control) control.IsTabStop = false;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                if (VisualTreeHelper.GetChild(element, i) is UIElement child) ExcludeFromTab(child);
        }
        tile.IsHitTestVisible = false; ExcludeFromTab(tile);
        var motion = new TranslateTransform();
        var host = new WidgetDragButton { Content = tile, Height = Ui.DockTileHeight, Padding = new Thickness(0), CornerRadius = new CornerRadius(14),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(1), BorderBrush = Ui.Brush(Ui.Light ? "#55376F66" : "#557EBBA8"),
            Background = Ui.Brush(Ui.Light ? "#18FFFFFF" : "#122D5148"), RenderTransform = motion };
        AutomationProperties.SetName(host, "Organizar " + definition.Name);
        AutomationProperties.SetHelpText(host, "Arrastra para mover. Con el teclado, usa izquierda y derecha. Escape termina la edición.");

        editTargets.Add((definition.Id, host, motion));
        bool pressed = false, dragged = false, committing = false;
        uint pointerId = 0;
        double origin = 0, pointerRow = 0, pointerViewport = 0;
        var edgeScroll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        int destination = -1;
        string[] otherIds = []; DragSlot[] dragSlots = []; double slotStart = 0, slotEnd = 0; long edgeStamp = 0;
        var slots = new Dictionary<string, (double X, double Width)>();
        var modelSlots = new Dictionary<string, double>(); double initialShift = 0;
        void Clear(bool settle = false)
        {
            if (committing) return;
            edgeScroll.Stop(); if (!settle) StopEditorTransitions();
            pressed = dragged = false; cancelWidgetDrag = null; host.SetDragging(false);
            foreach (var target in editTargets) { if (settle) PreviewEditorPosition(target.Motion, 0); else target.Motion.X = 0; target.Motion.Y = 0; if (target.Host.Parent is UIElement parent) Canvas.SetZIndex(parent, 0); }
            host.BorderBrush = Ui.Brush(Ui.Light ? "#55376F66" : "#557EBBA8");
        }
        host.AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, args) =>
        {
            if (settings.DockLocked || !args.GetCurrentPoint(host).Properties.IsLeftButtonPressed) return;
            cancelWidgetDrag?.Invoke();
            row.UpdateLayout();
            var visiblePositions = editTargets.ToDictionary(t => t.Id, t => t.Host.TransformToVisual(row).TransformPoint(new(0, 0)).X);
            StopEditorTransitions(); slots.Clear(); modelSlots.Clear();
            foreach (var target in editTargets)
            {
                target.Motion.X = target.Motion.Y = 0;
                if (target.Host.Parent is FrameworkElement parent)
                {
                    parent.RenderTransform = null; double modelX = parent.TransformToVisual(row).TransformPoint(new(0, 0)).X;
                    modelSlots[target.Id] = modelX; target.Motion.X = visiblePositions[target.Id] - modelX;
                    slots[target.Id] = (visiblePositions[target.Id], parent.ActualWidth);
                }
            }
            if (!slots.ContainsKey(definition.Id) || !(host.PointerCaptures?.Any(p => p.PointerId == args.Pointer.PointerId) == true || host.CapturePointer(args.Pointer))) return;
            otherIds = settings.Widgets.Where(id => id != definition.Id && slots.ContainsKey(id)).ToArray(); slotStart = slots.Values.Min(s => s.X); slotEnd = slots.Values.Max(s => s.X + s.Width);
            dragSlots = slots.Select(pair => new DragSlot(pair.Key, pair.Value.X, pair.Value.Width)).ToArray();
            pressed = true; dragged = false; pointerId = args.Pointer.PointerId; origin = args.GetCurrentPoint(row).Position.X;
            destination = settings.Widgets.IndexOf(definition.Id); initialShift = motion.X;
            cancelWidgetDrag = () => { Clear(); host.ReleasePointerCaptures(); };
            host.Focus(FocusState.Pointer); args.Handled = true;
        }), true);
        void Preview()
        {
            double delta = pointerRow - origin;
            if (!dragged && Math.Abs(delta) < 4) return;
            if (!dragged)
            {
                host.SetDragging(true); host.BorderBrush = Ui.Brush(Ui.Light ? "#376F66" : "#B9E9D6");
                if (host.Parent is UIElement parent) Canvas.SetZIndex(parent, 10);
            }
            dragged = true;
            var source = slots[definition.Id];
            double first = slotStart, last = slotEnd;
            double center = source.X + delta + source.Width / 2;
            delta = Math.Clamp(delta, first - source.X, last - source.X - source.Width);
            motion.X = initialShift + delta; motion.Y = Ui.ReducedMotion ? 0 : settings.Edge == "bottom" ? -3 : 3;
            var others = otherIds;
            int next = WidgetDragMath.Destination(dragSlots, definition.Id, center, destination);
            if (next != destination)
            {
                destination = next; var proposed = others.ToList(); proposed.Insert(destination, definition.Id); double x = first;
                foreach (string id in proposed)
                {
                    var target = editTargets.First(t => t.Id == id);
                    if (id != definition.Id) PreviewEditorPosition(target.Motion, x - modelSlots[id]);
                    x += slots[id].Width + row.Spacing;
                }
            }
        }
        edgeScroll.Tick += (_, _) =>
        {
            if (!pressed || !dragged) { edgeScroll.Stop(); return; }
            long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
            double elapsed = edgeStamp == 0 ? .016 : System.Diagnostics.Stopwatch.GetElapsedTime(edgeStamp, stamp).TotalSeconds; edgeStamp = stamp;
            double before = dockScroll.HorizontalOffset;
            double after = WidgetDragMath.ScrollOffset(pointerViewport, dockScroll.ActualWidth, before, dockScroll.ScrollableWidth, elapsed);
            if (Math.Abs(after - before) < .1) { edgeScroll.Stop(); return; }
            dockScroll.ChangeView(after, null, null, true);
            pointerRow += (after - before) / fitScale; Preview();
        };
        host.AddHandler(UIElement.PointerMovedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, args) =>
        {
            if (!pressed || pointerId != args.Pointer.PointerId) return;
            pointerRow = args.GetCurrentPoint(row).Position.X;
            pointerViewport = args.GetCurrentPoint(dockScroll).Position.X;
            Preview();
            if (dragged && dockScroll.ScrollableWidth > 0 && (pointerViewport < 44 || pointerViewport > dockScroll.ActualWidth - 44)) { if (!edgeScroll.IsEnabled) { edgeStamp = System.Diagnostics.Stopwatch.GetTimestamp(); edgeScroll.Start(); } } else edgeScroll.Stop();
            args.Handled = true;
        }), true);
        host.AddHandler(UIElement.PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, args) =>
        {
            if (!pressed || pointerId != args.Pointer.PointerId) return;
            var drop = args.GetCurrentPoint(dockScroll).Position;
            bool move = dragged && drop.Y >= -32 && drop.Y <= dockScroll.ActualHeight + 32 && destination != settings.Widgets.IndexOf(definition.Id);
            edgeScroll.Stop(); pressed = false; committing = move; cancelWidgetDrag = null;
            host.ReleasePointerCapture(args.Pointer); args.Handled = true;
            if (!move) { Clear(true); return; }
            DispatcherQueue.TryEnqueue(() => {
                if (closing || !editingDock) return;
                var positions = CaptureWidgetPositions();
                if (WidgetOrder.MoveTo(settings.Widgets, definition.Id, destination))
                {
                    Save(); StopEditorTransitions();
                    foreach (var target in editTargets) { target.Motion.X = target.Motion.Y = 0; if (target.Host.Parent is UIElement wrapper) Canvas.SetZIndex(wrapper, 0); }
                    if (host.Parent is Border wrapperHost) { row.Children.Remove(wrapperHost); row.Children.Insert(destination, wrapperHost); }
                    row.UpdateLayout(); AnimateWidgetPositions(positions, []);
                }
                committing = dragged = false; host.SetDragging(false); host.BorderBrush = Ui.Brush(Ui.Light ? "#55376F66" : "#557EBBA8");
                editTargets.FirstOrDefault(t => t.Id == definition.Id).Host?.Focus(FocusState.Pointer);
            });
        }), true);
        host.PointerCaptureLost += (_, _) => DispatcherQueue.TryEnqueue(() => { if (pressed) Clear(); });
        host.PointerCanceled += (_, _) => { Clear(); host.ReleasePointerCaptures(); };
        host.KeyDown += (_, args) =>
        {
            if (args.Key is not VirtualKey.Left and not VirtualKey.Right || pressed) return;
            args.Handled = true; ShiftWidget(definition.Id, args.Key == VirtualKey.Left ? -1 : 1);
            DispatcherQueue.TryEnqueue(() => editTargets.FirstOrDefault(t => t.Id == definition.Id).Host?.Focus(FocusState.Keyboard));
        };
        return host;
    }

    void ShowOrder()
    {
        var body = Ui.Stack(12);
        body.Children.Add(Ui.Text("Selecciona un widget para moverlo. También puedes arrastrarlo desde el lápiz. Los cambios se guardan solos.", 13, Ui.MutedColor));
        var rows = new ObservableCollection<WidgetOrderRow>(settings.Widgets.Select(id => WidgetCatalog.Find(id)!).Select(d => new WidgetOrderRow(d.Id, d.Name, d.Category, d.Glyph)));
        var list = new ListView { ItemsSource = rows, SelectionMode = ListViewSelectionMode.Single, Height = Math.Clamp(DockWorkArea.Height / Native.Dpi(this) / settings.Scale - 410, 120, 240), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(list, "Orden de los widgets: selecciona para cambiar de posición");
        list.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <Grid Height="52" Padding="6" ColumnSpacing="12">
                <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                <FontIcon Grid.Column="0" Glyph="{Binding Glyph}" FontFamily="Segoe Fluent Icons" FontSize="20"/>
                <StackPanel Grid.Column="1" VerticalAlignment="Center" Spacing="2">
                  <TextBlock Text="{Binding Name}" FontSize="14" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
                  <TextBlock Text="{Binding Category}" FontSize="10" Opacity="0.65"/>
                </StackPanel>
              </Grid>
            </DataTemplate>
            """);
        var style = new Style(typeof(ListViewItem));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3)));
        list.ItemContainerStyle = style;
        var status = Ui.Status(11);
        var movement = new Grid { ColumnSpacing = 8, RowSpacing = 8 }; for (int i = 0; i < 2; i++) { movement.ColumnDefinitions.Add(new()); movement.RowDefinitions.Add(new() { Height = GridLength.Auto }); }
        var buttons = new List<Button>();
        void Move(int destination)
        {
            int current = list.SelectedIndex;
            if (current < 0 || destination < 0 || destination >= rows.Count || current == destination) return;
            var selected = rows[current]; rows.Move(current, destination); list.SelectedItem = selected; list.ScrollIntoView(selected);
        }
        void Add(string text, string label, Func<int> destination, int column)
        {
            var button = Ui.Button(text, label, () => Move(destination())); button.Padding = new Thickness(8, 10, 8, 10); button.MinHeight = 42;
            Grid.SetColumn(button, column % 2); Grid.SetRow(button, column / 2); movement.Children.Add(button); buttons.Add(button);
        }
        Add("Al inicio", "Mover widget al inicio", () => 0, 0);
        Add("Subir", "Mover widget una posición antes", () => list.SelectedIndex - 1, 1);
        Add("Bajar", "Mover widget una posición después", () => list.SelectedIndex + 1, 2);
        Add("Al final", "Mover widget al final", () => rows.Count - 1, 3);
        void Selection()
        {
            int index = list.SelectedIndex;
            buttons[0].IsEnabled = buttons[1].IsEnabled = index > 0;
            buttons[2].IsEnabled = buttons[3].IsEnabled = index >= 0 && index < rows.Count - 1;
        }
        list.SelectionChanged += (_, _) => Selection();
        rows.CollectionChanged += (_, _) =>
        {
            if (WidgetOrder.Apply(settings.Widgets, rows.Select(r => r.Id))) { Save(); BuildDock(); status.Text = "Orden guardado"; }
            DispatcherQueue.TryEnqueue(Selection);
        };
        body.Children.Add(list); body.Children.Add(movement); body.Children.Add(status);
        if (rows.Count > 0) list.SelectedIndex = 0; Selection();
        if (settings.DockLocked) body.Children.Add(Ui.Button("Desbloquear arrastre", "Desbloquear arrastre de widgets", () => { settings.DockLocked = false; Save(); BuildDock(); ShowOrder(); }));
        body.Children.Add(Ui.Button("Organizar directamente en la barra", "Activar edición del dock", () => SetDockEditing(true), true));
        body.Children.Add(Ui.Text("Lápiz: organiza sobre la barra. Pulsa Listo o Escape para terminar. Los widgets conservan su aspecto; puedes arrastrarlos desde cualquier punto.", 12, Ui.MutedColor));
        ShowPage("order", "Organiza tu dock", "ARRASTRAR · MOVER · GUARDAR", body);
    }
}
