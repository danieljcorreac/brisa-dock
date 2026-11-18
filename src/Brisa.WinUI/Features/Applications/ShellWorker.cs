using System.Collections.Concurrent;
using System.Runtime.InteropServices;
namespace Brisa.WinUI;
internal static class ShellWorker
{
    static readonly BlockingCollection<Action> jobs = new(256);
    static ShellWorker()
    {
        var worker = new Thread(() => { CoInitializeEx(0, 2); try { foreach (var job in jobs.GetConsumingEnumerable()) job(); } finally { CoUninitialize(); } }) { IsBackground = true, Name = "Brisa Shell" };
        worker.SetApartmentState(ApartmentState.STA); worker.Start();
    }
    [DllImport("ole32.dll")] static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] static extern void CoUninitialize();
    public static Task<T> Run<T>(Func<T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!jobs.TryAdd(() => { try { completion.SetResult(work()); } catch (Exception e) { completion.SetException(e); } })) completion.SetException(new InvalidOperationException("Shell busy."));
        return completion.Task;
    }
}
