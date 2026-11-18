using System.Runtime.InteropServices;
namespace Brisa.WinUI;
internal record RecentDocument(string Name, string Path, bool Frequent);
internal record RecentDocumentList(bool Identified, IReadOnlyList<RecentDocument> Items);
internal static class RecentDocuments
{
    [ComImport, Guid("86BEC222-30F2-47E0-9F25-60D11CD75C28")] class DocumentLists { }
    [ComImport, Guid("3C594F9F-9F30-47A1-979A-C9E83D3D0A06"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ILists
    { void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string id); void GetList(int type, uint count, ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object items); }
    [ComImport, Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IObjects
    { void GetCount(out uint count); void GetAt(uint at, ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object item); }
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IItem
    { void BindToHandler(nint context, ref Guid handler, ref Guid iid, out nint value); void GetParent(out IItem parent); void GetDisplayName(uint type, [MarshalAs(UnmanagedType.LPWStr)] out string name); }
    [StructLayout(LayoutKind.Sequential)] struct Key { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] struct Variant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public nint Pointer; }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IProperties
    { void GetCount(out uint count); void GetAt(uint index, out Key key); void GetValue(ref Key key, out Variant value); }
    [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(nint window, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IProperties properties);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHGetPropertyStoreFromParsingName(string path, nint context, uint flags, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IProperties properties);
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref Variant value);
    static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    internal static string? AppId(nint window, string? shortcut)
    {
        IProperties? properties = null;
        try
        {
            var iid = typeof(IProperties).GUID;
            int result = shortcut?.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) == true
                ? SHGetPropertyStoreFromParsingName(shortcut, 0, 0, ref iid, out properties)
                : SHGetPropertyStoreForWindow(window, ref iid, out properties);
            if (result < 0) return null;
            var key = new Key { Format = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
            properties.GetValue(ref key, out var value);
            try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) : null; }
            finally { PropVariantClear(ref value); }
        }
        catch (COMException) { return null; }
        finally { Release(properties); }
    }
    public static RecentDocumentList Read(nint window, string? shortcut)
    {
        string? id = shortcut != null ? Brisa.Core.AppTargets.AppId(shortcut) : null;
        id ??= AppId(window, shortcut);
        if (string.IsNullOrWhiteSpace(id) && shortcut != null) id = AppId(window, null);
        if (string.IsNullOrWhiteSpace(id)) return new(false, []);
        object? lists = null; var result = new List<RecentDocument>();
        try
        {
            lists = new DocumentLists(); ((ILists)lists).SetAppID(id);
            for (int type = 0; type < 2; type++)
            {
                object? items = null;
                try
                {
                    var arrayId = typeof(IObjects).GUID; ((ILists)lists).GetList(type, 12, ref arrayId, out items); var array = (IObjects)items; array.GetCount(out uint count);
                    for (uint i = 0; i < Math.Min(count, 12); i++)
                    {
                        object? item = null;
                        try
                        {
                            var itemId = typeof(IItem).GUID; array.GetAt(i, ref itemId, out item); var shell = (IItem)item;
                            shell.GetDisplayName(0x80058000, out string path); shell.GetDisplayName(0, out string name);
                            if (System.IO.Path.IsPathFullyQualified(path)) result.Add(new(name, path, type == 1));
                        }
                        catch (Exception e) when (e is COMException or ArgumentException) { /* Unsupported shell items are skipped independently. */ }
                        finally { Release(item); }
                    }
                }
                catch (COMException) { }
                finally { Release(items); }
            }
        }
        catch (COMException) { }
        finally { Release(lists); }
        return new(true, result.DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
