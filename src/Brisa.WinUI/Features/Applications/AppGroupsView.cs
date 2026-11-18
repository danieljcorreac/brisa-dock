using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
namespace Brisa.WinUI;

public sealed partial class MainWindow
{
    bool appAppearanceExpanded;
    FrameworkElement AppIconSizeOptions(Action refresh) => ChoiceUi.Group(3, new[] { ("Pequeños", 30), ("Medianos", 38), ("Grandes", 46) }.Select(option => ChoiceUi.Card(option.Item1, "", Ui.Glyph("\uE8A5", option.Item2, Ui.Accent), settings.IconSize == option.Item2, () => { settings.IconSize = option.Item2; Save(); BuildDock(); refresh(); })));
    static string GroupColor(string accent) => accent switch { "blue" => "#88BCFF", "peach" => "#F2BE93", "violet" => "#BAA6EE", "rose" => "#EBA8BD", _ => "#A7D8C5" };
    IEnumerable<AppShortcut> GroupMembers(AppGroup group) => group.AppIds.Select(id => settings.Apps.FirstOrDefault(a => a.Id == id)).OfType<AppShortcut>();
    Button GroupTile(AppGroup group)
    {
        var frame = new Grid { Width = settings.IconSize, Height = 52 };
        var preview = new Grid { Width = settings.IconSize, Height = settings.IconSize, Padding = new Thickness(4), CornerRadius = new CornerRadius(10), Background = Ui.Brush(GroupColor(group.Accent)) };
        for (int i = 0; i < 2; i++) { preview.ColumnDefinitions.Add(new()); preview.RowDefinitions.Add(new()); }
        var members = GroupMembers(group).ToArray(); var identities = members.Select(m => ShortcutIdentity.Resolve(m.Target)).ToArray(); int cell = 0;
        foreach (var app in members.Take(4))
        {
            var icon = new Grid { Width = (settings.IconSize - 10) / 2d, Height = (settings.IconSize - 10) / 2d };
            icon.Children.Add(Ui.Glyph("\uE8A5", 12, "#24364A")); _ = LoadIconAsync(icon, app.Target, (settings.IconSize - 10) / 2d);
            Grid.SetColumn(icon, cell % 2); Grid.SetRow(icon, cell / 2); preview.Children.Add(icon); cell++;
        }
        if (members.Length == 0) { var glyph = Ui.Glyph("\uE8F1", 22, "#24364A"); Grid.SetColumnSpan(glyph, 2); Grid.SetRowSpan(glyph, 2); preview.Children.Add(glyph); }
        preview.VerticalAlignment = VerticalAlignment.Center; preview.Margin = new Thickness(0, 0, 0, 6); frame.Children.Add(preview);
        var indicator = new Border { Height = 3, Width = 5, CornerRadius = new CornerRadius(2), Background = Ui.Brush(GroupColor(group.Accent)), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center };
        frame.Children.Add(indicator);
        appIndicators.Add(foreground =>
        {
            var windows = runningApps.Where(a => identities.Any(target => RunningApps.Matches(target, a))).SelectMany(a => a.Windows).ToArray();
            indicator.Visibility = windows.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            indicator.Width = windows.Any(w => w.Handle == foreground.ToInt64()) ? 16 : 5;
        });
        var tile = Tile(frame, $"Grupo {group.Name} \u00b7 {members.Length} accesos", () => ShowAppGroup(group.Id), settings.IconSize + 20);
        appMotionTargets.Add((tile, "group:" + group.Id));
        tile.Background = Ui.Brush(settings.AppCards ? Ui.Surface : "#00FFFFFF");
        var menu = new MenuFlyout();
        void Item(string label, Action action) { var entry = new MenuFlyoutItem { Text = label, Icon = Ui.Glyph(AppMenuGlyph(label), 16) }; entry.Click += (_, _) => action(); menu.Items.Add(entry); }
        Item("Editar grupo", () => ShowGroupEditor(group.Id));
        Item("Mover a la izquierda", () => ShiftGroup(group.Id, -1)); Item("Mover a la derecha", () => ShiftGroup(group.Id, 1));
        menu.Items.Add(new MenuFlyoutSeparator());
        Item("Deshacer grupo", () => { settings.AppGroups.RemoveAll(g => g.Id == group.Id); Save(); BuildDock(); });
        EnablePinnedDrag(tile, null, group.Id); AttachAppMenu(tile, menu); return tile;
    }
    void ShiftGroup(string id, int direction)
    {
        MovePinned("group:" + id, direction);
    }
    void MovePinned(string id, int direction) { if (PinnedLayout.Move(settings, id, direction)) { Save(); BuildDock(); } }
    void ShowAppGroup(string id)
    {
        var group = settings.AppGroups.FirstOrDefault(g => g.Id == id); if (group == null) return;
        var body = Ui.Stack(14); var members = GroupMembers(group).ToArray();
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 }; grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new());
        int index = 0;
        foreach (var app in members)
        {
            if (index % 2 == 0) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var card = Ui.Stack(7); card.HorizontalAlignment = HorizontalAlignment.Center;
            var icon = new Grid { Width = 36, Height = 36 }; icon.Children.Add(Ui.Glyph("\uE8A5", 26, GroupColor(group.Accent))); _ = LoadIconAsync(icon, app.Target, 36); card.Children.Add(icon);
            var name = Ui.Text(app.Name, 13, bold: true); name.TextAlignment = TextAlignment.Center; card.Children.Add(name);
            var running = RunningApps.ForTarget(ShortcutIdentity.Resolve(app.Target), runningApps);
            card.Children.Add(Ui.Text(running == null ? "Abrir" : $"{running.Windows.Count} ventanas", 10, Ui.MutedColor));
            var button = Ui.Button(card, "Abrir " + app.Name, () =>
            {
                if (app.Folder) { ShowFolderStack(app.Target); return; }
                var current = RunningApps.ForTarget(ShortcutIdentity.Resolve(app.Target), runningApps);
                if (current == null) { panel?.AppWindow.Hide(); Launch(app.Target); }
                else if (current.Windows.Count == 1) SwitchToWindow(current.Windows[0], false, 0);
                else ShowAppWindows(current, app.Target);
            });
            Grid.SetColumn(button, index % 2); Grid.SetRow(button, index / 2); grid.Children.Add(button); index++;
        }
        body.Children.Add(grid);
        if (members.Length == 0) body.Children.Add(Ui.Text("Tu grupo est\u00e1 listo. Elige los accesos que quieres reunir aqu\u00ed.", 14, Ui.MutedColor));
        body.Children.Add(Ui.Button("Editar este grupo", "Editar este grupo", () => ShowGroupEditor(id)));
        ShowPage("app-group", group.Name, $"{Ui.Count(members.Length, "acceso", "accesos")} \u00b7 TU COLECCI\u00d3N", body);
    }
    void ShowAppsManager()
    {
        var body = Ui.Stack(12);
        body.Children.Add(Ui.Text("Tus accesos abren aplicaciones, archivos o carpetas desde la sección Aplicaciones del dock.", 13, Ui.MutedColor));
        var locations = new List<TextBlock>();
        var visibility = new ToggleSwitch { Header = "Mostrar accesos en el dock", IsOn = settings.Widgets.Contains("apps") };
        var visibilityStatus = Ui.Text("", 12, Ui.MutedColor);
        void RefreshVisibility()
        {
            string count = Ui.Count(settings.Apps.Count, "acceso guardado", "accesos guardados");
            visibilityStatus.Text = count + (visibility.IsOn ? " · Visibles en el dock" : " · Sección oculta");
            foreach (var location in locations) location.Text = visibility.IsOn ? "En el dock · Editar" : "Guardado · Sección oculta";
        }
        RefreshVisibility(); visibility.Toggled += (_, _) => { if (visibility.IsOn) PinnedLayout.EnsureVisible(settings); else settings.Widgets.Remove("apps"); Save(); BuildDock(); RefreshVisibility(); };
        var visibilityCard = Ui.Stack(8); visibilityCard.Children.Add(visibility); visibilityCard.Children.Add(visibilityStatus); body.Children.Add(Ui.Card(visibilityCard, 12));
        if (!string.IsNullOrWhiteSpace(shortcutFeedback)) body.Children.Add(Ui.Text(shortcutFeedback, 12, Ui.Accent));
        var quick = Ui.Stack(8);
        quick.Children.Add(Ui.Text("ACCESOS RÁPIDOS", 11, Ui.MutedColor, true));
        void Quick(string label, string target)
        {
            bool pinned = IsPinned(target);
            var button = Ui.Button(label + (pinned ? " · Añadido" : " · Añadir"), "Añadir acceso a " + label, () => { if (PinApplication(label, target)) ShowAppsManager(); });
            button.IsEnabled = !pinned && settings.Apps.Count < 20; quick.Children.Add(button);
        }
        Quick("Explorador de archivos", "explorer.exe"); Quick("Calculadora", "calc.exe");
        quick.Children.Add(Ui.Button("Buscar Google Chrome", "Buscar Chrome entre las aplicaciones instaladas", () => { installedSearch = "Chrome"; installedSource = 0; ShowInstalledApps(); }));
        body.Children.Add(Ui.Text($"MIS ACCESOS · {settings.Apps.Count}/20", 11, Ui.MutedColor, true));
        body.Children.Add(Ui.Text("Arrastra desde los puntos para ordenar. Pulsa el nombre para editar o Desfijar para retirarlo.", 12, Ui.MutedColor));
        if (settings.Apps.Count == 0 && settings.AppGroups.Count == 0) body.Children.Add(Ui.Text("Aún no has fijado accesos. Explora las aplicaciones o elige uno de los accesos habituales.", 13, Ui.MutedColor));
        if (settings.Apps.Count > 0 || settings.AppGroups.Count > 0) body.Children.Add(BuildShortcutList(visibility, locations));
        body.Children.Add(Ui.Button("Explorar aplicaciones", "Buscar aplicaciones instaladas", ShowInstalledApps, true));
        body.Children.Add(Ui.Button("Elegir archivo o acceso…", "Añadir aplicación o archivo al dock", AddApp));
        body.Children.Add(Ui.Button("Añadir carpeta…", "Añadir carpeta con vista rápida", AddFolderShortcut));
        body.Children.Add(new Expander { Header = "Añadir accesos habituales", Content = quick, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        var appearance = Ui.Stack(10);
        appearance.Children.Add(AppIconSizeOptions(ShowAppsManager));
        var cards = new ToggleSwitch { Header = "Tarjetas detrás de los iconos", IsOn = settings.AppCards };
        cards.Toggled += (_, _) => { settings.AppCards = cards.IsOn; Save(); BuildDock(); }; appearance.Children.Add(cards);
        var appearanceExpander = new Expander { Header = "Tamaño y aspecto de los iconos", Content = appearance, IsExpanded = appAppearanceExpanded, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        appearanceExpander.RegisterPropertyChangedCallback(Expander.IsExpandedProperty, (_, _) => appAppearanceExpanded = appearanceExpander.IsExpanded); body.Children.Add(appearanceExpander);
        var running = new ToggleSwitch { Header = "Mostrar también aplicaciones abiertas", IsOn = settings.ShowRunningApps };
        running.Toggled += (_, _) => { settings.ShowRunningApps = running.IsOn; Save(); BuildDock(); }; body.Children.Add(running);
        body.Children.Add(Ui.Text("Los accesos fijados permanecen en el dock. Las aplicaciones abiertas aparecen mientras tengan ventanas.", 12, Ui.MutedColor));
        body.Children.Add(Ui.Text("Re\u00fane tus accesos en colecciones. Cada grupo aparece como un icono con una vista de sus aplicaciones.", 13, Ui.MutedColor));
        var create = Ui.Button("+ Crear grupo", "Crear grupo", () => ShowGroupEditor(null), true); create.IsEnabled = settings.AppGroups.Count < 12; body.Children.Add(create);
        body.Children.Add(Ui.Text("Al deshacer un grupo, sus accesos vuelven a la barra. Puedes reunir hasta 20 accesos en un m\u00e1ximo de 12 grupos.", 11, Ui.MutedColor));
        ShowPage("apps-manager", "Aplicaciones y carpetas", "MIS ACCESOS · GRUPOS", body);
    }
    void ShowGroupEditor(string? id, IEnumerable<string>? initialMembers = null)
    {
        var group = settings.AppGroups.FirstOrDefault(g => g.Id == id);
        var body = Ui.Stack(12);
        var name = new TextBox { Header = "Nombre del grupo", Text = group?.Name ?? "", PlaceholderText = "Trabajo, herramientas, juegos\u2026", MaxLength = 28, CornerRadius = new CornerRadius(10) }; AutomationProperties.SetName(name, "Nombre del grupo"); body.Children.Add(name);
        var accent = new ComboBox { Header = "Color", ItemsSource = new[] { "Menta", "Azul", "Melocot\u00f3n", "Violeta", "Rosa" }, SelectedIndex = Array.IndexOf(AppGroups.Accents, group?.Accent ?? "mint"), HorizontalAlignment = HorizontalAlignment.Stretch }; body.Children.Add(accent);
        body.Children.Add(Ui.Text("Elige accesos fijados. Si uno pertenece a otro grupo, se mover\u00e1 a este al guardar.", 12, Ui.MutedColor));
        var selected = new HashSet<string>(group?.AppIds ?? initialMembers?.ToList() ?? []);
        foreach (var app in settings.Apps)
        {
            var owner = settings.AppGroups.FirstOrDefault(g => g.AppIds.Contains(app.Id));
            var label = new Grid { ColumnSpacing = 9 }; label.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); label.ColumnDefinitions.Add(new());
            label.Children.Add(ShortcutListIcon("app:" + app.Id));
            var title = Ui.Text(app.Name + (owner != null && owner.Id != id ? " · " + owner.Name : ""), 13); title.MaxLines = 2; title.TextTrimming = TextTrimming.CharacterEllipsis; Grid.SetColumn(title, 1); label.Children.Add(title);
            var check = new CheckBox { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, IsChecked = selected.Contains(app.Id) }; AutomationProperties.SetName(check, app.Name);
            check.Checked += (_, _) => selected.Add(app.Id); check.Unchecked += (_, _) => selected.Remove(app.Id); body.Children.Add(check);
        }
        if (settings.Apps.Count == 0) body.Children.Add(Ui.Text("Añade primero un acceso desde Aplicaciones y carpetas para poder incluirlo en un grupo.", 12, Ui.MutedColor));
        var error = Ui.Status(12, Ui.Light ? "#AE3535" : "#F4ABAB"); body.Children.Add(error);
        body.Children.Add(Ui.Button("Guardar grupo", "Guardar grupo", () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { error.Text = "Escribe un nombre para el grupo."; name.Focus(FocusState.Programmatic); return; }
            string groupId = id ?? Guid.NewGuid().ToString("N");
            if (id == null && settings.AppGroups.Count >= 12) { error.Text = "Ya tienes 12 grupos."; return; }
            var updated = new AppGroup(groupId, name.Text.Trim(), [], AppGroups.Accents[Math.Clamp(accent.SelectedIndex, 0, AppGroups.Accents.Length - 1)]);
            int at = settings.AppGroups.FindIndex(g => g.Id == groupId);
            if (at < 0) settings.AppGroups.Add(updated); else settings.AppGroups[at] = updated;
            foreach (string appId in selected) AppGroups.Assign(settings, appId, groupId);
            Save(); BuildDock(); ShowAppGroup(groupId);
        }, true));
        body.Children.Add(Ui.Button("Cancelar", "Cancelar cambios del grupo", ShowAppsManager));
        if (group != null) body.Children.Add(Ui.Button("Deshacer grupo y conservar accesos", "Deshacer grupo y conservar accesos", () => { settings.AppGroups.RemoveAll(g => g.Id == group.Id); Save(); BuildDock(); ShowAppsManager(); }));
        ShowPage("group-editor", group == null ? "Nuevo grupo" : "Editar grupo", "NOMBRE \u00b7 COLOR \u00b7 APLICACIONES", body);
    }
}
