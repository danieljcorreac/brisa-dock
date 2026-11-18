namespace Brisa.Core;

public static class PanelNavigation
{
    public static string? Parent(string page) => page switch
    {
        "appearance" or "layout-preferences" or "widget-preferences" or "apps-manager" or "windows-settings" or "installation" or "backups" or "accessibility" => "settings",
        "dock-settings" => "layout-preferences",
        "gallery" or "order" => "widget-preferences",
        "installed-apps" or "shortcut-editor" or "app-group" or "group-editor" or "app-windows" or "recent-documents" => "apps-manager",
        "update-preview" or "restore-version" => "installation",
        "health" => "windows-settings",
        "backup-preview" or "backup-recovery" => "backups",
        "task-editor" => "tasks",
        "event-editor" or "calendar-series" or "calendar-exceptions" or "event-archive" or "calendar-import" => "calendar",
        "claude-setup" or "claude-direct-consent" => "claude",
        _ when page.StartsWith("folder-stack-", StringComparison.Ordinal) => "apps-manager",
        _ => null
    };
}
