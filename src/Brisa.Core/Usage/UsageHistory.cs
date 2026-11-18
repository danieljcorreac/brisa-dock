namespace Brisa.Core;
public record QuotaSample(string Provider, string Window, double UsedPercent, DateTimeOffset At, DateTimeOffset? Reset, string? Source = null);
public static class UsageHistory
{
    public static IReadOnlyList<IReadOnlyList<QuotaSample>> Trend(IEnumerable<QuotaSample> history, string provider, string window)
    {
        var samples = history.Where(s => s.Provider == provider && s.Window == window && double.IsFinite(s.UsedPercent) && s.UsedPercent is >= 0 and <= 100).OrderBy(s => s.At).DistinctBy(s => (s.At, s.Reset)).TakeLast(80);
        var segments = new List<IReadOnlyList<QuotaSample>>(); List<QuotaSample>? current = null;
        foreach (var sample in samples)
        {
            if (current == null || current[^1].Reset != sample.Reset || current[^1].Source != sample.Source) { current = []; segments.Add(current); }
            current.Add(sample);
        }
        return segments;
    }
    public static bool Append(List<QuotaSample> history, UsageSnapshot snapshot, DateTimeOffset now)
    {
        if (snapshot.WithFreshness(now).State != ConnectionState.Connected || snapshot.UpdatedAt == null) return false;
        bool changed = false;
        foreach (var window in snapshot.Windows.Where(w => !w.Expired(now) && double.IsFinite(w.UsedPercent) && w.UsedPercent is >= 0 and <= 100))
        {
            var last = history.LastOrDefault(s => s.Provider == snapshot.ProviderId && s.Window == window.Label);
            if (last != null && last.Source == snapshot.Source && last.UsedPercent == window.UsedPercent && last.Reset == window.ResetsAt && (last.At == snapshot.UpdatedAt.Value || now - last.At < TimeSpan.FromMinutes(30))) continue;
            history.Add(new(snapshot.ProviderId, window.Label, window.UsedPercent, snapshot.UpdatedAt.Value, window.ResetsAt, snapshot.Source)); changed = true;
        }
        if (history.Count > 500) history.RemoveRange(0, history.Count - 500);
        return changed;
    }
    public static string AlertKey(string provider, QuotaWindow window) => provider + ":" + window.Label + ":" + window.ResetsAt?.ToUnixTimeSeconds();
}
