using Brisa.Core;
using System.Runtime.InteropServices;
using Windows.Graphics;
namespace Brisa.WinUI;

internal sealed class DesktopReservation : IDisposable
{
    const uint Notification = 0x8000 + 91;
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; public readonly RectInt32 Value => new(Left, Top, Right - Left, Bottom - Top); }
    [StructLayout(LayoutKind.Sequential)] struct Data { public uint Size; public nint Window; public uint Callback, Edge; public Rect Rect; public nint Param; }
    delegate nint Procedure(nint window, uint message, nint w, nint l, nuint id, nint data);
    [DllImport("shell32.dll")] static extern nuint SHAppBarMessage(uint message, ref Data data);
    [DllImport("comctl32.dll")] static extern bool SetWindowSubclass(nint window, Procedure callback, nuint id, nint data);
    [DllImport("comctl32.dll")] static extern bool RemoveWindowSubclass(nint window, Procedure callback, nuint id);
    [DllImport("comctl32.dll")] static extern nint DefSubclassProc(nint window, uint message, nint w, nint l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
    readonly nint window; readonly Procedure callback; readonly Action changed;
    readonly uint shellRestart = RegisterWindowMessage("TaskbarCreated");
    bool disposed, updating, dirty = true, top;
    RectInt32 monitor;
    int thickness;
    public bool Active { get; private set; }
    public RectInt32? Band { get; private set; }
    public DesktopReservation(nint window, Action changed)
    {
        this.window = window; this.changed = changed; callback = Message;
        if (!SetWindowSubclass(window, callback, 0xB592, 0)) throw new InvalidOperationException("No se pudo registrar el espacio del dock.");
    }
    Data Request() => new() { Size = (uint)Marshal.SizeOf<Data>(), Window = window, Callback = Notification, Edge = top ? 1u : 3u };
    public bool Update(bool enabled, RectInt32 bounds, bool atTop, int height)
    {
        if (disposed) return false;
        if (!enabled) { Release(); return true; }
        height = Math.Clamp(height, 1, Math.Max(1, bounds.Height / 3));
        if (Active && (monitor != bounds || top != atTop)) Release();
        if (Active && !dirty && height == thickness) return true;
        monitor = bounds; top = atTop; thickness = height;
        updating = true;
        try
        {
            var data = Request();
            if (!Active) { Active = SHAppBarMessage(0, ref data) != 0; if (!Active) return false; }
            data.Rect = new() { Left = bounds.X, Top = bounds.Y, Right = bounds.X + bounds.Width, Bottom = bounds.Y + bounds.Height };
            SHAppBarMessage(2, ref data);
            if (top) data.Rect.Bottom = data.Rect.Top + height; else data.Rect.Top = data.Rect.Bottom - height;
            if (Band != data.Rect.Value && SHAppBarMessage(3, ref data) == 0) { Release(); return false; }
            Band = data.Rect.Value; dirty = false; return true;
        }
        finally { updating = false; }
    }
    public RectInt32 IncludeOwnSpace(RectInt32 work)
    {
        if (!Active || Band is not { } band) return work;
        var result = DockReservation.IncludeOwnSpace(new(work.X, work.Y, work.Width, work.Height), new(band.X, band.Y, band.Width, band.Height), top);
        return new(result.X, result.Y, result.Width, result.Height);
    }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern nint SendMessage(nint window, uint message, nint w, nint l);
    internal void ProbeShellRestartForChecks() { Release(); SendMessage(window, shellRestart, 0, 0); }
    public void Invalidate() { dirty = true; }
    public void Release()
    {
        if (Active) { var data = Request(); SHAppBarMessage(1, ref data); }
        Active = false; Band = null; dirty = true;
    }
    nint Message(nint hwnd, uint message, nint w, nint l, nuint id, nint data)
    {
        try
        {
            if (message == 2) Release();
            else if (message == shellRestart) { Active = false; Band = null; dirty = true; changed(); }
            else if (message == Notification && (int)w == 1 && !updating) { dirty = true; changed(); }
            else if (Active && message is 6 or 0x47) { var request = Request(); if (message == 6) request.Param = (w.ToInt64() & 0xffff) == 0 ? 0 : 1; SHAppBarMessage(message == 6 ? 6u : 9u, ref request); }
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException) { }
        return DefSubclassProc(hwnd, message, w, l);
    }
    public void Dispose() { if (disposed) return; Release(); disposed = true; RemoveWindowSubclass(window, callback, 0xB592); }
}
