using System.Collections.Concurrent;
namespace Brisa.Core;

/// <summary>Runs independent background jobs without allowing duplicates of the same job.</summary>
public sealed class BackgroundWork(Action<string, Exception> report)
{
    readonly ConcurrentDictionary<string, byte> running = new();
    public int RunningCount => running.Count;
    public async Task RunAsync(string key, Func<CancellationToken, Task> action, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested || !running.TryAdd(key, 0)) return;
        try { await action(cancellation).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error) when (error is not OutOfMemoryException) { report(key, error); }
        finally { running.TryRemove(key, out _); }
    }
}
