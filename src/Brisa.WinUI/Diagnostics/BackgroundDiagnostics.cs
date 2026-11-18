namespace Brisa.WinUI;
internal static class BackgroundDiagnostics
{
    static int failures;
    public static int FailureCount => System.Threading.Volatile.Read(ref failures);
    static readonly object sync = new();
    static readonly Dictionary<string, DateTimeOffset> last = [];
    public static void Record(string component, Exception error)
    {
        System.Threading.Interlocked.Increment(ref failures);
        lock (sync)
        {
            var now = DateTimeOffset.UtcNow;
            if (last.TryGetValue(component, out var stamp) && now - stamp < TimeSpan.FromMinutes(1)) return;
            last[component] = now;
            // Exception text can include external data; record only its type.
            try { string path = System.IO.Path.Combine(AppContext.BaseDirectory, "brisa-background.log"); if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length > 262144) System.IO.File.Move(path, path + ".previous", true); System.IO.File.AppendAllText(path, $"{now:O} {component}: {error.GetType().FullName}{Environment.NewLine}"); }
            catch (Exception fileError) when (fileError is System.IO.IOException or UnauthorizedAccessException) { }
        }
    }
}
