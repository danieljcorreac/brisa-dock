using System.Runtime.InteropServices;
namespace Brisa.WinUI;
// Retain the frame styles DWM needs for anti-aliased corners, while making
// the entire window client area so WinUI owns every visible pixel.
internal static class WindowSurface
{
    delegate nint SubclassProc(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nint data);
    static readonly SubclassProc procedure = Callback;
    static readonly HashSet<nint> attached = [];
    [DllImport("comctl32.dll")] static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nint data);
    [DllImport("comctl32.dll")] static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] static extern nint DefSubclassProc(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    public static void Configure(nint hwnd)
    {
        if (!attached.Contains(hwnd))
        {
            if (!SetWindowSubclass(hwnd, procedure, 0xB52, 0)) return;
            attached.Add(hwnd);
        }
        long style = GetWindowLongPtr(hwnd, -16).ToInt64();
        SetWindowLongPtr(hwnd, -16, (nint)(style | 0x00C40000)); // WS_CAPTION | WS_THICKFRAME
    }
    static nint Callback(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nint data)
    {
        if (message == 0x83 && wParam != 0) return 0; // WM_NCCALCSIZE: no caption or border inset.
        if (message == 0x85) return 0; // WM_NCPAINT: no second frame around the compositor surface.
        if (message == 0x86) return 1; // WM_NCACTIVATE: prevent an inactive white frame.
        if (message == 0x14) return 1; // WM_ERASEBKGND: the compositor paints the surface.
        if (message == 0x84) return 1; // WM_NCHITTEST: frameless dock has client interaction only.
        if (message == 0x82) { attached.Remove(hwnd); RemoveWindowSubclass(hwnd, procedure, 0xB52); }
        return DefSubclassProc(hwnd, message, wParam, lParam);
    }
}
