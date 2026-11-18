using Brisa.Core;
namespace Brisa.WinUI;

public sealed partial class MainWindow
{
    Action? PageAction(string page) => page switch
    {
        "settings" => ShowSettings, "layout-preferences" => ShowLayoutPreferences,
        "widget-preferences" => ShowWidgetPreferences, "apps-manager" => ShowAppsManager,
        "installation" => ShowInstallation, "windows-settings" => ShowWindowsSettings,
        "backups" => ShowBackupSettings, "tasks" => ShowTasks, "calendar" => ShowCalendar,
        "claude" => () => ShowUsage("claude"), "chatgpt" => () => ShowUsage("chatgpt"),
        _ => null
    };
    static string PageLabel(string page) => page switch
    {
        "settings" => "Preferencias", "layout-preferences" => "Dock y pantalla",
        "widget-preferences" => "Widgets", "apps-manager" => "Aplicaciones y carpetas",
        "installation" => "Instalación y actualizaciones", "windows-settings" => "Brisa y Windows",
        "backups" => "Copias y recuperación", "tasks" => "Tareas", "calendar" => "Calendario",
        "claude" => "Claude", "chatgpt" => "ChatGPT", _ => "Preferencias"
    };
    Action? messageReturn;
    void ConfigurePageBack(string id)
    {
        string? parent = PanelNavigation.Parent(id);
        if (id == "quota-history") parent = quotaHistoryProvider;
        panel?.SetBackNavigation(id == "message" ? messageReturn : parent == null ? null : PageAction(parent),
            id == "message" ? "Volver" : "Volver a " + PageLabel(parent ?? "settings"));
    }
}
