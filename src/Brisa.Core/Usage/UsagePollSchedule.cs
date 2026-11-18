namespace Brisa.Core;

/// <summary>Independent polling budgets; manual refresh never queues duplicate requests.</summary>
public sealed class UsagePollSchedule
{
    sealed class Entry { public bool Running; public DateTimeOffset Automatic, Manual; }
    readonly Dictionary<string, Entry> entries = [];
    public bool TryStart(string key, DateTimeOffset now, TimeSpan interval, bool manual)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            if (entries.Count >= 32) entries.Clear();
            entries[key] = entry = new();
        }
        if (entry.Running || now < (manual ? entry.Manual : entry.Automatic)) return false;
        entry.Running = true; entry.Automatic = now + interval; entry.Manual = now.AddSeconds(30); return true;
    }
    public void Complete(string key) { if (entries.TryGetValue(key, out var entry)) entry.Running = false; }
    public void SetAutomaticDue(string key, DateTimeOffset next) { if (entries.TryGetValue(key, out var entry)) entry.Automatic = next; }
}
