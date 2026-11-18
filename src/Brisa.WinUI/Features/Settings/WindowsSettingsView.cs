using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    WindowsIntegration? windowsIntegration;
    readonly DispatcherTimer displayDebounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    void SetupWindowsIntegration()
    {
        displayDebounce.Tick += (_, _) =>
        {
            displayDebounce.Stop();
            if (lifetime.IsCancellationRequested) return;
            bool monitorSettingsOpen = panel?.AppWindow.IsVisible == true && panel.PageId == "dock-settings";
            BuildDock();
            if (monitorSettingsOpen) ShowDockSettings();
            else panel?.Reflow(this, DockWorkArea, settings.Scale);
        };
        try
        {
            // Let the Shell callback return before activating XAML or closing its HWND.
            Action Deferred(Action action) => () => DispatcherQueue.TryEnqueue(() => { if (!closing) action(); });
            windowsIntegration = new(WinRT.Interop.WindowNative.GetWindowHandle(this), Deferred(RevealFromWindows), Deferred(() => { RevealFromWindows(); ShowSettings(); }), Deferred(() => { RevealFromWindows(); ShowTasks(); }), Deferred(Close),
                () => { displayDebounce.Stop(); displayDebounce.Start(); },
                Deferred(RecoverWindowsSession),
                Deferred(() => { RevealFromWindows(); ShowOrder(); }), Deferred(() => { RevealFromWindows(); ShowGallery(); }), SaveSessionState, RefreshSystemAccessibility);
            windowsIntegration.TrayStateChanged += SyncTaskbarVisibility;
            windowsIntegration.QuickMenuRequested += anchor => DispatcherQueue.TryEnqueue(() => { if (!closing) ShowTrayQuickMenu(anchor); });
            windowsIntegration.SetTray(settings.TrayIcon);
            windowsIntegration.SetHotkeys(settings.GlobalShortcuts);
        }
        catch { windowsIntegration?.Dispose(); windowsIntegration = null; AppWindow.IsShownInSwitchers = true; }
    }
    DateTimeOffset lastWindowsRecovery;
    void RecoverWindowsSession()
    {
        if (closing || DateTimeOffset.Now - lastWindowsRecovery < TimeSpan.FromSeconds(1)) return;
        lastWindowsRecovery = DateTimeOffset.Now;
        lastAiPoll = DateTimeOffset.MinValue; lastWeatherPoll = DateTimeOffset.MinValue; lastMediaPoll = DateTimeOffset.MinValue;
        windowsIntegration?.SetTray(settings.TrayIcon); windowsIntegration?.SetHotkeys(settings.GlobalShortcuts);
        reservation?.Invalidate(); dockBehavior?.Reveal(); displayDebounce.Stop(); displayDebounce.Start();
    }
    void SyncTaskbarVisibility(bool trayActive)
    {
        if (closing) return;
        AppWindow.IsShownInSwitchers = !trayActive;
        Native.Style(this, settings.AlwaysOnTop);
    }
    TrayMenuWindow? trayQuickMenu;
    void ShowTrayQuickMenu(Brisa.Core.PreviewRect anchor)
    {
        trayQuickMenu?.Dismiss();
        activeAnchor = null;
        void Open(Action action) { RevealFromWindows(); action(); }
        var menu = new TrayMenuWindow([
            ("Mostrar Brisa", "\uE737", RevealFromWindows),
            ("Organizar widgets", "\uE70F", () => Open(ShowOrder)),
            ("Añadir widgets", "\uE710", () => Open(ShowGallery)),
            ("", "", null),
            ("Tareas", "\uE73E", () => Open(ShowTasks)),
            ("Ajustes", "\uE713", () => Open(ShowSettings)),
            ("Calendario", "\uE787", () => Open(ShowCalendar)),
            ("", "", null),
            ("Salir de Brisa", "\uE8BB", Close)
        ]);
        trayQuickMenu = menu;
        menu.Closed += (_, _) => { if (trayQuickMenu == menu) trayQuickMenu = null; };
        menu.ShowAnchored(anchor, settings.Scale);
    }
    void RevealFromWindows() { dockBehavior?.Reveal(); AppWindow.Show(); Native.Style(this, settings.AlwaysOnTop); Activate(); }
    void ShowWindowsSettings()
    {
        var body = Ui.Stack(14);
        body.Children.Add(Ui.Text("Un acceso a Brisa desde Windows, incluso cuando la barra est\u00e1 oculta.", 13, Ui.MutedColor));
        var reserve = new ToggleSwitch { Header = "Reservar espacio para Brisa", IsOn = settings.ReserveSpace };
        var reserveStatus = Ui.Text("", 12, Ui.MutedColor);
        void ReservationStatus() => reserveStatus.Text = settings.AutoHide ? "La reserva se pausa mientras usas ocultación automática." : reservation?.Active == true ? "Las ventanas maximizadas respetan el espacio del dock en esta pantalla." : settings.ReserveSpace ? "Windows no ha podido reservar el espacio. Puedes volver a intentarlo desde Recuperar posición y bandeja." : "Si lo activas, las ventanas maximizadas dejarán libre una franja para el dock. Se libera al desactivarlo o cerrar Brisa.";
        reserve.Toggled += (_, _) => { settings.ReserveSpace = reserve.IsOn; BuildDock(); Save(); ReservationStatus(); };
        body.Children.Add(reserve); ReservationStatus(); body.Children.Add(reserveStatus);
        var trayStatus = Ui.Text("", 11, Ui.MutedColor);
        void UpdateTrayStatus() => trayStatus.Text = windowsIntegration?.TrayActive == true
            ? "Brisa está en el área de notificación, sin ocupar la barra de tareas. Clic para mostrarla; clic derecho para organizar widgets, abrir el catálogo, tareas, ajustes o salir. Puede estar en los iconos ocultos de Windows."
            : "Sin bandeja, Brisa vuelve a aparecer con su icono en la barra de tareas.";
        var tray = new ToggleSwitch { Header = "Icono en la bandeja", IsOn = settings.TrayIcon }; bool changing = false;
        tray.Toggled += (_, _) =>
        {
            if (changing) return;
            bool enabled = tray.IsOn;
            if (windowsIntegration == null || !windowsIntegration.SetTray(enabled)) { changing = true; tray.IsOn = false; changing = false; settings.TrayIcon = false; windowsIntegration?.SetTray(false); ShowMessage("Bandeja", "Windows no ha permitido crear el icono. Puedes seguir usando la barra."); }
            else settings.TrayIcon = enabled;
            UpdateTrayStatus(); Save();
        }; body.Children.Add(tray);
        UpdateTrayStatus(); body.Children.Add(trayStatus);
        var startup = new ToggleSwitch { Header = "Abrir Brisa al iniciar sesi\u00f3n" }; bool startupChanging = false;
        try { startup.IsOn = StartupRegistration.Enabled; }
        catch { startup.IsEnabled = false; }
        startup.Toggled += (_, _) =>
        {
            if (startupChanging) return;
            try { StartupRegistration.Set(startup.IsOn); }
            catch (Exception e) when (e is UnauthorizedAccessException or System.IO.IOException or ArgumentException or System.Security.SecurityException)
            {
                startupChanging = true; startup.IsOn = false; startupChanging = false; ShowMessage("Inicio de Windows", "No se pudo cambiar el inicio autom\u00e1tico: " + e.Message);
            }
        }; body.Children.Add(startup);
        body.Children.Add(Ui.Text("Se registra solo para tu usuario y se usa la ubicaci\u00f3n actual de Brisa. Windows puede desactivarlo desde Aplicaciones de inicio.", 11, Ui.MutedColor));
        body.Children.Add(Ui.Button("Aplicaciones de inicio de Windows", "Abrir aplicaciones de inicio", () => Launch("ms-settings:startupapps")));
        var shortcuts = new ToggleSwitch { Header = "Atajos globales", IsOn = settings.GlobalShortcuts }; bool shortcutChanging = false;
        shortcuts.Toggled += (_, _) =>
        {
            if (shortcutChanging) return;
            bool enabled = shortcuts.IsOn;
            if (windowsIntegration == null || !windowsIntegration.SetHotkeys(enabled))
            {
                shortcutChanging = true; shortcuts.IsOn = false; shortcutChanging = false; settings.GlobalShortcuts = false;
                ShowMessage("Atajos", "No se pudieron reservar los atajos. Es posible que otra aplicaci\u00f3n ya los utilice.");
            }
            else settings.GlobalShortcuts = enabled;
            Save();
        }; body.Children.Add(shortcuts);
        var shortcutStatus = Ui.Text("", 11, Ui.MutedColor); body.Children.Add(shortcutStatus);
        body.Children.Add(Ui.Text("Ctrl + Alt + B muestra la barra. Ctrl + Alt + T abre tus tareas. Se liberan al desactivarlos o cerrar Brisa.", 12, Ui.MutedColor));
        body.Children.Add(Ui.Text("Dentro de Brisa: Ctrl + , abre Ajustes; Ctrl + Mayús + T, Tareas; Ctrl + Mayús + C, Calendario; Ctrl + Mayús + B, Copias; Ctrl + Mayús + O, Organizar. Escape cierra el panel y devuelve el foco al dock. Estos atajos no se reservan para otras aplicaciones.", 12, Ui.MutedColor));
        body.Children.Add(Ui.Button("Estado y consumo de Brisa", "Consultar medidas de rendimiento y guardar un diagnóstico", ShowHealth));
        body.Children.Add(Ui.Button("Mostrar la barra", "Mostrar la barra de Brisa", RevealFromWindows));
        body.Children.Add(Ui.Button("Recuperar posición y bandeja", "Recolocar Brisa y volver a registrar su icono de bandeja", () =>
        {
            RevealFromWindows(); windowsIntegration?.SetTray(settings.TrayIcon);
            windowsIntegration?.SetHotkeys(settings.GlobalShortcuts); reservation?.Invalidate();
            BuildDock(); Save(); ShowWindowsSettings();
        }));
        body.Children.Add(Ui.Text("Foco y temporizador guardan su estado cada treinta segundos mientras funcionan y al iniciar, pausar, finalizar o recibir el aviso de suspensión o cierre de Windows. Al volver a abrir Brisa se recuperan en pausa.", 11, Ui.MutedColor));
        var notificationStatus = Ui.Text("Puedes comprobar la entrega de avisos con una prueba silenciosa.", 11, Ui.MutedColor);
        DateTimeOffset? notificationTest = null;
        body.Children.Add(Ui.Button("Probar notificación", "Enviar una notificación silenciosa de prueba", () =>
        {
            notificationTest = DateTimeOffset.Now;
            bool accepted = windowsIntegration?.Notify("Brisa · Prueba de avisos", "La notificación de prueba está funcionando.", true) == true;
            notificationStatus.Text = accepted ? "Windows aceptó la notificación. Esperando confirmación de presentación…" : "Windows no aceptó el aviso. Comprueba que la bandeja esté activa.";
        })); body.Children.Add(notificationStatus);
        var quiet = new ToggleSwitch { Header = "Horas silenciosas", IsOn = settings.QuietHours }; quiet.Toggled += (_, _) => { settings.QuietHours = quiet.IsOn; Save(); }; body.Children.Add(quiet);
        var quietStart = new NumberBox { Header = "Silencio desde (hora 0-23)", Value = settings.QuietStart, Minimum = 0, Maximum = 23, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var quietEnd = new NumberBox { Header = "Silencio hasta (hora 0-23)", Value = settings.QuietEnd, Minimum = 0, Maximum = 23, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        quietStart.ValueChanged += (_, _) => { if (double.IsFinite(quietStart.Value)) { settings.QuietStart = (int)quietStart.Value; Save(); } }; quietEnd.ValueChanged += (_, _) => { if (double.IsFinite(quietEnd.Value)) { settings.QuietEnd = (int)quietEnd.Value; Save(); } }; body.Children.Add(quietStart); body.Children.Add(quietEnd);
        body.Children.Add(Ui.Text("Las horas silenciosas aplazan los avisos de tareas y silencian los temporizadores de Brisa. No cambian el modo No molestar de Windows.", 11, Ui.MutedColor));
        ShowPage("windows-settings", "Brisa y Windows", "BANDEJA \u00b7 INICIO \u00b7 ATAJOS", body);
        livePanelUpdate = () =>
        {
            ReservationStatus(); UpdateTrayStatus();
            shortcutStatus.Text = !settings.GlobalShortcuts ? "Atajos globales desactivados." : windowsIntegration?.HotkeysActive == true ? "Atajos globales disponibles." : "Windows no permite reservar los atajos por ahora. Brisa volverá a intentarlo sin cambiar tu preferencia.";
            if (notificationTest is not { } requested) return;
            if (windowsIntegration?.LastNotificationShown >= requested) notificationStatus.Text = "Windows confirmó que mostró el aviso de prueba.";
            else if (DateTimeOffset.Now - requested > TimeSpan.FromSeconds(15) && windowsIntegration?.LastNotificationAccepted >= requested) notificationStatus.Text = "Windows aceptó el aviso, pero no confirmó su presentación. Puede haberlo silenciado o aplazado.";
        };
    }
}
