using System.Runtime.InteropServices;
namespace Brisa.WinUI;
internal static partial class Audio
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class Enumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IEnumerator
    {
        void EnumAudioEndpoints(int flow, uint state, out IDevices devices);
        void GetDefaultAudioEndpoint(int flow, int role, out IDevice device);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDevice
    {
        void Activate(ref Guid iid, uint context, nint activation, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        void OpenPropertyStore(uint access, out IProperties properties);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetState(out uint state);
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IVolume
    {
        void RegisterControlChangeNotify(nint notify);
        void UnregisterControlChangeNotify(nint notify);
        void GetChannelCount(out uint channels);
        void SetMasterVolumeLevel(float level, ref Guid context);
        void SetMasterVolumeLevelScalar(float level, ref Guid context);
        void GetMasterVolumeLevel(out float level);
        void GetMasterVolumeLevelScalar(out float level);
        void SetChannelVolumeLevel(uint channel, float level, ref Guid context);
        void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        void GetChannelVolumeLevel(uint channel, out float level);
        void GetChannelVolumeLevelScalar(uint channel, out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
    static T Use<T>(Func<IVolume, T> action)
    {
        var enumerator = (IEnumerator)new Enumerator(); IDevice? device = null; object? endpoint = null;
        try
        {
            enumerator.GetDefaultAudioEndpoint(0, 1, out device); var id = typeof(IVolume).GUID;
            device.Activate(ref id, 23, 0, out endpoint); return action((IVolume)endpoint);
        }
        finally
        {
            if (endpoint != null) Marshal.ReleaseComObject(endpoint);
            if (device != null) Marshal.ReleaseComObject(device);
            Marshal.ReleaseComObject(enumerator);
        }
    }
    public static (int Volume, bool Muted) Read() => Use(v => { v.GetMasterVolumeLevelScalar(out float volume); v.GetMute(out bool mute); return ((int)Math.Round(volume * 100), mute); });
    public static void SetVolume(float value) => Use(v => { var context = Guid.Empty; v.SetMasterVolumeLevelScalar(Math.Clamp(value, 0, 1), ref context); return 0; });
    public static void SetMute(bool value) => Use(v => { var context = Guid.Empty; v.SetMute(value, ref context); return 0; });
}
