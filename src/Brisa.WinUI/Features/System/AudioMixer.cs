using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Brisa.WinUI;

internal record AudioOutput(string Id, string Name, bool Default);
internal record AudioSession(string Id, string Name, int Volume, bool Muted, bool Active);
internal static partial class Audio
{
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDevices
    { void GetCount(out uint count); void Item(uint index, out IDevice device); }
    [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] struct Variant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public nint Pointer; }
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref Variant value);
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IProperties
    { void GetCount(out uint count); void GetAt(uint index, out PropertyKey key); void GetValue(ref PropertyKey key, out Variant value); }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessionManager
    { void GetAudioSessionControl(nint session, uint flags, out nint control); void GetSimpleAudioVolume(nint session, uint flags, out nint volume); void GetSessionEnumerator(out ISessions sessions); }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessions
    { void GetCount(out int count); void GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session); }
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessionControl
    {
        void GetState(out int state);
        void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        void GetGroupingParam(out Guid grouping); void SetGroupingParam(ref Guid grouping, ref Guid context);
        void RegisterAudioSessionNotification(nint notification); void UnregisterAudioSessionNotification(nint notification);
        void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetProcessId(out uint process);
        [PreserveSig] int IsSystemSoundsSession();
    }
    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessionVolume
    {
        void SetMasterVolume(float value, ref Guid context); void GetMasterVolume(out float value);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context); void GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }
    static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    static T OnDevice<T>(string id, Func<IDevice, T> action)
    {
        var enumerator = (IEnumerator)new Enumerator(); IDevice? device = null;
        try { enumerator.GetDevice(id, out device); return action(device); }
        finally { Release(device); Release(enumerator); }
    }
    static T OnVolume<T>(string id, Func<IVolume, T> action) => OnDevice(id, device =>
    {
        var iid = typeof(IVolume).GUID; object? volume = null;
        try { device.Activate(ref iid, 23, 0, out volume); return action((IVolume)volume); }
        finally { Release(volume); }
    });
    public static IReadOnlyList<AudioOutput> Outputs()
    {
        var enumerator = (IEnumerator)new Enumerator(); IDevices? devices = null; IDevice? current = null;
        try
        {
            string? defaultId = null;
            try { enumerator.GetDefaultAudioEndpoint(0, 1, out current); current.GetId(out defaultId); } catch (COMException) { }
            enumerator.EnumAudioEndpoints(0, 1, out devices); devices.GetCount(out uint count); var outputs = new List<AudioOutput>();
            for (uint i = 0; i < count; i++)
            {
                IDevice? device = null; IProperties? properties = null;
                try
                {
                    devices.Item(i, out device); device.GetId(out string id); device.OpenPropertyStore(0, out properties);
                    var key = new PropertyKey { Format = new("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
                    properties.GetValue(ref key, out var value);
                    string name;
                    try { name = value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) ?? "Salida de audio" : "Salida de audio"; }
                    finally { PropVariantClear(ref value); }
                    outputs.Add(new(id, name, id == defaultId));
                }
                catch (COMException) { /* A disconnected endpoint must not hide the others. */ }
                finally { Release(properties); Release(device); }
            }
            return outputs.OrderByDescending(o => o.Default).ThenBy(o => o.Name).ToArray();
        }
        finally { Release(current); Release(devices); Release(enumerator); }
    }
    public static (int Volume, bool Muted) Read(string id) => OnVolume(id, v => { v.GetMasterVolumeLevelScalar(out float volume); v.GetMute(out bool muted); return ((int)Math.Round(volume * 100), muted); });
    public static void SetVolume(string id, float value) => OnVolume(id, v => { var context = Guid.Empty; v.SetMasterVolumeLevelScalar(Math.Clamp(value, 0, 1), ref context); return 0; });
    public static void SetMute(string id, bool muted) => OnVolume(id, v => { var context = Guid.Empty; v.SetMute(muted, ref context); return 0; });
    static T Sessions<T>(string output, Func<ISessions, T> action) => OnDevice(output, device =>
    {
        var iid = typeof(ISessionManager).GUID; object? manager = null; ISessions? sessions = null;
        try { device.Activate(ref iid, 23, 0, out manager); ((ISessionManager)manager).GetSessionEnumerator(out sessions); return action(sessions); }
        finally { Release(sessions); Release(manager); }
    });
    public static IReadOnlyList<AudioSession> ReadSessions(string output) => Sessions(output, sessions =>
    {
        var result = new List<AudioSession>(); sessions.GetCount(out int count);
        for (int i = 0; i < count; i++)
        {
            object? instance = null;
            try
            {
                sessions.GetSession(i, out instance); var control = (ISessionControl)instance;
                control.GetState(out int state); if (state == 2) continue;
                control.GetSessionInstanceIdentifier(out string id); control.GetProcessId(out uint pid);
                string name = "Aplicación de audio";
                if (control.IsSystemSoundsSession() == 0) name = "Sonidos del sistema";
                else try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; } catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                var volume = (ISessionVolume)instance; volume.GetMasterVolume(out float level); volume.GetMute(out bool muted);
                result.Add(new(id, name, (int)Math.Round(level * 100), muted, state == 1));
            }
            catch (COMException) { }
            finally { Release(instance); }
        }
        return result.DistinctBy(s => s.Id).OrderByDescending(s => s.Active).ThenBy(s => s.Name).ToArray();
    });
    public static bool SetSession(string output, string id, float? level = null, bool? muted = null) => Sessions(output, sessions =>
    {
        sessions.GetCount(out int count);
        for (int i = 0; i < count; i++)
        {
            object? instance = null;
            try
            {
                sessions.GetSession(i, out instance); ((ISessionControl)instance).GetSessionInstanceIdentifier(out string candidate);
                if (candidate != id) continue;
                var volume = (ISessionVolume)instance; var context = Guid.Empty;
                if (level.HasValue) volume.SetMasterVolume(Math.Clamp(level.Value, 0, 1), ref context);
                if (muted.HasValue) volume.SetMute(muted.Value, ref context);
                return true;
            }
            catch (COMException) { }
            finally { Release(instance); }
        }
        return false;
    });
}
