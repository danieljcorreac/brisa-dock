using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace Brisa.WinUI;

internal sealed record DockMonitor(string Id, string Name, bool Primary, RectInt32 Bounds, RectInt32 WorkArea)
{
    public string Label => $"{Name} · {Bounds.Width} × {Bounds.Height}" + (Primary ? " · Principal" : " · Secundario");
}

internal static class DockMonitors
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; public readonly RectInt32 Value => new(Left, Top, Right - Left, Bottom - Top); }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct Info
    {
        public uint Size; public Rect Monitor, Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct Device
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    delegate bool MonitorProc(nint monitor, nint dc, nint rect, nint data);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(nint dc, nint rect, MonitorProc callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(nint monitor, ref Info info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplayDevices(string device, uint index, ref Device info, uint flags);

    [StructLayout(LayoutKind.Sequential)] struct ScreenPoint { public int X, Y; }
    [DllImport("user32.dll")] static extern nint MonitorFromPoint(ScreenPoint point, uint flags);
    public static RectInt32 WorkAreaAt(int x, int y)
    {
        var info = new Info { Size = (uint)Marshal.SizeOf<Info>() };
        if (GetMonitorInfo(MonitorFromPoint(new ScreenPoint { X = x, Y = y }, 2), ref info)) return info.Work.Value;
        throw new InvalidOperationException("Could not resolve the menu anchor's monitor.");
    }

    public static IReadOnlyList<DockMonitor> Read()
    {
        var result = new List<DockMonitor>();
        EnumDisplayMonitors(0, 0, (handle, _, _, _) =>
        {
            var info = new Info { Size = (uint)Marshal.SizeOf<Info>() };
            if (!GetMonitorInfo(handle, ref info) || info.Work.Value.Width <= 0 || info.Work.Value.Height <= 0) return true;
            var device = new Device { Size = (uint)Marshal.SizeOf<Device>() };
            bool found = EnumDisplayDevices(info.Device, 0, ref device, 1);
            string id = found && !string.IsNullOrWhiteSpace(device.Id) ? device.Id : info.Device;
            string name = found && !string.IsNullOrWhiteSpace(device.Description) ? device.Description : info.Device;
            result.Add(new(id, name, (info.Flags & 1) != 0, info.Monitor.Value, info.Work.Value));
            return true;
        }, 0);
        return result.OrderByDescending(m => m.Primary).ThenBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).ToArray();
    }
}

public sealed partial class MainWindow
{
    DockMonitor? selectedMonitor;
    string monitorTopology = "";
    DateTimeOffset lastMonitorCheck;
    static string Topology(IReadOnlyList<DockMonitor> monitors) => string.Join("|", monitors.Select(m => $"{m.Id}:{m.Primary}:{m.Bounds.X},{m.Bounds.Y},{m.Bounds.Width},{m.Bounds.Height}:{m.WorkArea.X},{m.WorkArea.Y},{m.WorkArea.Width},{m.WorkArea.Height}"));
    void CheckMonitorTopology()
    {
        if (DateTimeOffset.Now - lastMonitorCheck < TimeSpan.FromSeconds(8)) return;
        lastMonitorCheck = DateTimeOffset.Now;
        string signature = Topology(DockMonitors.Read());
        if (signature == monitorTopology) return;
        monitorTopology = signature; displayDebounce.Stop(); displayDebounce.Start();
    }
    RectInt32 DockWorkArea => reservation?.IncludeOwnSpace(selectedMonitor?.WorkArea ?? Native.WorkArea(this)) ?? selectedMonitor?.WorkArea ?? Native.WorkArea(this);
    RectInt32 DockBounds => selectedMonitor?.Bounds ?? Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).OuterBounds;

    void PrepareMonitor()
    {
        var connected = DockMonitors.Read();
        monitorTopology = Topology(connected);
        string? id = MonitorSelection.Resolve(connected.Select(m => new MonitorChoice(m.Id, m.Primary)).ToArray(), settings.MonitorId);
        var target = connected.FirstOrDefault(m => m.Id == id);
        if (target == null) return;
        bool changed = selectedMonitor?.Id != target.Id || selectedMonitor.Bounds != target.Bounds;
        selectedMonitor = target;
        if (changed)
        {
            activeAnchor = null;
            // Move first so GetDpiForWindow and XAML use the destination's scaling.
            int width = Math.Min(AppWindow.Size.Width, target.WorkArea.Width - 24);
            int height = Math.Min(AppWindow.Size.Height, target.WorkArea.Height - 24);
            AppWindow.MoveAndResize(new(target.WorkArea.X + (target.WorkArea.Width - width) / 2, target.WorkArea.Y + (target.WorkArea.Height - height) / 2, Math.Max(1, width), Math.Max(1, height)));
            dockBehavior?.Place(AppWindow.Position);
            dockBehavior?.Reveal();
        }
    }

    void AddMonitorSettings(StackPanel body)
    {
        var monitors = DockMonitors.Read();
        body.Children.Add(Ui.Text("PANTALLA DEL DOCK", 11, Ui.MutedColor, true));
        void Select(string? id) { settings.MonitorId = id; Save(); BuildDock(); ShowDockSettings(); }
        body.Children.Add(ChoiceUi.Card("Seguir a la principal", "Se adapta si cambias tu pantalla principal", Ui.Glyph("\uE7F4", 26, Ui.Light ? "#376F66" : "#B9E9D6"), settings.MonitorId == null, () => Select(null)));
        body.Children.Add(ChoiceUi.Group(Math.Min(2, Math.Max(1, monitors.Count)), monitors.Select((m, i) => ChoiceUi.Card($"Pantalla {i + 1}", $"{m.Bounds.Width} × {m.Bounds.Height} · {(m.Primary ? "Principal" : "Secundaria")}", ChoiceUi.Screen(number: (i + 1).ToString(), aspect: m.Bounds.Width / (double)Math.Max(1, m.Bounds.Height)), string.Equals(m.Id, settings.MonitorId, StringComparison.OrdinalIgnoreCase), () => Select(m.Id)))));
        if (settings.MonitorId != null && monitors.All(m => !string.Equals(m.Id, settings.MonitorId, StringComparison.OrdinalIgnoreCase)))
            body.Children.Add(Ui.Card(Ui.Text("Tu pantalla preferida está desconectada. Brisa usa la principal hasta que vuelva a estar disponible.", 12, Ui.MutedColor)));
        body.Children.Add(Ui.Text("Elige tu pantalla secundaria para dejar allí la barra y sus tarjetas. Si la desconectas, Brisa usa temporalmente la principal y recuerda tu elección.", 12, Ui.MutedColor));
        body.Children.Add(Ui.Button("Actualizar pantallas", "Volver a detectar monitores", () => { BuildDock(); ShowDockSettings(); }));
    }
}
