using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    string installedSearch = "";
    string shortcutFeedback = "";
    int installedSource, installedRequest, shortcutRequest;
    void ShowInstalledApps() => OpenInstalledApps(false);
    async void OpenInstalledApps(bool force)
    {
        int request = ++installedRequest;
        var body = Ui.Stack(12); body.MinHeight = 540;
        body.Children.Add(Ui.Text("Fijar añade un acceso a la sección Aplicaciones de tu dock. Esa sección se mostrará automáticamente.", 12, Ui.MutedColor));
        var search = new TextBox { Text = installedSearch, PlaceholderText = "Buscar aplicaciones y juegos…", CornerRadius = new CornerRadius(10) };
        AutomationProperties.SetName(search, "Buscar aplicaciones instaladas"); body.Children.Add(search);
        var source = new ComboBox { ItemsSource = new[] { "Todas", "Windows", "Menú Inicio", "Steam" }, SelectedIndex = installedSource, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(source, "Origen de las aplicaciones"); body.Children.Add(source);
        var status = Ui.Status(12, Ui.MutedColor); status.Text = "Buscando aplicaciones en este equipo…"; body.Children.Add(status);
        var feedback = Ui.Status(12, Ui.Accent); feedback.Text = shortcutFeedback; body.Children.Add(feedback);
        var list = Ui.Stack(8);
        var results = new ScrollViewer { Content = list, Height = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, IsTabStop = false }; body.Children.Add(results);
        var tools = new Grid { ColumnSpacing = 8 }; tools.ColumnDefinitions.Add(new()); tools.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        tools.Children.Add(Ui.Button("Elegir archivo o acceso…", "Elegir archivo o acceso", AddApp));
        var refresh = Ui.Button(Ui.Glyph("\uE72C", 16), "Actualizar aplicaciones conservando la búsqueda", () => OpenInstalledApps(true)); Grid.SetColumn(refresh, 1); tools.Children.Add(refresh); body.Children.Add(tools);
        body.Children.Add(Ui.Button("Organizar mis accesos", "Organizar aplicaciones y grupos", ShowAppsManager));
        search.TextChanged += (_, _) => installedSearch = search.Text;
        source.SelectionChanged += (_, _) => installedSource = source.SelectedIndex;
        ShowPage("installed-apps", "Añadir accesos", "APLICACIONES · ARCHIVOS · CARPETAS", body);
        try
        {
            var installed = await InstalledApplications.Read(force);
            if (closing || request != installedRequest || body.XamlRoot == null || panel?.PageId != "installed-apps" || !panel.AppWindow.IsVisible) return;
            void Render()
            {
                list.Children.Clear(); string filter = source.SelectedItem as string ?? "Todas";
                var matches = installed.Where(a => (filter == "Todas" || a.Source == filter) && a.Name.Contains(search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToArray();
                status.Text = matches.Length == 0 ? "No hay resultados. También puedes elegir un archivo o acceso." : (matches.Length == 1 ? "1 aplicación" : $"{matches.Length} aplicaciones") + (matches.Length > 60 ? " · Escribe para acotar la lista" : "");
                var pinnedApps = settings.Apps.GroupBy(a => ShortcutKey(a.Target), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                foreach (var app in matches.Take(60))
                {
                    var row = new Grid { ColumnSpacing = 12 }; row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
                    var icon = new Grid { Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center }; icon.Children.Add(Ui.Glyph("\uE8A5", 24, Ui.Accent)); _ = LoadIconAsync(icon, app.Target, 32); row.Children.Add(icon);
                    var copy = Ui.Stack(3); var name = Ui.Text(app.Name, 13, bold: true); name.TextTrimming = TextTrimming.CharacterEllipsis; name.MaxLines = 1; copy.Children.Add(name); copy.Children.Add(Ui.Text(app.Source, 11, Ui.MutedColor)); Grid.SetColumn(copy, 1); row.Children.Add(copy);
                    pinnedApps.TryGetValue(ShortcutKey(app.Target), out var pinnedApp);
                    bool pinned = pinnedApp != null;
                    var pin = Ui.Button(pinned ? "Desfijar" : "Fijar", (pinned ? "Desfijar " : "Fijar ") + app.Name + (pinned ? " del dock" : " en el dock"), () =>
                    {
                        if (pinnedApp != null) UnpinShortcut(pinnedApp.Id);
                        else if (!PinApplication(app.Name, app.Target)) return;
                        feedback.Text = shortcutFeedback;
                        double offset = results.VerticalOffset; Render(); results.ChangeView(null, offset, null, true);
                    });
                    pin.IsEnabled = pinned || settings.Apps.Count < 20; Grid.SetColumn(pin, 2); row.Children.Add(pin);
                    list.Children.Add(Ui.Card(row, 10));
                }
                if (settings.Apps.Count >= 20) status.Text += " · Has alcanzado los 20 accesos";
            }
            var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            debounce.Tick += (_, _) => { debounce.Stop(); if (panel?.PageId == "installed-apps" && panel.AppWindow.IsVisible) { results.ChangeView(null, 0, null, true); Render(); } };
            body.Unloaded += (_, _) => debounce.Stop();
            search.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); };
            source.SelectionChanged += (_, _) => { debounce.Stop(); results.ChangeView(null, 0, null, true); Render(); }; Render();
        }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException or IOException)
        { status.Text = "No se pudo leer la lista. Puedes actualizarla o elegir un acceso."; }
    }
    bool IsPinned(string target)
    {
        string identity = ShortcutKey(target);
        return settings.Apps.Any(a => ShortcutKey(a.Target) == identity);
    }
    static string ShortcutKey(string target) => AppTargets.ShortcutKey(ShortcutIdentity.Resolve(target), Environment.GetFolderPath(Environment.SpecialFolder.Windows));
    void UnpinShortcut(string id, bool showManager = false)
    {
        var shortcut = settings.Apps.FirstOrDefault(a => a.Id == id); if (shortcut == null) return;
        AppGroups.RemoveShortcut(settings, id);
        shortcutFeedback = "Se ha retirado el acceso a " + shortcut.Name + " del dock.";
        if (settings.ShowRunningApps && RunningApps.ForTarget(ShortcutIdentity.Resolve(shortcut.Target), runningApps) != null)
            shortcutFeedback = shortcut.Name + " ya no está fijada. Su icono seguirá visible mientras tenga ventanas abiertas.";
        Save(); BuildDock(); if (showManager) ShowAppsManager();
    }
    bool PinApplication(string name, string target, bool folder = false)
    {
        if (settings.Apps.Count >= 20 || IsPinned(target)) return false;
        var added = new AppShortcut(Guid.NewGuid().ToString("N"), name, target, Folder: folder);
        settings.Apps.Add(added); PinnedLayout.EnsureVisible(settings);
        shortcutFeedback = name + " se ha añadido al dock, en Aplicaciones. Puedes organizarlo en Mis accesos.";
        Save(); BuildDock();
        DispatcherQueue.TryEnqueue(() => { if (!closing) appMotionTargets.FirstOrDefault(entry => entry.Key == "app:" + added.Id).Tile?.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false }); });
        return true;
    }
    async void ShowShortcutEditor(string id)
    {
        var app = settings.Apps.FirstOrDefault(a => a.Id == id); if (app == null) return;
        int request = ++shortcutRequest;
        var body = Ui.Stack(12); var icon = new Grid { Width = 48, Height = 48, HorizontalAlignment = HorizontalAlignment.Center }; icon.Children.Add(Ui.Glyph("\uE8A5", 32, Ui.Accent)); _ = LoadIconAsync(icon, app.Target, 48); body.Children.Add(icon);
        var name = new TextBox { Header = "Nombre del acceso", Text = app.Name, MaxLength = 60 }; AutomationProperties.SetName(name, "Nombre del acceso"); body.Children.Add(name);
        string identity = ShortcutIdentity.Resolve(app.Target);
        string kind = app.Folder ? "Carpeta" : AppTargets.AppId(identity) != null ? "Aplicación de Windows" : AppTargets.SteamId(identity) != null ? "Juego de Steam" : (identity.StartsWith(AppLauncherPaths.UpdaterPrefix) || identity.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) ? "Aplicación de escritorio" : "Archivo o acceso";
        body.Children.Add(Ui.Text(kind, 12, Ui.MutedColor));
        var target = Ui.Text(app.Target, 11, Ui.MutedColor); target.IsTextSelectionEnabled = true; target.TextWrapping = TextWrapping.Wrap;
        body.Children.Add(new Expander { Header = "Ubicación del acceso", Content = target, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        if (Path.IsPathFullyQualified(app.Target) && !File.Exists(app.Target) && !Directory.Exists(app.Target)) body.Children.Add(Ui.Text(AppLaunchers.PreferredTarget(app.Target) != app.Target ? "Brisa usará el acceso actualizado de esta aplicación." : "Este acceso ya no está disponible. Puedes quitarlo y volver a añadirlo.", 12, Ui.MutedColor));
        var windowStatus = Ui.Status(12, Ui.MutedColor); if (!app.Folder) windowStatus.Text = "Buscando ventanas…"; body.Children.Add(windowStatus);
        RunningApp? available = null;
        var windows = Ui.Button("Ver ventanas", "Ver ventanas de " + app.Name, () => { if (available != null) ShowAppWindows(available, app.Target); });
        windows.IsEnabled = false; windows.Visibility = app.Folder ? Visibility.Collapsed : Visibility.Visible; body.Children.Add(windows);
        var error = Ui.Status(12, Ui.MutedColor); body.Children.Add(error);
        body.Children.Add(Ui.Button("Guardar nombre", "Guardar nombre del acceso", () => { if (string.IsNullOrWhiteSpace(name.Text)) { error.Text = "Escribe un nombre."; return; } int at = settings.Apps.FindIndex(a => a.Id == id); if (at < 0) return; settings.Apps[at] = app with { Name = name.Text.Trim() }; Save(); BuildDock(); ShowAppsManager(); }, true));
        body.Children.Add(Ui.Button("Desfijar del dock", "Desfijar " + app.Name, () => UnpinShortcut(id, true)));
        body.Children.Add(Ui.Button("Volver", "Volver a mis aplicaciones", ShowAppsManager));
        ShowPage("shortcut-editor", "Editar acceso", "TU DOCK · A TU MANERA", body);
        if (app.Folder) return;
        try
        {
            var apps = await Task.Run(DesktopWindows.Read);
            if (closing || request != shortcutRequest || body.XamlRoot == null || panel?.PageId != "shortcut-editor" || !panel.AppWindow.IsVisible) return;
            available = RunningApps.ForTarget(ShortcutIdentity.Resolve(app.Target), apps);
            windowStatus.Text = available == null ? "Sin ventanas abiertas" : Ui.Count(available.Windows.Count, "ventana abierta", "ventanas abiertas");
            windows.IsEnabled = available != null; windows.Visibility = available == null ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception or InvalidOperationException) { windowStatus.Text = "No se pudo consultar las ventanas."; windows.Visibility = Visibility.Collapsed; }
    }
}
