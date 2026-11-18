using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    void ShowPreferencesHome()
    {
        var body = Ui.Stack(10);
        body.Children.Add(Ui.Text("Un espacio a tu medida", 20, bold: true));
        body.Children.Add(Ui.Text("Elige cómo se ve, dónde aparece y qué tienes a mano.", 12, Ui.MutedColor));
        body.Children.Add(Ui.Navigation("Apariencia", "Color, material y transparencia", "\uE790", ShowAppearance));
        body.Children.Add(Ui.Navigation("Dock y pantalla", "Tamaño, posición, monitor y comportamiento", "\uE7F4", ShowLayoutPreferences));
        body.Children.Add(Ui.Navigation("Widgets", "Tu colección y el orden de la barra", "\uE8A9", ShowWidgetPreferences));
        body.Children.Add(Ui.Navigation("Aplicaciones y carpetas", "Accesos, grupos y ventanas abiertas", "\uE8B7", ShowAppsManager));
        body.Children.Add(Ui.Navigation("Windows", "Bandeja, inicio y accesos rápidos", "\uE770", ShowWindowsSettings));
        body.Children.Add(Ui.Navigation("Instalación y actualizaciones", "Tu versión y la copia anterior", "\uE895", ShowInstallation));
        body.Children.Add(Ui.Navigation("Copias y recuperación", "Guardar, revisar y restaurar tus datos", "\uE74E", ShowBackupSettings));
        body.Children.Add(Ui.Navigation("Accesibilidad", "Movimiento y visibilidad de la barra", "\uE776", ShowAccessibilityPreferences));
        body.Children.Add(Ui.Text("Brisa " + typeof(MainWindow).Assembly.GetName().Version?.ToString(3) + " · Tus datos se guardan en este equipo", 11, Ui.MutedColor));
        body.Children.Add(Ui.Button("Salir de Brisa", "Salir de Brisa", Close));
        ShowPage("settings", "Preferencias", "BRISA", body);
    }
    void ShowLayoutPreferences()
    {
        var body = Ui.Stack(14); AddPlacementOptions(body, ShowLayoutPreferences);
        body.Children.Add(Ui.Navigation("Pantalla y comportamiento", "Monitor, ancho, ocultación e iconos", "\uE7F4", ShowDockSettings));
        body.Children.Add(Ui.Button("Volver a preferencias", "Volver a preferencias", ShowSettings));
        ShowPage("layout-preferences", "Tu dock", "DISPOSICIÓN", body);
    }
    void ShowWidgetPreferences()
    {
        var body = Ui.Stack(12);
        body.Children.Add(Ui.Navigation("Explorar widgets", "Vistas de ejemplo de tu colección", "\uE710", ShowGallery));
        body.Children.Add(Ui.Navigation("Organizar mi dock", "Mover widgets sobre la barra o desde la lista", "\uE70F", ShowOrder));
        body.Children.Add(Ui.Button("Volver a preferencias", "Volver a preferencias", ShowSettings));
        ShowPage("widget-preferences", "Tus widgets", "COLECCIÓN", body);
    }
    void ShowAccessibilityPreferences()
    {
        var body = Ui.Stack(16);
        var follow = new ToggleSwitch { Header = "Respetar accesibilidad de Windows", IsOn = settings.FollowSystemAccessibility };
        follow.Toggled += (_, _) => { settings.FollowSystemAccessibility = follow.IsOn; ApplyPreferences(); SetSurface(); BuildDock(); Save(); panel?.RefreshMaterial(); }; body.Children.Add(follow);
        body.Children.Add(Ui.Text("Aplica el contraste de Windows y desactiva las animaciones si las tienes desactivadas en el sistema. No cambia los ajustes de Windows.", 12, Ui.MutedColor));
        var motion = new ToggleSwitch { Header = "Reducir movimiento", IsOn = settings.ReducedMotion };
        motion.Toggled += (_, _) => { settings.ReducedMotion = motion.IsOn; ApplyPreferences(); Save(); }; body.Children.Add(motion);
        body.Children.Add(Ui.Text("Reduce los desplazamientos y aplica los cambios de valor sin animación.", 12, Ui.MutedColor));
        var top = new ToggleSwitch { Header = "Siempre visible", IsOn = settings.AlwaysOnTop };
        top.Toggled += (_, _) => { settings.AlwaysOnTop = top.IsOn; Native.Style(this, settings.AlwaysOnTop); if (panel != null) Native.Style(panel, settings.AlwaysOnTop); Save(); }; body.Children.Add(top);
        body.Children.Add(Ui.Text("Tab y Mayús+Tab recorren los controles. Escape cierra el panel. Mayús+F10 abre las acciones del elemento. En edición, las flechas mueven el widget seleccionado. La barra permanece visible mientras la usas con el teclado.", 13));
        body.Children.Add(Ui.Text("Atajos: Ctrl+, preferencias · Ctrl+Mayús+O organizar · Ctrl+Mayús+T tareas · Ctrl+Mayús+C calendario · Ctrl+Mayús+B copias.", 12, Ui.MutedColor));
        body.Children.Add(Ui.Button("Volver a preferencias", "Volver a preferencias", ShowSettings));
        ShowPage("accessibility", "Accesibilidad", "A TU RITMO", body);
    }
}
