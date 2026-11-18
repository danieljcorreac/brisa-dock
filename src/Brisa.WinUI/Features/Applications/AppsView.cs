using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
namespace Brisa.WinUI;

public sealed partial class MainWindow
{
    IReadOnlyList<RunningApp> runningApps = [];
    readonly List<Action<nint>> appIndicators = [];
    bool readingApps, appCatalogReady;
    DateTimeOffset lastAppPoll;
    async Task RefreshAppsAsync()
    {
        if (closing || editingDock || draggingPinned || openDockMenus > 0 || readingApps || !settings.Widgets.Contains("apps") || DateTimeOffset.Now - lastAppPoll < TimeSpan.FromSeconds(2)) return;
        readingApps = true; lastAppPoll = DateTimeOffset.Now;
        try
        {
            if (!appCatalogReady) { await InstalledApplications.Read(); appCatalogReady = true; }
            if (lifetime.IsCancellationRequested) return;
            var next = await Task.Run(DesktopWindows.Read);
            if (lifetime.IsCancellationRequested || editingDock || draggingPinned || openDockMenus > 0) return;
            string Signature(IReadOnlyList<RunningApp> apps) => string.Join("|", apps.Select(a => a.Key + ":" + string.Join(",", a.Windows.Select(w => w.Handle))));
            bool changed = Signature(next) != Signature(runningApps); runningApps = next;
            if (changed) BuildDock();
        }
        finally { readingApps = false; }
    }
    FrameworkElement BuildApps()
    {
        appIndicators.Clear();
        var apps = Ui.Stack(8, true);
        var represented = new HashSet<string>();
        foreach (string entry in PinnedLayout.Visible(settings))
        {
            if (entry.StartsWith("group:", StringComparison.Ordinal))
            {
                var collection = settings.AppGroups.First(g => "group:" + g.Id == entry);
                apps.Children.Add(GroupTile(collection));
                foreach (var match in runningApps.Where(a => GroupMembers(collection).Any(m => RunningApps.Matches(ShortcutIdentity.Resolve(m.Target), a)))) represented.Add(match.Key);
                continue;
            }
            var shortcut = settings.Apps.First(a => "app:" + a.Id == entry);
            string resolved = ShortcutIdentity.Resolve(shortcut.Target);
            var group = RunningApps.ForTarget(resolved, runningApps);
            foreach (var match in runningApps.Where(a => RunningApps.Matches(resolved, a))) represented.Add(match.Key);
            AddAppTile(shortcut.Name, shortcut.Target, group?.Key, shortcut);
        }
        var unpinned = settings.ShowRunningApps ? runningApps.Where(a => !represented.Contains(a.Key)).ToArray() : [];
        if (unpinned.Length > 0) apps.Children.Add(new Border { Width = 1, Height = 24, Margin = new Thickness(5, 0, 5, 0), Background = Ui.Brush(Ui.MutedColor), Opacity = .35 });
        foreach (var group in unpinned) AddAppTile(group.Name, group.LaunchTarget, group.Key, null);
        var add = Tile(Ui.Glyph("\uE710", 16, Ui.MutedColor), "A\u00f1adir aplicaci\u00f3n", ShowInstalledApps, 30);
        var addMenu = new MenuFlyout(); var choose = new MenuFlyoutItem { Text = "Elegir archivo o acceso…", Icon = Ui.Glyph("\uE8E5", 16) }; choose.Click += (_, _) => AddApp(); addMenu.Items.Add(choose); var folderItem = new MenuFlyoutItem { Text = "Añadir carpeta…", Icon = Ui.Glyph("\uE8B7", 16) }; folderItem.Click += (_, _) => AddFolderShortcut(); addMenu.Items.Add(folderItem); var organize = new MenuFlyoutItem { Text = "Organizar aplicaciones y grupos", Icon = Ui.Glyph("\uE8F1", 16) }; organize.Click += (_, _) => ShowAppsManager(); addMenu.Items.Add(organize); AttachAppMenu(add, addMenu); apps.Children.Add(add);
        return new Border { Child = apps };

        void AddAppTile(string name, string target, string? key, AppShortcut? shortcut)
        {
            var holder = new Grid { Width = settings.IconSize, Height = settings.IconSize };
            holder.Children.Add(Ui.Glyph(shortcut?.Folder == true || shortcut?.Id == "files" ? "\uE8B7" : "\uE8A5", 30, shortcut?.Id == "files" ? "#F5C565" : "#9DC6FF"));
            _ = LoadIconAsync(holder, target);
            var content = new Grid { Width = settings.IconSize, Height = 52 };
            holder.VerticalAlignment = VerticalAlignment.Center; holder.Margin = new Thickness(0, 0, 0, 6); content.Children.Add(holder);
            var indicator = new Border { Width = 5, Height = 3, CornerRadius = new CornerRadius(2), Background = Ui.Brush(Ui.Light ? "#367669" : "#B9E9D6"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Visibility = key == null ? Visibility.Collapsed : Visibility.Visible };
            content.Children.Add(indicator);
            string resolvedTarget = ShortcutIdentity.Resolve(target);
            IReadOnlyList<RunningApp>? observed = null; RunningApp? cachedApp = null;
            RunningApp? CurrentApp()
            {
                if (ReferenceEquals(observed, runningApps)) return cachedApp;
                observed = runningApps;
                return cachedApp = shortcut == null ? runningApps.FirstOrDefault(a => a.Key == key) : RunningApps.ForTarget(resolvedTarget, runningApps);
            }
            nint beforeClick = DesktopWindows.Foreground;
            void ActivateApp()
            {
                if (shortcut?.Folder == true) { ShowFolderStack(target); return; }
                var current = CurrentApp();
                if (current == null) Launch(target);
                else if (current.Windows.Count == 1) SwitchToWindow(current.Windows[0], true, beforeClick);
                else ShowAppWindows(current, target);
            }
            var button = Tile(content, name, ActivateApp, settings.IconSize + 20);
            appMotionTargets.Add((button, shortcut == null ? "running:" + key : "app:" + shortcut.Id));
            void RememberForeground()
            {
                nint foreground = DesktopWindows.Foreground;
                if (runningApps.Any(a => a.Windows.Any(w => w.Handle == foreground.ToInt64()))) beforeClick = foreground;
            }
            button.PointerEntered += (_, _) => RememberForeground();
            button.PointerPressed += (_, _) => RememberForeground();
            button.Background = Ui.Brush(settings.AppCards ? Ui.Surface : "#00FFFFFF");
            if (key != null) appIndicators.Add(foreground => { var current = CurrentApp(); indicator.Width = current?.Windows.Any(w => w.Handle == foreground.ToInt64()) == true ? 16 : 5; });
            var menu = new MenuFlyout();
            void Item(string text, Action action) { var item = new MenuFlyoutItem { Text = text, Icon = Ui.Glyph(AppMenuGlyph(text), 16) }; item.Click += (_, _) => action(); menu.Items.Add(item); }
            Item(shortcut?.Folder == true ? "Abrir en el Explorador" : "Abrir nuevo", () => Launch(target));
            if (shortcut?.Folder != true) Item("Archivos recientes…", () => ShowRecentDocuments(name, shortcut?.Target, CurrentApp()?.Windows.FirstOrDefault()));
            if (key != null) Item("Ver ventanas", () => { if (CurrentApp() is { } current) ShowAppWindows(current, target); });
            menu.Items.Add(new MenuFlyoutSeparator());
            if (shortcut != null) Item("Editar acceso", () => ShowShortcutEditor(shortcut.Id));
            if (shortcut != null)
            {
                EnablePinnedDrag(button, shortcut.Id, null, () => { activeAnchor = button; ActivateApp(); });
                Item("Mover a la izquierda", () => MovePinned("app:" + shortcut.Id, -1));
                Item("Mover a la derecha", () => MovePinned("app:" + shortcut.Id, 1));
                Item("Organizar grupos", ShowAppsManager);
                foreach (var collection in settings.AppGroups)
                    Item("Mover a " + collection.Name, () => { AppGroups.Assign(settings, shortcut.Id, collection.Id); Save(); BuildDock(); });
                menu.Items.Add(new MenuFlyoutSeparator()); Item("Desfijar del dock", () => UnpinShortcut(shortcut.Id));
            }
            else Item("Fijar en el dock", () =>
            {
                if (settings.Apps.Count >= 20) { ShowMessage("Dock completo", "Puedes fijar hasta 20 accesos."); return; }
                PinApplication(name, target);
            });
            AttachAppMenu(button, menu); apps.Children.Add(button);
        }
    }
    static string AppMenuGlyph(string text) => text switch
    {
        "Abrir nuevo" or "Abrir en el Explorador" => "\uE8E5", "Archivos recientes…" => "\uE81C", "Ver ventanas" => "\uE8A7", "Editar acceso" or "Editar grupo" => "\uE70F",
        "Mover a la izquierda" => "\uE76B", "Mover a la derecha" => "\uE76C", "Desfijar del dock" => "\uE77A", "Fijar en el dock" => "\uE718", _ => "\uE8F1"
    };
    void AttachAppMenu(FrameworkElement target, MenuFlyout menu)
    {
        TrackDockMenu(menu);
        target.ContextRequested += (_, args) => { args.Handled = true; menu.ShowAt(target, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Placement = settings.Edge == "top" ? Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom : Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Top }); };
    }
    void ShowAppWindows(string key)
    {
        var app = runningApps.FirstOrDefault(a => a.Key == key);
        if (app == null) { ShowMessage("Ventanas", "Esta aplicaci\u00f3n ya no tiene ventanas abiertas."); return; }
        ShowAppWindows(app);
    }
    void ShowAppWindows(RunningApp app, string? launchTarget = null)
    {
        var body = Ui.Stack(12); var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        int columns = app.Windows.Count > 1 ? 2 : 1;
        for (int column = 0; column < columns; column++) grid.ColumnDefinitions.Add(new()); int i = 0;
        foreach (var window in app.Windows)
        {
            if (i % columns == 0) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var content = Ui.Stack(8); content.Children.Add(new WindowThumbnail(() => panel, window));
            var title = Ui.Text(window.Title, 12, bold: true); title.MaxLines = 2; title.TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis; content.Children.Add(title);
            var controls = new Grid { ColumnSpacing = 6 }; controls.ColumnDefinitions.Add(new()); controls.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            controls.Children.Add(Ui.Button("Activar", "Activar " + window.Title, () => SwitchToWindow(window, false, 0), true));
            var close = Ui.Button(Ui.Glyph("\uE8BB", 12), "Cerrar " + window.Title, () =>
            {
                if (!DesktopWindows.RequestClose(window)) ShowMessage("Ventana cerrada", "Esta ventana ya no est\u00e1 disponible.");
                else panel?.AppWindow.Hide();
            }); Grid.SetColumn(close, 1); controls.Children.Add(close); content.Children.Add(controls);
            var card = Ui.Card(content, 12); Grid.SetColumn(card, i % columns); Grid.SetRow(card, i / columns); grid.Children.Add(card); i++;
        }
        body.Children.Add(grid);
        body.Children.Add(Ui.Text("Las vistas previas dependen de Windows. Las ventanas minimizadas o protegidas pueden mostrar su \u00faltimo fotograma o no ofrecer imagen.", 11, Ui.MutedColor));
        body.Children.Add(Ui.Button("Abrir nueva instancia", "Abrir nueva instancia", () => Launch(launchTarget ?? app.LaunchTarget)));
        ShowPage("app-windows", app.Name, (app.Windows.Count == 1 ? "1 VENTANA" : $"{app.Windows.Count} VENTANAS") + " · VISTAS PREVIAS", body, columns > 1);
    }
    void SwitchToWindow(AppWindowInfo window, bool toggle, nint foreground)
    {
        panel?.AppWindow.Hide();
        if (!DesktopWindows.Activate(window, toggle, foreground)) ShowMessage("No se pudo activar", "Windows no ha permitido activar esa ventana o ya se ha cerrado. Vuelve a intentarlo desde el dock.");
    }
}
