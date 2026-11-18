namespace Brisa.Core;

public static class PinnedLayout
{
    public static bool EnsureVisible(DockSettings settings)
    {
        if (settings.Widgets.Contains("apps")) return false;
        settings.Widgets.Insert(0, "apps"); return true;
    }
    public static List<string> Normalize(DockSettings settings)
    {
        var all = settings.Apps.Select(a => "app:" + a.Id).Concat(settings.AppGroups.Select(g => "group:" + g.Id)).ToArray();
        var valid = all.ToHashSet();
        return (settings.PinnedOrder ?? []).Where(valid.Contains).Distinct().Concat(all).Distinct().ToList();
    }
    public static List<string> Visible(DockSettings settings)
    {
        var grouped = settings.AppGroups.SelectMany(g => g.AppIds).Select(id => "app:" + id).ToHashSet();
        return Normalize(settings).Where(id => !grouped.Contains(id)).ToList();
    }
    public static bool Move(DockSettings settings, string id, int delta)
    {
        var visible = Visible(settings); int from = visible.IndexOf(id), to = from + delta;
        if (from < 0 || to < 0 || to >= visible.Count || from == to) return false;
        var order = Normalize(settings); int a = order.IndexOf(id), b = order.IndexOf(visible[to]);
        (order[a], order[b]) = (order[b], order[a]); settings.PinnedOrder = order; return true;
    }
    public static bool ApplyVisibleOrder(DockSettings settings, IEnumerable<string> requested)
    {
        var next = requested.ToArray(); var visible = Visible(settings);
        var visibleSet = visible.ToHashSet(StringComparer.Ordinal);
        if (next.Length != visible.Count || next.Distinct(StringComparer.Ordinal).Count() != next.Length || !visibleSet.SetEquals(next) || visible.SequenceEqual(next)) return false;
        int index = 0;
        settings.PinnedOrder = Normalize(settings).Select(key => visibleSet.Contains(key) ? next[index++] : key).ToList();
        return true;
    }
}
