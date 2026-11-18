using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
namespace Brisa.WinUI;
public sealed partial class MainWindow : Window
{
    readonly SettingsStore store;
    readonly BackgroundWork background = new(BackgroundDiagnostics.Record);
    readonly DockSettings settings;
    readonly SystemStats stats = new();
    readonly StackPanel row = Ui.Stack(4, true);
    readonly Dictionary<string, Action> updates = [];
    readonly Dictionary<string, UsageSnapshot> usage = [];
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly CancellationTokenSource lifetime = new();
    readonly Border shell;
    readonly ScrollViewer dockScroll;
    readonly Viewbox dockContent;
    readonly Grid dockViewport;
    DockBehavior? dockBehavior;
    DesktopReservation? reservation;
    readonly SemaphoreSlim usageGate = new(1, 1);
    PanelWindow? panel;
    FrameworkElement? activeAnchor;
    bool openingPanelFromDock;
    Action? livePanelUpdate;
    DateTimeOffset lastAiPoll = DateTimeOffset.MinValue;
    bool building, closing, dockActive;
    double fitScale = 1;
    public MainWindow(string? dataDirectory = null)
    {
        InitializeComponent(); Title = "Brisa Dock"; Native.SetAppIcon(this);
        store = new(dataDirectory ?? System.IO.Path.Combine(AppContext.BaseDirectory, "data"));
        settings = store.Load(); ApplyPreferences();
        weather = settings.WeatherCache?.Reading;
        countdown.Set(TimeSpan.FromSeconds(settings.TimerRemainingSeconds ?? settings.TimerSeconds));
        focusSession = new(settings.Focus); focusSession.Restore(settings.FocusState);
        usage["claude"] = UsageSnapshot.Empty("claude", "Conecta tu plan");
        usage["chatgpt"] = UsageSnapshot.Empty("chatgpt", "Conecta tu plan");
        SetReviewQuotas();
        row.VerticalAlignment = VerticalAlignment.Center;
        SetupPinnedDrag();
        dockContent = new Viewbox { Child = row, Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        dockViewport = new Grid { HorizontalAlignment = HorizontalAlignment.Left }; dockViewport.Children.Add(dockContent);
        dockScroll = new ScrollViewer { Content = dockViewport, HorizontalScrollMode = ScrollMode.Enabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        dockScroll.PointerWheelChanged += (_, e) => { if (dockScroll.ScrollableWidth > 0) { dockScroll.ChangeView(Math.Clamp(dockScroll.HorizontalOffset - e.GetCurrentPoint(dockScroll).Properties.MouseWheelDelta / 2d, 0, dockScroll.ScrollableWidth), null, null, false); e.Handled = true; } };
        dockScroll.DragOver += (_, e) => { if (!editingDock || dockScroll.ScrollableWidth <= 0) return; double x = e.GetPosition(dockScroll).X; double delta = x < 28 ? -18 : x > dockScroll.ActualWidth - 28 ? 18 : 0; if (delta != 0) dockScroll.ChangeView(Math.Clamp(dockScroll.HorizontalOffset + delta, 0, dockScroll.ScrollableWidth), null, null, true); };
        shell = new Border { Child = dockScroll, Padding = new Thickness(12), CornerRadius = new CornerRadius(0), BorderThickness = new Thickness(0) };
        Content = shell; SetSurface(); Native.Style(this, settings.AlwaysOnTop); BuildDock();
        ConfigureKeyboard(shell);
        Activated += (_, e) => {
            dockActive = e.WindowActivationState != WindowActivationState.Deactivated;
            if (e.WindowActivationState == WindowActivationState.Deactivated) cancelWidgetDrag?.Invoke();
            else DispatcherQueue.TryEnqueue(EnsureDockKeyboardFocus);
        };
        dockBehavior = new DockBehavior(this, settings, () => DockBounds, () => editingDock || draggingPinned || openDockMenus > 0 || panel?.AppWindow.IsVisible == true || DockHasKeyboardFocus(), () => panel?.AppWindow.Hide());
        SetupWindowsIntegration();
        try { reservation = new(WinRT.Interop.WindowNative.GetWindowHandle(this), () => { displayDebounce.Stop(); displayDebounce.Start(); }); } catch (InvalidOperationException error) { BackgroundDiagnostics.Record("desktop-reservation", error); }
        dockScroll.IsTabStop = false;
        shell.Loaded += (_, _) => { PositionDock(); EnsureDockKeyboardFocus(); OpenReviewPage(); if (store.Warning != null) ShowMessage("Ajustes", store.Warning); else ReadMaintenanceResult(); };
        shell.KeyDown += (_, e) => { if (e.Key == VirtualKey.Escape) { if (editingDock) SetDockEditing(false); else panel?.AppWindow.Hide(); } };
        timer.Tick += (_, _) =>
        {
            if (closing) return;
            Tick();
            PollReleaseUpdates();
            if (settings.Widgets.Contains("apps") && !editingDock) _ = background.RunAsync("apps", _ => RefreshAppsAsync(), lifetime.Token);
            if (settings.Widgets.Contains("music")) _ = background.RunAsync("media", _ => RefreshMediaAsync(), lifetime.Token);
            if (DateTimeOffset.Now - lastAiPoll > TimeSpan.FromSeconds(5)) _ = background.RunAsync("usage", _ => RefreshUsageAsync(), lifetime.Token);
            if (settings.Widgets.Contains("weather") && settings.WeatherLocation != null && DateTimeOffset.Now - lastWeatherPoll > TimeSpan.FromMinutes(15)) _ = background.RunAsync("weather", _ => RefreshWeatherAsync(), lifetime.Token);
        };
        timer.Start(); Tick();
        Closed += (_, _) => { closing = true; trayQuickMenu?.Dismiss(); cancelWidgetDrag?.Invoke(); StopEditorTransitions(); SaveSessionState(); displayDebounce.Stop(); reservation?.Dispose(); windowsIntegration?.Dispose(); dockBehavior?.Dispose(); lifetime.Cancel(); timer.Stop(); panel?.Close(); };
        AppWindow.Changed += (_, e) => { if (e.DidSizeChange && !building) Native.Round(this, AppWindow.Size.Width, AppWindow.Size.Height); };
    }
    bool DockHasKeyboardFocus() => dockActive && shell.XamlRoot != null && FocusManager.GetFocusedElement(shell.XamlRoot) is Control { FocusState: FocusState.Keyboard };
    void ApplyPreferences() { ApplyAccessibilityPreferences(); Ui.Light = Ui.HighContrast ? ContrastColors.Light : settings.Theme == "light"; Ui.Midnight = settings.Theme == "midnight"; Ui.TintStrength = settings.TintStrength; Ui.RefreshContrast(); if (Ui.ReducedMotion) StopEditorTransitions(); }
    void SetSurface()
    {
        shell.RequestedTheme = Ui.Light ? ElementTheme.Light : ElementTheme.Dark;
        shell.Background = Ui.Brush(Ui.Translucent ? (Ui.Light ? "#70" : "#88") + Ui.BaseColor[1..] : Ui.BaseColor);
        shell.BorderBrush = null; shell.BorderThickness = new Thickness(0);
        if (Ui.Translucent && SystemBackdrop is GlassBackdrop existing && existing.Matches(Ui.Light, Ui.BaseColor)) existing.UpdateTint(Ui.TintStrength);
        else SystemBackdrop = Ui.Translucent ? new GlassBackdrop(Ui.Light, Ui.TintStrength, Ui.BaseColor) : null;
    }
    void Save()
    {
        try { settings.PinnedOrder = PinnedLayout.Normalize(settings); store.Save(settings); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { ShowMessage("No se pudo guardar", "Los cambios siguen en esta sesión. Revisa que la carpeta de Brisa permita escribir."); }
    }
    void BuildDock()
    {
        if (closing) return;
        cancelWidgetDrag?.Invoke();
        PrepareMonitor();
        var previousPositions = CaptureWidgetPositions();
        StopEditorTransitions();
        var previousApps = CaptureAppPositions();
        building = true; CancelPinnedDrag(); pinnedTargets.Clear(); appMotionTargets.Clear(); row.Children.Clear(); updates.Clear(); appIndicators.Clear(); editTargets.Clear(); row.Spacing = settings.WidgetSpacing;
        foreach (string id in settings.Widgets)
        {
            var definition = WidgetCatalog.Find(id); if (definition == null) continue;
            var tile = BuildWidget(definition);
            if (editingDock) tile = WidgetEditor(definition, tile);
            tile.Tag = id;
            var menu = new MenuFlyout();
            void Item(string text, string glyph, Action action) { var item = new MenuFlyoutItem { Text = text, Icon = Ui.Glyph(glyph, 16) }; item.Click += (_, _) => action(); menu.Items.Add(item); }
            Item(editingDock ? "Terminar organización" : "Organizar en la barra", "\uE70F", () => SetDockEditing(!editingDock));
            Item("Organizar desde la lista…", "\uE8A4", ShowOrder);
            menu.Items.Add(new MenuFlyoutSeparator());
            Item("Mover a la izquierda", "\uE76B", () => ShiftWidget(id, -1)); Item("Mover a la derecha", "\uE76C", () => ShiftWidget(id, 1));
            menu.Items.Add(new MenuFlyoutSeparator());
            Item("Añadir widgets…", "\uE710", ShowGallery); Item("Quitar este widget", "\uE74D", () => RemoveWidgetAnimated(id));
            TrackDockMenu(menu);
            tile.ContextRequested += (_, args) => {
                args.Handled = true;
                menu.ShowAt(tile, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Placement = settings.Edge == "top" ? Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom : Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Top });
            };
            row.Children.Add(new Border { Child = tile, Tag = id });
        }
        AddDivider();
        if (editingDock)
        {
            var state = Ui.Stack(3); state.VerticalAlignment = VerticalAlignment.Center;
            state.Children.Add(Ui.Text("EDITANDO", 10, Ui.Light ? "#376F66" : "#B9E9D6", true));
            state.Children.Add(Ui.Text(settings.DockLocked ? "Arrastre bloqueado" : "Arrastra para mover", 10, Ui.MutedColor));
            row.Children.Add(new Border { Child = state, Padding = new Thickness(8, 0, 8, 0) });
            var doneContent = Ui.Stack(6, true); doneContent.Children.Add(Ui.Glyph("\uE73E", 16)); doneContent.Children.Add(Ui.Text("Listo", 12, bold: true));
            var done = Tile(doneContent, "Terminar edición del dock", () => SetDockEditing(false), 84); done.Background = Ui.Brush(Ui.Surface); row.Children.Add(done);
        }
        else
        {
        row.Children.Add(Tile(Ui.Glyph("\uE710", 20), "Añadir widgets", ShowGallery, 42));
        var edit = Tile(Ui.Glyph(editingDock ? "\uE73E" : "\uE70F", 19, editingDock ? Ui.Light ? "#376F66" : "#B9E9D6" : Ui.MutedColor), editingDock ? "Terminar organización" : "Organizar widgets en la barra", () => SetDockEditing(!editingDock), 42);
        if (editingDock) edit.Background = Ui.Brush(Ui.Surface);
        row.Children.Add(edit);
        var preferences = Tile(Ui.Glyph("\uE713", 20, Ui.MutedColor), "Personalizar Brisa", ShowSettings, 42);
        preferences.RightTapped += (_, args) => { if (windowsIntegration != null) { args.Handled = true; windowsIntegration.ShowQuickMenu(); } };
        row.Children.Add(preferences);
        }
        row.Measure(new Windows.Foundation.Size(double.PositiveInfinity, 80));
        var a = DockWorkArea; double dpi = Native.Dpi(this); fitScale = settings.Scale;
        double contentWidth = row.DesiredSize.Width;
        // Keep hover overflow inside the scroll viewport, including at either end.
        double sideInset = Math.Max(settings.DockPadding, Math.Ceiling(70 * (settings.Magnification - 1) / 2 + 3));
        double natural = (contentWidth + sideInset * 2 + 2) * fitScale;
        double width = settings.WidthPercent > 0 ? (a.Width / dpi - settings.EdgeDistance * 2) * settings.WidthPercent / 100d : natural;
        width = Math.Clamp(width, Math.Min(240, a.Width / dpi - 16), a.Width / dpi - 16);
        row.RenderTransform = null;
        dockContent.Width = contentWidth * fitScale; dockContent.Height = Math.Max(Ui.DockTileHeight, row.DesiredSize.Height) * fitScale;
        double dockHeight = Math.Ceiling(Math.Max(90, Ui.DockTileHeight * settings.Magnification + 12));
        dockViewport.Width = (contentWidth + sideInset * 2) * fitScale; dockViewport.Height = dockHeight * fitScale;
        dockContent.Margin = new Thickness(sideInset * fitScale, 0, sideInset * fitScale, 0);
        shell.Padding = new Thickness(0);
        row.HorizontalAlignment = HorizontalAlignment.Left;
        Native.Resize(this, (int)(width * dpi), (int)(dockHeight * fitScale * dpi));
        building = false; PositionDock(); Tick();
        AnimateWidgetPositions(previousPositions, previousApps);
    }
    void AddDivider() => row.Children.Add(new Border { Width = 1, Height = 28, Background = Ui.Brush(Ui.Light ? "#22000000" : "#30FFFFFF"), Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
    Button Tile(UIElement content, string name, Action action, double width)
    {
        Button? button = null;
        button = Ui.Button(content, name, () => { activeAnchor = button; bool previous = openingPanelFromDock; openingPanelFromDock = true; try { action(); } finally { openingPanelFromDock = previous; } }); button.Width = width; button.Height = Ui.DockTileHeight; button.Padding = new Thickness(6); button.Background = Ui.Brush(width > 50 ? Ui.TileSurface : "#00FFFFFF"); button.BorderThickness = new Thickness(0); button.CornerRadius = new(14);
        if (!settings.HoverNames) ToolTipService.SetToolTip(button, null);
        var transform = new CompositeTransform(); button.RenderTransform = transform; button.RenderTransformOrigin = new(.5, .5);
        button.PointerEntered += (_, _) => { if (!Ui.ReducedMotion) { Ui.Animate(transform, "ScaleX", width < 70 ? settings.Magnification : 1.025, 130); Ui.Animate(transform, "ScaleY", width < 70 ? settings.Magnification : 1.025, 130); Ui.Animate(transform, "TranslateY", settings.Edge == "bottom" ? -2 : 2, 130); } };
        button.PointerExited += (_, _) => { Ui.Animate(transform, "ScaleX", 1, 160); Ui.Animate(transform, "ScaleY", 1, 160); Ui.Animate(transform, "TranslateY", 0, 160); };
        return button;
    }
    FrameworkElement BuildWidget(WidgetDefinition d)
    {
        if (d.Id == "apps") return BuildApps();
        if (d.Id == "music") return MusicTile();
        if (d.Id == "notes") return NotesTile();
        if (d.Id == "system") return SystemDockTile();
        if (d.Id is "claude" or "chatgpt") return UsageTile(d.Id);
        if (d.Id == "ai-limits") return AggregateUsageTile();
        var local = BuildLocalWidget(d); if (local != null) return local;
        var stack = Ui.Stack(2); stack.Width = d.Width - 12; stack.HorizontalAlignment = HorizontalAlignment.Center;
        var value = Ui.Text("", 18, bold: true); value.TextAlignment = TextAlignment.Center; value.TextWrapping = TextWrapping.NoWrap; value.HorizontalAlignment = HorizontalAlignment.Center;
        var caption = Ui.Text(d.Name.ToUpperInvariant(), 10, Ui.MutedColor); caption.CharacterSpacing = 100; caption.Height = 14; caption.TextWrapping = TextWrapping.NoWrap; caption.MaxWidth = d.Width - 12; caption.TextTrimming = TextTrimming.CharacterEllipsis; caption.TextAlignment = TextAlignment.Center; caption.HorizontalAlignment = HorizontalAlignment.Center;
        var face = new Grid { Height = 34, HorizontalAlignment = HorizontalAlignment.Stretch };
        FontIcon? weatherIcon = null;
        if (d.Id == "weather") { weatherIcon = Ui.Glyph("\uE753", 20, Ui.Light ? "#987122" : "#E8CE8A"); var weatherFace = Ui.Stack(5, true); weatherFace.HorizontalAlignment = HorizontalAlignment.Center; weatherFace.Children.Add(weatherIcon); weatherFace.Children.Add(value); face.Children.Add(weatherFace); }
        else if (d.Id == "sound") { var glyph = Ui.Glyph(d.Glyph, 23); glyph.HorizontalAlignment = HorizontalAlignment.Center; face.Children.Add(glyph); }
        else face.Children.Add(value);
        QuotaMeter? taskProgress = null;
        if (d.Id == "tasks") { taskProgress = new QuotaMeter(0, 3, Ui.Light ? "#467C6C" : "#B8E5D4") { Width = d.Width - 28, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center }; face.Children.Add(taskProgress); }
        QuotaMeter? batteryProgress = null;
        if (d.Id == "battery") { batteryProgress = new QuotaMeter(0, 3, Ui.Accent) { Width = d.Width - 26, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center }; face.Children.Add(batteryProgress); }
        stack.Children.Add(face); stack.Children.Add(caption);
        Action action = d.Id switch { "clock" => ShowCalendar, "tasks" => ShowTasks, "system" => ShowSystem, "battery" => ShowBattery, "sound" => ShowSound, "notes" => ShowNotes, "focus" => ShowFocus, "music" => ShowMusic, "weather" => ShowWeather, _ => ShowGallery };
        updates[d.Id] = () =>
        {
            switch (d.Id)
            {
                case "clock": value.FontSize = settings.Clock12Hour && settings.ClockSeconds ? 13 : 16; value.Text = DateTime.Now.ToString(settings.Clock12Hour ? (settings.ClockSeconds ? "hh:mm:ss tt" : "hh:mm tt") : (settings.ClockSeconds ? "HH:mm:ss" : "HH:mm")); caption.Text = DateTime.Now.ToString("ddd d MMM", CultureInfo.GetCultureInfo("es-ES")).ToUpperInvariant(); break;
                case "system": value.Text = $"{stats.Cpu:0}%   {stats.MemoryPercent:0}%"; caption.Text = "CPU         RAM"; break;
                case "battery": value.Text = stats.Battery is byte b ? $"{b}%" : "—"; caption.Text = stats.Charging ? "CARGANDO" : stats.Plugged ? "CORRIENTE" : "ENERGÍA"; batteryProgress!.Visibility = stats.Battery.HasValue ? Visibility.Visible : Visibility.Collapsed; batteryProgress.Set(stats.Battery ?? 0, stats.Battery < 20 ? Ui.Light ? "#A53E32" : "#F2A995" : Ui.Accent); break;
                case "tasks": int total = settings.Tasks.Count(t => !t.Archived), done = settings.Tasks.Count(t => !t.Archived && t.Done); value.Text = total == 0 ? "0" : $"{done}/{total}"; if (taskProgress != null) { taskProgress.Set(total == 0 ? 0 : done * 100d / total, Ui.Light ? "#467C6C" : "#B8E5D4"); taskProgress.Visibility = total == 0 ? Visibility.Collapsed : Visibility.Visible; } break;
                case "focus": value.Text = FormatFocus(); caption.Text = focusSession.Phase == FocusPhase.Work ? "FOCO" : "DESCANSO"; break;
                case "music": value.Text = "♫"; caption.Text = "MULTIMEDIA"; break;
                case "weather": value.Text = weather != null ? $"{weather.Temperature:0}\u00b0" : ""; value.Visibility = weather == null ? Visibility.Collapsed : Visibility.Visible; caption.Text = weather != null ? settings.WeatherLocation?.Name.ToUpperInvariant() : "ELIGE CIUDAD"; break;
            }
        };
        return Tile(stack, d.Name, action, d.Width);
    }
    async Task LoadIconAsync(Grid holder, string target, double? size = null)
    {
        try
        {
            var icon = await ShellIcons.Read(target); if (icon == null || closing) return;
            var image = new Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap(icon.Width, icon.Height);
            using (var stream = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsStream(image.PixelBuffer)) stream.Write(icon.Pixels);
            image.Invalidate(); holder.Children.Clear(); holder.Children.Add(new Image { Source = image, Width = size ?? settings.IconSize, Height = size ?? settings.IconSize });
        }
        catch { /* Keep the vector fallback when the shell cannot provide an icon. */ }
    }
    void PositionDock()
    {
        var size = AppWindow.Size; int gap = (int)(settings.EdgeDistance * Native.Dpi(this));
        reservation?.Update(DockReservation.Enabled(settings.ReserveSpace, settings.AutoHide), DockBounds, settings.Edge == "top", DockReservation.Thickness(size.Height, gap, DockBounds.Height));
        var a = DockWorkArea;
        var position = new PointInt32(a.X + (a.Width - size.Width) / 2, settings.Edge == "top" ? a.Y + gap : a.Y + a.Height - size.Height - gap);
        if (reservation?.Active == true && reservation.Band is { } band)
            position = new PointInt32(band.X + (band.Width - size.Width) / 2, settings.Edge == "top" ? band.Y + gap : band.Y + band.Height - size.Height - gap);
        if (dockBehavior != null) dockBehavior.Place(position); else AppWindow.Move(position);
    }
    void Tick()
    {
        if (closing) return;
        long tickStarted = Stopwatch.GetTimestamp();
        try
        {
        CheckMonitorTopology();
        Native.EnsureZOrder(this, settings.AlwaysOnTop);
        WindowDiagnostics.Write(this, shell, panel, background.RunningCount);
        stats.Read(); var foreground = DesktopWindows.Foreground; foreach (var update in appIndicators) update(foreground); TickLocalWidgets(); foreach (var action in updates.Values) action(); TickProductivity();
        if (panel?.AppWindow.IsVisible == true) livePanelUpdate?.Invoke();
        TickCalendarReminders();
        }
        finally { RuntimeHealth.Tick(Stopwatch.GetElapsedTime(tickStarted).TotalMilliseconds); }
        SampleEndurance();
    }
    void ShiftWidget(string id, int delta) { if (WidgetOrder.MoveTo(settings.Widgets, id, settings.Widgets.IndexOf(id) + delta)) { Save(); BuildDock(); } }
    void MoveWidget(string id, string target, bool after) { if (WidgetOrder.MoveRelative(settings.Widgets, id, target, after)) { Save(); BuildDock(); } }
    void ShowPage(string id, string title, string description, UIElement body, bool wide = false)
    {
        if (closing) return;
        title = id switch { "system" => "Sistema", "tasks" => "Tareas", "calendar" => "Calendario", "timer" => "Temporizador", "stopwatch" => "Cronómetro", "focus" => "Foco", "weather" => "Tiempo", "music" => "Reproducción", "sound" => "Sonido", "notes" => "Notas", "battery" => "Batería", "world-time" => "Hora mundial", "storage" => "Almacenamiento", "network" => "Red", "search" => "Buscar", "recycle-bin" => "Papelera", _ => title };
        livePanelUpdate = null;
        if (panel == null) { panel = new PanelWindow(ConfigureKeyboard); panel.Dismissed += RestoreDockFocus; panel.Closed += (_, _) => panel = null; }
        dockBehavior?.Reveal();
        double? anchorX = null;
        try
        {
            if (activeAnchor?.XamlRoot != null && activeAnchor.XamlRoot == shell.XamlRoot)
            {
                var point = activeAnchor.TransformToVisual(shell).TransformPoint(new Windows.Foundation.Point(activeAnchor.ActualWidth / 2, 0));
                anchorX = AppWindow.Position.X + point.X * Native.Dpi(this);
            }
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        ConfigurePageBack(id);
        Native.Style(panel, settings.AlwaysOnTop); panel.ShowPage(id, title, description, body, this, DockWorkArea, wide, anchorX, settings.Scale, openingPanelFromDock);
    }
    void ShowMessage(string title, string message) { messageReturn = PageAction(panel?.PageId ?? "") ?? PageAction(PanelNavigation.Parent(panel?.PageId ?? "") ?? "settings") ?? ShowSettings; ShowPage("message", title, "BRISA", Ui.Text(message, 14)); }
    void Launch(string target) { try { if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) target = AppLaunchers.PreferredTarget(target); if (AppTargets.AppId(target) != null) Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "\"" + target + "\"", UseShellExecute = true }); else Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { ShowMessage("No se pudo abrir", "El acceso ya no está disponible. Puedes quitarlo y añadirlo de nuevo."); } }
    async void AddApp()
    {
        if (pickingShortcut) return;
        pickingShortcut = true;
        try { var picker = new Windows.Storage.Pickers.FileOpenPicker(); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this)); picker.FileTypeFilter.Add("*"); var file = await picker.PickSingleFileAsync(); if (file != null && !closing) { if (settings.Apps.Count >= 20) { ShowMessage("Dock completo", "Puedes añadir hasta 20 accesos. Quita alguno antes de añadir otro."); return; } if (PinApplication(System.IO.Path.GetFileNameWithoutExtension(file.Name), file.Path)) ShowAppsManager(); else ShowMessage("Acceso ya fijado", "Este acceso ya está guardado en Aplicaciones y carpetas. Allí puedes mostrar la sección en el dock."); } }
        catch { ShowMessage("No se pudo elegir el archivo", "Vuelve a abrir el selector de aplicaciones."); }
        finally { pickingShortcut = false; }
    }
}
