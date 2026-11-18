namespace Brisa.Core;
public enum FocusPhase { Work, ShortBreak, LongBreak }
public record FocusOptions(int WorkMinutes = 25, int ShortBreakMinutes = 5, int LongBreakMinutes = 15, int Rounds = 4, bool AutoAdvance = false)
{
    public FocusOptions Normalize() => this with { WorkMinutes = Math.Clamp(WorkMinutes, 1, 180), ShortBreakMinutes = Math.Clamp(ShortBreakMinutes, 1, 60), LongBreakMinutes = Math.Clamp(LongBreakMinutes, 1, 120), Rounds = Math.Clamp(Rounds, 1, 12) };
}
public record FocusCheckpoint(FocusPhase Phase, int Completed, double RemainingSeconds);
public sealed class FocusSession
{
    readonly Countdown timer = new();
    public FocusOptions Options { get; private set; }
    public FocusPhase Phase { get; private set; }
    public int Completed { get; private set; }
    public bool Running => timer.Running;
    public TimeSpan Duration => TimeSpan.FromMinutes(Phase switch { FocusPhase.ShortBreak => Options.ShortBreakMinutes, FocusPhase.LongBreak => Options.LongBreakMinutes, _ => Options.WorkMinutes });
    public FocusSession(FocusOptions options) { Options = options.Normalize(); Reset(); }
    public void Configure(FocusOptions options) { Options = options.Normalize(); Reset(); }
    public void Reset() { Phase = FocusPhase.Work; Completed = 0; timer.Set(Duration); }
    public TimeSpan Remaining(DateTimeOffset now) => timer.Remaining(now);
    public void Toggle(DateTimeOffset now) { if (timer.Running) timer.Pause(now); else timer.Start(now); }
    public bool Tick(DateTimeOffset now)
    {
        if (!timer.CompleteIfDue(now)) return false;
        if (Phase == FocusPhase.Work) { Completed++; Phase = Completed % Options.Rounds == 0 ? FocusPhase.LongBreak : FocusPhase.ShortBreak; }
        else Phase = FocusPhase.Work;
        timer.Set(Duration); if (Options.AutoAdvance) timer.Start(now); return true;
    }
    public FocusCheckpoint Snapshot(DateTimeOffset now) => new(Phase, Completed, Remaining(now).TotalSeconds);
    public void Restore(FocusCheckpoint? saved)
    {
        if (saved == null || !Enum.IsDefined(saved.Phase) || !double.IsFinite(saved.RemainingSeconds)) return;
        Phase = saved.Phase; Completed = Math.Clamp(saved.Completed, 0, 1000000); timer.Set(TimeSpan.FromSeconds(Math.Clamp(saved.RemainingSeconds, 1, Duration.TotalSeconds)));
    }
}
public record NoteItem(string Id, string Text, DateTimeOffset UpdatedAt, bool Archived = false);
public static class AlertPolicy
{
    public static bool IsQuiet(DateTime local, bool enabled, int start, int end)
    {
        if (!enabled || start == end) return false;
        return start < end ? local.Hour >= start && local.Hour < end : local.Hour >= start || local.Hour < end;
    }
}
