using Brisa.Core;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
namespace Brisa.WinUI;
internal record ShellIcon(int Width, int Height, byte[] Pixels);
internal static class ShellIcons
{
    [StructLayout(LayoutKind.Sequential)] struct Size { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct Bitmap { public int Type, Width, Height, WidthBytes; public ushort Planes, BitsPixel; public nint Bits; }
    [StructLayout(LayoutKind.Sequential)] struct BitmapInfo { public uint Size; public int Width, Height; public ushort Planes, Bits; public uint Compression, ImageSize; public int X, Y; public uint Used, Important; }
    [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IImageFactory { void GetImage(Size size, uint flags, out nint bitmap); }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)] static extern void SHCreateItemFromParsingName(string path, nint context, in Guid iid, [MarshalAs(UnmanagedType.Interface)] out IImageFactory item);
    [DllImport("gdi32.dll")] static extern int GetObjectW(nint handle, int size, out Bitmap bitmap);
    [DllImport("gdi32.dll")] static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] static extern int GetDIBits(nint dc, nint bitmap, uint start, uint lines, [Out] byte[] pixels, ref BitmapInfo info, uint usage);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(nint handle);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(nint dc);
    static readonly ConcurrentDictionary<string, Task<ShellIcon?>> cache = new(StringComparer.OrdinalIgnoreCase);
    public static Task<ShellIcon?> Read(string target)
    {
        if (cache.Count > 256) cache.Clear();
        return cache.GetOrAdd(target, path => ShellWorker.Run(() => Extract(path)));
    }
    static ShellIcon? Extract(string target)
    {
        IImageFactory? factory = null; nint bitmap = 0, dc = 0;
        try
        {
            bool gameArt = false;
            if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) target = AppLaunchers.PreferredTarget(target);
            if (target.StartsWith(@"\\", StringComparison.Ordinal)) return null;
            if (target == "explorer.exe") target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), target);
            else if (target == "calc.exe") target = Path.Combine(Environment.SystemDirectory, target);
            else if (AppTargets.SteamId(target) is string game)
            {
                target = SteamApplications.Read().FirstOrDefault(g => g.Id == game)?.Icon ?? SteamApplications.Client ?? ""; gameArt = target.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase);
            }
            else if (target.Equals("steam://open/main", StringComparison.OrdinalIgnoreCase)) target = SteamApplications.Client ?? "";
            else if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase)) target = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe") }.FirstOrDefault(File.Exists) ?? "";
            if (target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var link = ShortcutIdentity.Describe(target);
                if (link.Target.StartsWith(@"\\") || link.Icon?.StartsWith(@"\\") == true) return null;
                if (!File.Exists(link.Icon) && AppLaunchers.UpdaterExecutable(link.Target, link.Arguments) is string current) target = current;
            }
            if (target.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            {
                var shortcut = ShortcutIdentity.Describe(target);
                if (shortcut.Icon is string icon && !icon.StartsWith(@"\\") && Path.IsPathFullyQualified(icon) && File.Exists(icon)) target = icon;
            }
            if (target.Length == 0 || target.StartsWith(@"\\") || AppTargets.AppId(target) == null && !File.Exists(target) && !Directory.Exists(target)) return null;
            var iid = typeof(IImageFactory).GUID; SHCreateItemFromParsingName(target, 0, in iid, out factory);
            // The Shell selects application icons; only local Steam artwork uses a thumbnail.
            factory.GetImage(new Size { X = 96, Y = 96 }, gameArt ? 9u : 5u, out bitmap);
            if (bitmap == 0 || GetObjectW(bitmap, Marshal.SizeOf<Bitmap>(), out var source) == 0 || source.Width is <= 0 or > 512 || source.Height is <= 0 or > 512) return null;
            var info = new BitmapInfo { Size = (uint)Marshal.SizeOf<BitmapInfo>(), Width = source.Width, Height = -source.Height, Planes = 1, Bits = 32 };
            byte[] pixels = new byte[source.Width * source.Height * 4]; dc = CreateCompatibleDC(0);
            if (dc == 0 || GetDIBits(dc, bitmap, 0, (uint)source.Height, pixels, ref info, 0) == 0) return null;
            // Some legacy Shell icons are opaque DIBs with an unused alpha channel.
            if (!Enumerable.Range(0, pixels.Length / 4).Any(i => pixels[i * 4 + 3] != 0)) for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            return new(source.Width, source.Height, pixels);
        }
        catch (Exception e) when (e is COMException or IOException or UnauthorizedAccessException or ArgumentException) { return null; }
        finally { if (dc != 0) DeleteDC(dc); if (bitmap != 0) DeleteObject(bitmap); if (factory != null) Marshal.ReleaseComObject(factory); }
    }
}
