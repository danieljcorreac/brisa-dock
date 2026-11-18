using Brisa.Core;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
namespace Brisa.WinUI;
internal record DiskReading(string Name, long AvailableFreeSpace, long TotalSize);
internal sealed class LocalMetrics
{
    readonly NetworkMeter network = new();
    public double Download { get; private set; }
    public double Upload { get; private set; }
    public Queue<double> DownloadHistory { get; } = new();
    public void ReadNetwork()
    {
        try
        {
            long down = 0, up = 0;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel))
            { var s = nic.GetIPStatistics(); down += s.BytesReceived; up += s.BytesSent; }
            (Download, Upload) = network.Sample(down, up, DateTimeOffset.Now);
            DownloadHistory.Enqueue(Download); while (DownloadHistory.Count > 45) DownloadHistory.Dequeue();
        }
        catch (NetworkInformationException) { Download = Upload = 0; }
    }
    public static string Rate(double value) => value >= 1048576 ? $"{value / 1048576:0.0} MB/s" : value >= 1024 ? $"{value / 1024:0} KB/s" : $"{value:0} B/s";
    public static string Size(long value) => value >= 1073741824 ? $"{value / 1073741824d:0.0} GB" : $"{value / 1048576d:0.0} MB";
    public static DiskReading? Drive(string? selected)
    {
        try { var drive = new DriveInfo(selected ?? Path.GetPathRoot(Environment.SystemDirectory)!); return drive.IsReady ? new(drive.Name, drive.AvailableFreeSpace, drive.TotalSize) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
}
internal static class DesktopUtilities
{
    [StructLayout(LayoutKind.Sequential)] struct BinInfo { public uint Size; public long Bytes, Items; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHQueryRecycleBin(string? root, ref BinInfo info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHEmptyRecycleBin(nint hwnd, string? root, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint FindWindow(string name, string? caption);
    [DllImport("user32.dll")] static extern bool PostMessage(nint hwnd, uint message, nint wparam, nint lparam);
    [DllImport("user32.dll")] static extern bool MessageBeep(uint type);
    public static (long Items, long Bytes)? RecycleBin()
    { var info = new BinInfo { Size = (uint)Marshal.SizeOf<BinInfo>() }; return SHQueryRecycleBin(null, ref info) == 0 ? (info.Items, info.Bytes) : null; }
    public static void EmptyBin(nint owner) => SHEmptyRecycleBin(owner, null, 0); // Windows shows its own confirmation.
    public static void TimerAlert() => MessageBeep(0x40);
    public static void Keyboard()
    {
        var existing = FindWindow("OSKMainClass", null);
        if (existing != 0) PostMessage(existing, 0x10, 0, 0);
        else Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "osk.exe")) { UseShellExecute = true });
    }
}
