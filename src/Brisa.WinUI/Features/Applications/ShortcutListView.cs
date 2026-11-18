using System.Collections.ObjectModel;
using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System;

namespace Brisa.WinUI;
public sealed record ShortcutListRow(string Key, FrameworkElement Content, Button Handle)
{
    public override string ToString() => AutomationProperties.GetName(Content);
}
public sealed partial class MainWindow
{
    Grid ShortcutListIcon(string key)
    {
        var icon = new Grid { Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center };
        if (key.StartsWith("app:", StringComparison.Ordinal))
        {
            var app = settings.Apps.First(a => "app:" + a.Id == key);
            icon.Children.Add(Ui.Glyph(app.Folder ? "\uE8B7" : "\uE8A5", 24, Ui.Accent));
            _ = LoadIconAsync(icon, app.Target, 32);
        }
        else
        {
            var group = settings.AppGroups.First(g => "group:" + g.Id == key);
            icon.CornerRadius = new(8); icon.Background = Ui.Brush(Ui.Surface);
            for (int i = 0; i < 2; i++) { icon.ColumnDefinitions.Add(new()); icon.RowDefinitions.Add(new()); }
            int index = 0;
            var members = GroupMembers(group).Take(4).ToArray();
            foreach (var app in members)
            {
                double size = members.Length == 1 ? 24 : 13;
                var small = new Grid { Width = size, Height = size }; small.Children.Add(Ui.Glyph("\uE8A5", size - 2, Ui.Accent)); _ = LoadIconAsync(small, app.Target, size);
                Grid.SetColumn(small, index % 2); Grid.SetRow(small, index / 2);
                if (members.Length == 1) { Grid.SetColumnSpan(small, 2); Grid.SetRowSpan(small, 2); }
                icon.Children.Add(small); index++;
            }
            if (index == 0) { var glyph = Ui.Glyph("\uE8F1", 22, GroupColor(group.Accent)); Grid.SetColumnSpan(glyph, 2); Grid.SetRowSpan(glyph, 2); icon.Children.Add(glyph); }
        }
        return icon;
    }
    FrameworkElement BuildShortcutList(ToggleSwitch visibility, List<TextBlock> locations)
    {
        var rows = new ObservableCollection<ShortcutListRow>();
        foreach (string key in PinnedLayout.Visible(settings))
        {
            bool shortcut = key.StartsWith("app:", StringComparison.Ordinal);
            string label = shortcut ? settings.Apps.First(a => "app:" + a.Id == key).Name : settings.AppGroups.First(g => "group:" + g.Id == key).Name + " · Grupo";
            var line = new Grid { Tag = key, ColumnSpacing = 6, MinHeight = 64 };
            line.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); line.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); line.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var grip = Ui.Button(Ui.Text("⋮⋮", 17, Ui.MutedColor), "Ordenar " + label, () => { });
            grip.Width = 26; grip.Height = 36; grip.VerticalAlignment = VerticalAlignment.Center;
            grip.Padding = new Thickness(0); grip.BorderThickness = new Thickness(0); grip.Background = Ui.Brush("#00FFFFFF"); grip.CornerRadius = new(8); line.Children.Add(grip);
            AutomationProperties.SetHelpText(grip, "Arrastra o usa Control más Arriba o Abajo para cambiar su posición.");
            AutomationProperties.SetHelpText(line, "Arrastra desde los puntos para mover " + label);
            var content = new Grid { ColumnSpacing = 9, MinHeight = 44 }; content.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); content.ColumnDefinitions.Add(new());
            content.Children.Add(ShortcutListIcon(key));
            var copy = Ui.Stack(3); copy.VerticalAlignment = VerticalAlignment.Center; var name = Ui.Text(label, 13, bold: true); name.MaxLines = 2; name.TextTrimming = TextTrimming.CharacterEllipsis; copy.Children.Add(name);
            var location = Ui.Text(visibility.IsOn ? "En el dock · Editar" : "Guardado · Sección oculta", 11, Ui.MutedColor); locations.Add(location); copy.Children.Add(location);
            Grid.SetColumn(copy, 1); content.Children.Add(copy);
            var edit = Ui.Button(content, "Editar " + label, () => { if (shortcut) ShowShortcutEditor(key[4..]); else ShowGroupEditor(key[6..]); });
            edit.Padding = new Thickness(6); edit.Background = Ui.Brush("#00FFFFFF"); edit.BorderThickness = new Thickness(0);
            edit.HorizontalContentAlignment = HorizontalAlignment.Stretch; edit.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(edit, 1); line.Children.Add(edit);
            var remove = Ui.Button(shortcut ? "Desfijar" : "Desagrupar", shortcut ? "Desfijar " + label + " del dock" : "Deshacer " + label + " y conservar sus accesos", () => {
                if (shortcut) UnpinShortcut(key[4..], true);
                else { settings.AppGroups.RemoveAll(g => g.Id == key[6..]); Save(); BuildDock(); ShowAppsManager(); }
            }); remove.Padding = new Thickness(10, 8, 10, 8); remove.CornerRadius = new(10);
            remove.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(remove, 2); line.Children.Add(remove);
            AutomationProperties.SetName(line, label); rows.Add(new(key, line, grip));
        }
        var list = new ListView { ItemsSource = rows, SelectionMode = ListViewSelectionMode.Single, TabFocusNavigation = KeyboardNavigationMode.Local,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, MaxHeight = 300 };
        list.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load("<ItemsPanelTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><StackPanel/></ItemsPanelTemplate>");
        AutomationProperties.SetName(list, "Accesos del dock: arrastra para ordenar");
        list.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><ContentPresenter Content="{Binding Content}" HorizontalAlignment="Stretch"/></DataTemplate>
            """);
        AutomationProperties.SetHelpText(list, "Selecciona un acceso y usa Control más Arriba o Abajo para moverlo con el teclado.");
        var style = new Style(typeof(ListViewItem));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 8, 14, 8)));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(14)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(2, 2, 2, 6)));
        // Explicitly bind the native presenter's radius; the default template does not
        // inherit Control.CornerRadius. Theme resources retain accessible state colors.
        style.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Load("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ListViewItem">
                <ListViewItemPresenter
                    CornerRadius="{TemplateBinding CornerRadius}" ContentMargin="{TemplateBinding Padding}"
                    HorizontalContentAlignment="{TemplateBinding HorizontalContentAlignment}"
                    VerticalContentAlignment="{TemplateBinding VerticalContentAlignment}"
                    ContentTransitions="{TemplateBinding ContentTransitions}"
                    SelectionCheckMarkVisualEnabled="False" SelectionIndicatorVisualEnabled="False"
                    PointerOverBackground="{ThemeResource ListViewItemBackgroundPointerOver}"
                    SelectedBackground="{ThemeResource ListViewItemBackgroundSelected}"
                    SelectedPointerOverBackground="{ThemeResource ListViewItemBackgroundSelectedPointerOver}"
                    PressedBackground="{ThemeResource ListViewItemBackgroundPressed}"
                    SelectedPressedBackground="{ThemeResource ListViewItemBackgroundSelectedPressed}"
                    PointerOverForeground="{ThemeResource ListViewItemForegroundPointerOver}"
                    SelectedForeground="{ThemeResource ListViewItemForegroundSelected}"
                    DisabledOpacity="{ThemeResource ListViewItemDisabledThemeOpacity}"
                    Control.IsTemplateFocusTarget="True"
                    FocusVisualMargin="{TemplateBinding FocusVisualMargin}"
                    FocusVisualPrimaryBrush="{TemplateBinding FocusVisualPrimaryBrush}"
                    FocusVisualPrimaryThickness="{TemplateBinding FocusVisualPrimaryThickness}"
                    FocusVisualSecondaryBrush="{TemplateBinding FocusVisualSecondaryBrush}"
                    FocusVisualSecondaryThickness="{TemplateBinding FocusVisualSecondaryThickness}" />
            </ControlTemplate>
            """)));
        list.ItemContainerStyle = style;
        void RefreshTransitions() => list.ItemContainerTransitions = Ui.ReducedMotion ? new TransitionCollection() : new TransitionCollection { new RepositionThemeTransition { IsStaggeringEnabled = false } };
        RefreshTransitions();
        var status = Ui.Text("", 11, Ui.MutedColor); status.MinHeight = 18;
        AutomationProperties.SetLiveSetting(status, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        bool dragging = false;
        ShortcutListRow? dragged = null; double dragX = 0, dragY = 0, originY = 0, originScroll = 0;
        uint pointerId = 0;
        FrameworkElement? floating = null; Transform? originalTransform = null;
        double originalOpacity = 1; int originalZ = 0;
        var translation = new TranslateTransform();
        var scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        ScrollViewer? scrollHost = null;
        var markers = new Dictionary<ShortcutListRow, Border>();
        foreach (var row in rows)
        {
            var marker = new Border { Height = 2, Background = Ui.Brush(Ui.Accent), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            Grid.SetColumnSpan(marker, 3); ((Grid)row.Content).Children.Add(marker); markers[row] = marker;
        }
        void HideMarkers() { foreach (var marker in markers.Values) marker.Visibility = Visibility.Collapsed; }
        int Slot(double y)
        {
            for (int i = 0; i < rows.Count; i++)
                if (list.ContainerFromItem(rows[i]) is FrameworkElement item && y < item.TransformToVisual(list).TransformPoint(new(0, 0)).Y - (item == floating ? translation.Y : 0) + item.ActualHeight / 2) return i;
            return rows.Count;
        }
        void Preview()
        {
            HideMarkers(); if (rows.Count == 0) return;
            if (dragX < 0 || dragX > list.ActualWidth || dragY < 0 || dragY > list.ActualHeight) return;
            int slot = Slot(dragY), from = dragged == null ? -1 : rows.IndexOf(dragged);
            if (ShortcutDragLayout.Destination(from, slot, rows.Count) == from) return;
            var marker = markers[rows[Math.Min(slot, rows.Count - 1)]];
            marker.VerticalAlignment = slot == rows.Count ? VerticalAlignment.Bottom : VerticalAlignment.Top; marker.Visibility = Visibility.Visible;
        }
        ScrollViewer? FindScroll(DependencyObject root)
        {
            if (root is ScrollViewer viewer) return viewer;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) if (FindScroll(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
            return null;
        }
        scrollTimer.Tick += (_, _) =>
        {
            if (!dragging || scrollHost == null) { scrollTimer.Stop(); return; }
            double delta = ShortcutDragLayout.ScrollStep(dragX, dragY, list.ActualWidth, list.ActualHeight);
            if (delta != 0) scrollHost.ChangeView(null, Math.Clamp(scrollHost.VerticalOffset + delta, 0, scrollHost.ScrollableHeight), null, true);
        };
        PanelWindow? owner = null;
        void OnActivation(object sender, WindowActivatedEventArgs args) { if (args.WindowActivationState == WindowActivationState.Deactivated) { ResetDrag(); status.Text = ""; } }
        list.Loaded += (_, _) => { owner = panel; if (owner != null) owner.Activated += OnActivation; };
        list.Unloaded += (_, _) => { ResetDrag(); if (owner != null) owner.Activated -= OnActivation; };
        list.Loaded += (_, _) => { scrollHost = FindScroll(list); if (scrollHost != null) scrollHost.ViewChanged += (_, _) => { if (dragging) { UpdateFloating(); Preview(); } }; };
        void Commit()
        {
            if (closing || list.XamlRoot == null || panel?.PageId != "apps-manager") return;
            if (PinnedLayout.ApplyVisibleOrder(settings, rows.Select(row => row.Key))) { Save(); BuildDock(); status.Text = "Orden guardado";
                if (FrameworkElementAutomationPeer.FromElement(status) is { } peer) peer.RaiseAutomationEvent(Microsoft.UI.Xaml.Automation.Peers.AutomationEvents.LiveRegionChanged); }
        }
        void Trace(string stage, string result)
        {
            if (!Environment.GetCommandLineArgs().Contains("--diagnostics")) return;
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "brisa-shortcut-drag.jsonl"), System.Text.Json.JsonSerializer.Serialize(new { Stage = stage, Result = result, Order = rows.Select(row => row.Key).ToArray() }) + Environment.NewLine); } catch (IOException) { }
        }
        void UpdateFloating()
        {
            if (floating != null) translation.Y = dragY - originY + (scrollHost?.VerticalOffset ?? 0) - originScroll;
        }
        void ResetDrag()
        {
            dragged = null; dragging = false;
            if (floating != null) { floating.RenderTransform = originalTransform; floating.Opacity = originalOpacity; Canvas.SetZIndex(floating, originalZ); }
            floating = null; translation.Y = 0; HideMarkers(); scrollTimer.Stop(); list.ReleasePointerCaptures();
        }
        foreach (var row in rows)
        {
            row.Content.GotFocus += (_, _) => list.SelectedItem = row;
            row.Handle.Click += (_, _) => list.SelectedItem = row;
            row.Handle.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, args) =>
            {
                if (!args.GetCurrentPoint(row.Handle).Properties.IsLeftButtonPressed || dragged != null) return;
                row.Handle.ReleasePointerCaptures();
                if (!list.CapturePointer(args.Pointer)) return;
                dragged = row; pointerId = args.Pointer.PointerId; dragY = originY = args.GetCurrentPoint(list).Position.Y; dragX = args.GetCurrentPoint(list).Position.X;
                scrollHost = FindScroll(list); originScroll = scrollHost?.VerticalOffset ?? 0;
                list.SelectedItem = row; list.Focus(FocusState.Pointer); args.Handled = true;
            }), true);
            list.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, args) =>
            {
                if (dragged != row || args.Pointer.PointerId != pointerId) return;
                dragY = args.GetCurrentPoint(list).Position.Y; dragX = args.GetCurrentPoint(list).Position.X;
                if (!dragging && Math.Abs(dragY - originY) >= 5)
                {
                    dragging = true; floating = list.ContainerFromItem(row) as FrameworkElement;
                    if (floating != null) { originalTransform = floating.RenderTransform; originalOpacity = floating.Opacity; originalZ = Canvas.GetZIndex(floating); floating.RenderTransform = translation; floating.Opacity = .9; Canvas.SetZIndex(floating, 100); }
                    status.Text = "Suelta el acceso en su nueva posición"; Trace("start", row.Key);
                }
                if (dragging) { UpdateFloating(); Preview(); scrollTimer.Start(); }
                args.Handled = true;
            }), true);
            list.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, args) =>
            {
                if (dragged != row || args.Pointer.PointerId != pointerId) return;
                var point = args.GetCurrentPoint(list).Position; dragY = point.Y;
                int from = rows.IndexOf(row), slot = Slot(dragY);
                int destination = ShortcutDragLayout.Destination(from, slot, rows.Count);
                bool apply = dragging && point.X >= 0 && point.X <= list.ActualWidth && point.Y >= 0 && point.Y <= list.ActualHeight;
                ResetDrag(); status.Text = ""; args.Handled = true;
                if (apply && from >= 0 && from != destination) { RefreshTransitions(); rows.Move(from, destination); list.SelectedItem = row; Commit(); }
                Trace("drop", apply ? $"{from}->{destination}" : "cancelled");
            }), true);
            list.PointerCanceled += (_, _) => { if (dragged == row) { ResetDrag(); status.Text = ""; } };
            list.PointerCaptureLost += (_, _) => { if (dragged == row) { ResetDrag(); status.Text = ""; } };
        }
        list.KeyDown += (_, args) => { if (args.Key == VirtualKey.Escape && dragged != null) { ResetDrag(); status.Text = ""; args.Handled = true; } };
        void KeyboardMove(int direction)
        {
            if (dragging || list.SelectedItem is not ShortcutListRow selected) return;
            int index = rows.IndexOf(selected), destination = index + direction;
            if (index < 0 || destination < 0 || destination >= rows.Count) return;
            RefreshTransitions(); rows.Move(index, destination); list.SelectedItem = selected; list.ScrollIntoView(selected); Commit();
        }
        foreach (var option in new[] { (VirtualKey.Up, -1), (VirtualKey.Down, 1) })
        {
            var key = new KeyboardAccelerator { Key = option.Item1, Modifiers = VirtualKeyModifiers.Control, ScopeOwner = list };
            key.Invoked += (_, args) => { args.Handled = true; KeyboardMove(option.Item2); }; list.KeyboardAccelerators.Add(key);
        }
        if (rows.Count > 0) list.SelectedIndex = 0;
        var section = Ui.Stack(6); section.Children.Add(list); section.Children.Add(status); return section;
    }
}
