namespace Brisa.Core;

public record MonitorChoice(string Id, bool Primary);
public static class MonitorSelection
{
    // Keep the preference separate from the connected selection: unplugging must not erase it.
    public static string? Resolve(IReadOnlyList<MonitorChoice> connected, string? preferred) =>
        connected.FirstOrDefault(m => string.Equals(m.Id, preferred, StringComparison.OrdinalIgnoreCase))?.Id
        ?? connected.FirstOrDefault(m => m.Primary)?.Id ?? connected.FirstOrDefault()?.Id;
}
