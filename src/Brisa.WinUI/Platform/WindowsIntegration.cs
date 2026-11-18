using Brisa.Core;
using System.Runtime.InteropServices;
namespace Brisa.WinUI;
internal sealed class WindowsIntegration : IDisposable
{
    const uint CallbackMessage = 0x8000 + 42;
    const int RevealHotkey = 0xB501, TasksHotkey = 0xB502;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct NotifyData
    {
        public uint Size; public nint Window; public uint Id, Flags, Callback; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct IconIdentifier { public uint Size; public nint Window; public uint Id; public Guid Guid; }
    [DllImport("shell32.dll")] static extern int Shell_NotifyIconGetRect(ref IconIdentifier identifier, out Rect rectangle);
    delegate nint SubclassProc(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nint data);
    [DllImport("comctl32.dll")] static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nint data);
    [DllImport("comctl32.dll")] static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] static extern nint DefSubclassProc(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(uint message, ref NotifyData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint LoadImage(nint instance, string name, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] static extern nint LoadIcon(nint instance, nint resource);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenu(nint menu, uint flags, nuint id, string text);
    [DllImport("user32.dll")] static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint owner, nint rect);
    [DllImport("user32.dll")] static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(nint hwnd);
    readonly nint hwnd, icon;
    readonly bool ownedIcon;
    readonly SubclassProc procedure;
    readonly Action reveal, settings, tasks, exit, layout, resume, organize, gallery;
    readonly Action? checkpoint, accessibility;
    readonly uint taskbarCreated, restoreMessage;
    bool trayWanted, hotkeysWanted, disposed, trayVersion4;
    readonly Microsoft.UI.Xaml.DispatcherTimer trayRetry = new() { Interval = TimeSpan.FromSeconds(5) };
    public bool TrayActive { get; private set; }
    public bool HotkeysActive { get; private set; }
    public DateTimeOffset? LastNotificationAccepted { get; private set; }
    public DateTimeOffset? LastNotificationShown { get; private set; }
    public event Action<bool>? TrayStateChanged;
    public event Action<PreviewRect>? QuickMenuRequested;
    public WindowsIntegration(nint hwnd, Action reveal, Action settings, Action tasks, Action exit, Action layout, Action resume, Action organize, Action gallery, Action? checkpoint = null, Action? accessibility = null)
    {
        this.hwnd = hwnd; this.reveal = reveal; this.settings = settings; this.tasks = tasks; this.exit = exit; this.layout = layout; this.resume = resume;
        this.organize = organize; this.gallery = gallery; this.checkpoint = checkpoint; this.accessibility = accessibility;
        icon = LoadImage(0, System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"), 1, 32, 32, 0x10); ownedIcon = icon != 0;
        if (icon == 0) icon = LoadIcon(0, 32512);
        taskbarCreated = RegisterWindowMessage("TaskbarCreated"); restoreMessage = RegisterWindowMessage("BrisaDock.Reveal.Local.v1");
        procedure = Callback;
        if (!SetWindowSubclass(hwnd, procedure, 0xB51, 0)) { if (ownedIcon) DestroyIcon(icon); throw new InvalidOperationException("No se pudo conectar Brisa con los mensajes de Windows."); }
        trayRetry.Tick += (_, _) => { if (disposed) { trayRetry.Stop(); return; } if (trayWanted && !TrayActive) SetTray(true); if (hotkeysWanted && !HotkeysActive) SetHotkeys(true); RetryIfNeeded(); };
    }
    NotifyData Data() => new() { Size = (uint)Marshal.SizeOf<NotifyData>(), Window = hwnd, Id = 1, Flags = 1 | 2 | 4 | 0x80, Callback = CallbackMessage, Icon = icon, Tip = "Brisa Dock · Tu espacio", Info = "", Title = "" };
    public bool SetTray(bool enabled)
    {
        trayWanted = enabled; var data = Data();
        if (enabled)
        {
            if (TrayActive && !Shell_NotifyIcon(1, ref data)) TrayActive = false;
            if (!TrayActive)
            {
                TrayActive = Shell_NotifyIcon(0, ref data);
                if (TrayActive) { data.Timeout = 4; trayVersion4 = Shell_NotifyIcon(4, ref data); }
            }
        }
        else { if (TrayActive) Shell_NotifyIcon(2, ref data); TrayActive = false; trayVersion4 = false; }
        TrayStateChanged?.Invoke(TrayActive);
        RetryIfNeeded();
        return !enabled || TrayActive;
    }
    void RetryIfNeeded() { if (!disposed && (trayWanted && !TrayActive || hotkeysWanted && !HotkeysActive)) trayRetry.Start(); else trayRetry.Stop(); }
    public bool SetHotkeys(bool enabled)
    {
        hotkeysWanted = enabled;
        UnregisterHotKey(hwnd, RevealHotkey); UnregisterHotKey(hwnd, TasksHotkey); HotkeysActive = false;
        if (!enabled) { RetryIfNeeded(); return true; }
        bool revealRegistered = RegisterHotKey(hwnd, RevealHotkey, 0x4003, 0x42);
        bool tasksRegistered = RegisterHotKey(hwnd, TasksHotkey, 0x4003, 0x54);
        if (!revealRegistered || !tasksRegistered) { UnregisterHotKey(hwnd, RevealHotkey); UnregisterHotKey(hwnd, TasksHotkey); RetryIfNeeded(); return false; }
        HotkeysActive = true; RetryIfNeeded(); return true;
    }
    public bool Notify(string title, string text, bool silent = false)
    {
        if (!TrayActive) return false;
        var data = Data(); data.Flags = 0x10; data.Title = title[..Math.Min(title.Length, 63)]; data.Info = text[..Math.Min(text.Length, 255)]; data.InfoFlags = silent ? 0x11u : 1u;
        bool accepted = Shell_NotifyIcon(1, ref data); if (accepted) LastNotificationAccepted = DateTimeOffset.Now; return accepted;
    }
    public PreviewRect QuickMenuAnchor(bool fromTray)
    {
        if (fromTray)
        {
            var identifier = new IconIdentifier { Size = (uint)Marshal.SizeOf<IconIdentifier>(), Window = hwnd, Id = 1 };
            if (Shell_NotifyIconGetRect(ref identifier, out var rect) == 0 && rect.Right > rect.Left && rect.Bottom > rect.Top)
                return new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
        return GetCursorPos(out var cursor) ? new(cursor.X, cursor.Y, 1, 1) : new(0, 0, 1, 1);
    }
    public void ShowQuickMenu(bool fromTray = false) { if (QuickMenuRequested != null) QuickMenuRequested(QuickMenuAnchor(fromTray)); else Menu(); }
    void Menu()
    {
        if (!GetCursorPos(out var position)) return;
        var menu = CreatePopupMenu(); if (menu == 0) return;
        try
        {
            AppendMenu(menu, 0, 1, "Mostrar Brisa");
            AppendMenu(menu, 0, 5, "Organizar widgets");
            AppendMenu(menu, 0, 6, "Añadir widgets");
            AppendMenu(menu, 0x800, 0, "");
            AppendMenu(menu, 0, 3, "Tareas"); AppendMenu(menu, 0, 2, "Ajustes");
            AppendMenu(menu, 0x800, 0, ""); AppendMenu(menu, 0, 4, "Salir de Brisa");
            SetForegroundWindow(hwnd); uint selected = TrackPopupMenu(menu, 0x100 | 0x2, position.X, position.Y, 0, hwnd, 0); PostMessage(hwnd, 0, 0, 0);
            if (selected == 1) reveal(); else if (selected == 2) settings(); else if (selected == 3) tasks(); else if (selected == 4) exit(); else if (selected == 5) organize(); else if (selected == 6) gallery();
        }
        finally { DestroyMenu(menu); }
    }
    nint Callback(nint window, uint message, nint wParam, nint lParam, nuint id, nint data)
    {
        if (disposed) return DefSubclassProc(window, message, wParam, lParam);
        try
        {
            if (message == CallbackMessage)
            {
                uint action = (uint)(lParam.ToInt64() & 0xFFFF);
                if (action == 0x402) LastNotificationShown = DateTimeOffset.Now;
                // v4 sends NIN_SELECT/NIN_KEYSELECT; legacy shells send mouse messages.
                if (trayVersion4 ? action is 0x400 or 0x401 : action is 0x202 or 0x203) reveal();
                else if (trayVersion4 ? action == 0x7B : action is 0x205 or 0x7B) ShowQuickMenu(fromTray: true);
                return 0;
            }
            if (message == taskbarCreated) { TrayActive = false; if (trayWanted) SetTray(true); }
            if (message == restoreMessage) { reveal(); return 0; }
            if (message == 0x312) { if ((int)wParam == RevealHotkey) reveal(); else if ((int)wParam == TasksHotkey) tasks(); return 0; }
            if (message == 0x11 || message == 0x16 && wParam != 0 || message == 0x218 && wParam.ToInt64() == 4) checkpoint?.Invoke();
            if (message is 0x7E or 0x2E0 || (message == 0x1A && wParam.ToInt64() == 0x2F)) layout();
            if (message == 0x218 && wParam.ToInt64() is 7 or 18) resume();
            if (message is 0x1A or 0x15 or 0x31A) accessibility?.Invoke();
        }
        catch { /* Never allow a managed exception to cross a native window procedure. */ }
        return DefSubclassProc(window, message, wParam, lParam);
    }
    public static void RevealExisting() => PostMessage(0xFFFF, RegisterWindowMessage("BrisaDock.Reveal.Local.v1"), 0, 0);
    internal void ProbeLifecycleForChecks()
    {
        // Local messages to this diagnostic HWND only; never suspend Windows or restart Explorer.
        SendMessage(hwnd, 0x218, 4, 0); SendMessage(hwnd, 0x11, 0, 0);
        SendMessage(hwnd, 0x218, 7, 0); SendMessage(hwnd, 0x218, 18, 0);
    }
    internal void ProbeDisplayChangesForChecks()
    {
        SendMessage(hwnd, 0x7E, 0, 0); // Display topology changed, diagnostic HWND only.
        SendMessage(hwnd, 0x1A, 0x2F, 0); // Work area changed.
    }
    internal bool ProbeTrayKeyboardForChecks()
    {
        if (!TrayActive || !trayVersion4) return false;
        SendMessage(hwnd, CallbackMessage, 0, 0x401); // NIN_KEYSELECT.
        SendMessage(hwnd, CallbackMessage, 0, 0x7B); // WM_CONTEXTMENU.
        return true;
    }
    internal bool ProbeTrayRecoveryForChecks()
    {
        if (!SetTray(true)) return false;
        var data = Data(); if (!Shell_NotifyIcon(2, ref data)) return false;
        // Recover a stale cached state as well as an Explorer restart, on this probe only.
        bool staleRecovered = SetTray(true); if (!staleRecovered || !Shell_NotifyIcon(2, ref data)) return false;
        SendMessage(hwnd, taskbarCreated, 0, 0); return TrayActive;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; SetHotkeys(false); SetTray(false); RemoveWindowSubclass(hwnd, procedure, 0xB51); if (ownedIcon) DestroyIcon(icon);
    }
}
