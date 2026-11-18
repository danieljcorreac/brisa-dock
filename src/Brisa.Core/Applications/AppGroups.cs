namespace Brisa.Core;

public record AppGroup(string Id, string Name, List<string> AppIds, string Accent = "mint");
public static class AppGroups
{
    public static readonly string[] Accents = ["mint", "blue", "peach", "violet", "rose"];
    public static List<AppGroup> Normalize(IEnumerable<AppGroup>? groups, IReadOnlyList<AppShortcut> apps)
    {
        var available = apps.Select(a => a.Id).ToHashSet(); var used = new HashSet<string>();
        return (groups ?? []).Where(g => g != null && !string.IsNullOrWhiteSpace(g.Id)).DistinctBy(g => g.Id).Take(12)
            .Select(g => new AppGroup(g.Id, string.IsNullOrWhiteSpace(g.Name) ? "Grupo" : g.Name.Trim()[..Math.Min(g.Name.Trim().Length, 28)],
                (g.AppIds ?? []).Where(id => id != null && available.Contains(id) && used.Add(id)).ToList(),
                Accents.Contains(g.Accent) ? g.Accent : "mint")).ToList();
    }
    public static bool Assign(DockSettings settings, string appId, string? groupId)
    {
        if (!settings.Apps.Any(a => a.Id == appId) || (groupId != null && !settings.AppGroups.Any(g => g.Id == groupId))) return false;
        foreach (var group in settings.AppGroups) group.AppIds.RemoveAll(id => id == appId);
        if (groupId != null) settings.AppGroups.First(g => g.Id == groupId).AppIds.Add(appId);
        return true;
    }
    public static void RemoveShortcut(DockSettings settings, string appId)
    {
        settings.Apps.RemoveAll(a => a.Id == appId);
        foreach (var group in settings.AppGroups) group.AppIds.RemoveAll(id => id == appId);
    }
}
