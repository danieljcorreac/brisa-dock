using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Brisa.WinUI;

internal record HealthSnapshot(DateTimeOffset Time, double? CpuPercent, long PrivateBytes, long WorkingBytes, long ManagedBytes, int Handles, uint GdiObjects, uint UserObjects, int BackgroundJobs, long Ticks, double MeanTickMs, double MaxTickMs, int BackgroundErrors);
internal static class RuntimeHealth
{
    [DllImport("user32.dll")] static extern uint GetGuiResources(nint process, uint flags);
    static long ticks; static double totalMs, maxMs;
    static long lastCpuStamp; static TimeSpan lastCpu; static double? lastCpuPercent;
    public static void Tick(double elapsed) { ticks++; totalMs += elapsed; maxMs = Math.Max(maxMs, elapsed); }
    public static HealthSnapshot Capture(int jobs)
    {
        using var process = Process.GetCurrentProcess(); long now = Stopwatch.GetTimestamp(); var cpu = process.TotalProcessorTime;
        if (lastCpuStamp == 0) { lastCpuStamp = now; lastCpu = cpu; }
        else if (Stopwatch.GetElapsedTime(lastCpuStamp, now) >= TimeSpan.FromSeconds(1))
        {
            lastCpuPercent = Math.Clamp((cpu - lastCpu).TotalMilliseconds / Stopwatch.GetElapsedTime(lastCpuStamp, now).TotalMilliseconds / Environment.ProcessorCount * 100, 0, 100);
            lastCpuStamp = now; lastCpu = cpu;
        }
        double? percent = lastCpuPercent;
        return new(DateTimeOffset.UtcNow, percent.HasValue ? Math.Round(percent.Value, 3) : null, process.PrivateMemorySize64, process.WorkingSet64, GC.GetTotalMemory(false), process.HandleCount, GetGuiResources(process.Handle, 0), GetGuiResources(process.Handle, 1), jobs, ticks, ticks == 0 ? 0 : totalMs / ticks, maxMs, BackgroundDiagnostics.FailureCount);
    }
}
