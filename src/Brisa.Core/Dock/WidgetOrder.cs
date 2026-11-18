namespace Brisa.Core;

public static class WidgetOrder
{
    public static bool MoveTo(List<string> order, string id, int destination)
    {
        int source = order.IndexOf(id);
        if (source < 0 || destination < 0 || destination >= order.Count || source == destination) return false;
        order.RemoveAt(source); order.Insert(destination, id); return true;
    }
    public static bool MoveRelative(List<string> order, string id, string target, bool after)
    {
        int source = order.IndexOf(id), destination = order.IndexOf(target);
        if (source < 0 || destination < 0 || id == target) return false;
        if (after) destination++;
        if (source < destination) destination--;
        return MoveTo(order, id, destination);
    }
    public static bool Apply(List<string> order, IEnumerable<string> proposed)
    {
        var next = proposed.ToArray();
        if (next.Length != order.Count || next.Distinct().Count() != next.Length || !order.ToHashSet().SetEquals(next) || order.SequenceEqual(next)) return false;
        order.Clear(); order.AddRange(next); return true;
    }
    public static bool InsertAfter(List<string> order, string id, string? anchor)
    {
        if (order.Contains(id)) return false;
        int index = anchor == null ? 0 : order.IndexOf(anchor) is int at && at >= 0 ? at + 1 : order.Count;
        order.Insert(index, id); return true;
    }
}
