using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
namespace Brisa.Core;
public record CalendarImport(IReadOnlyList<CalendarEvent> Events, int Omitted);
public static class ICalendar
{
    public const int MaxBytes = 5 * 1024 * 1024;
    static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\\n").Replace(";", "\\;").Replace(",", "\\,");
    static string Unescape(string text)
    {
        var result = new StringBuilder();
        for (int i = 0; i < text.Length; i++) { if (text[i] == '\\' && i + 1 < text.Length) { char c = text[++i]; result.Append(c is 'n' or 'N' ? '\n' : c); } else result.Append(text[i]); }
        return result.ToString();
    }
    internal static string Id(string uid)
    {
        if (uid.StartsWith("brisa-", StringComparison.Ordinal) && uid.EndsWith("@local", StringComparison.Ordinal))
        {
            string candidate = uid[6..^6];
            if (Guid.TryParseExact(candidate, "N", out _) || (candidate.StartsWith("ics-", StringComparison.Ordinal) && candidate.Length == 68 && candidate[4..].All(Uri.IsHexDigit))) return candidate;
        }
        return "ics-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uid))).ToLowerInvariant();
    }
    static (DateTimeOffset At, bool AllDay) Date(string key, string value)
    {
        var parameters = key.Split(';').Skip(1).Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1].Trim('"'), StringComparer.OrdinalIgnoreCase);
        bool day = value.Length == 8;
        if (parameters.TryGetValue("VALUE", out var kind) && !kind.Equals(day ? "DATE" : "DATE-TIME", StringComparison.OrdinalIgnoreCase)) throw new FormatException();
        if ((day || value.EndsWith('Z')) && parameters.ContainsKey("TZID")) throw new FormatException();
        var date = DateTime.ParseExact(value.TrimEnd('Z'), day ? "yyyyMMdd" : "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None);
        if (value.EndsWith('Z')) return (new(date, TimeSpan.Zero), false);
        var zone = TimeZoneInfo.Local;
        if (parameters.TryGetValue("TZID", out string? tz) && !TimeZoneInfo.TryFindSystemTimeZoneById(tz, out zone)) throw new FormatException("Zona horaria no compatible.");
        if (zone.IsInvalidTime(date)) throw new FormatException("Hora inexistente.");
        var offset = zone.IsAmbiguousTime(date) ? zone.GetAmbiguousTimeOffsets(date).Max() : zone.GetUtcOffset(date);
        return (new(date, offset), day);
    }
    public static CalendarImport Parse(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes) throw new InvalidDataException("El calendario supera 5 MB.");
        var unfolded = new List<string>(); StringBuilder? folded = null;
        foreach (string line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        { if ((line.StartsWith(' ') || line.StartsWith('\t')) && folded != null) folded.Append(line.AsSpan(1)); else { if (folded != null) unfolded.Add(folded.ToString()); folded = new(line); } }
        if (folded != null) unfolded.Add(folded.ToString());
        if (!unfolded.Any(l => l.TrimStart('\uFEFF').Equals("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase)) || !unfolded.Any(l => l.Equals("END:VCALENDAR", StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("No es un archivo iCalendar completo.");
        var recurring = CalendarSchedule.ReadSeries(unfolded);
        var result = recurring.Events.ToList(); var properties = new Dictionary<string, (string Key, string Value)>(StringComparer.OrdinalIgnoreCase); int depth = 0, omitted = recurring.Omitted;
        foreach (string line in unfolded)
        {
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase)) { properties.Clear(); depth = 1; continue; }
            if (depth == 0) continue;
            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (depth != 1) { depth = 0; omitted++; continue; }
                depth = 0;
                try
                {
                    if (properties.TryGetValue("UID", out var seriesUid) && recurring.Handled.Contains(seriesUid.Value)) continue;
                    if (properties.ContainsKey("RRULE") || properties.ContainsKey("RECURRENCE-ID") || properties.ContainsKey("RDATE") || properties.ContainsKey("EXDATE") || (properties.TryGetValue("STATUS", out var status) && status.Value.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))) { omitted++; continue; }
                    var startValue = properties["DTSTART"]; var start = Date(startValue.Key, startValue.Value);
                    DateTimeOffset end;
                    if (properties.TryGetValue("DTEND", out var endValue)) { var parsedEnd = Date(endValue.Key, endValue.Value); if (parsedEnd.AllDay != start.AllDay) throw new FormatException(); end = parsedEnd.At; }
                    else if (properties.TryGetValue("DURATION", out var duration))
                    {
                        var span = XmlConvert.ToTimeSpan(duration.Value);
                        if (start.AllDay) { if (span.Ticks % TimeSpan.TicksPerDay != 0) throw new FormatException(); var date = start.At.Date.AddDays(span.TotalDays); end = new(date, TimeZoneInfo.Local.GetUtcOffset(date)); }
                        else end = start.At + span;
                    }
                    else if (start.AllDay) { var date = start.At.Date.AddDays(1); end = new(date, TimeZoneInfo.Local.GetUtcOffset(date)); }
                    else throw new FormatException("El evento necesita fecha final.");
                    string title = properties.TryGetValue("SUMMARY", out var summary) ? Unescape(summary.Value).Trim() : "Evento sin título";
                    string uid = properties.TryGetValue("UID", out var uidValue) ? Unescape(uidValue.Value) : title + "|" + start.At.ToString("O") + "|" + end.ToString("O");
                    if (title.Length == 0 || end <= start.At || uid.Length > 1024 || result.Count >= 1000) { omitted++; continue; }
                    result.Add(new(Id(uid), title[..Math.Min(title.Length, 120)], start.At, end, start.AllDay, ExternalUid: uid));
                }
                catch (Exception e) when (e is FormatException or KeyNotFoundException or ArgumentException or OverflowException) { omitted++; }
                continue;
            }
            if (line.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase)) { depth++; continue; }
            if (line.StartsWith("END:", StringComparison.OrdinalIgnoreCase)) { depth--; continue; }
            if (depth != 1) continue;
            int colon = line.IndexOf(':'); if (colon <= 0) continue;
            string key = line[..colon]; string name = key.Split(';')[0]; properties[name] = (key, line[(colon + 1)..]);
        }
        return new(result.DistinctBy(e => e.Id).ToArray(), omitted + (depth > 0 ? 1 : 0));
    }
    public static string Export(IEnumerable<CalendarEvent> events)
    {
        var result = new StringBuilder();
        void Line(string value)
        {
            int bytes = 0;
            foreach (var rune in value.EnumerateRunes()) { if (bytes + rune.Utf8SequenceLength > 75) { result.Append("\r\n "); bytes = 1; } result.Append(rune.ToString()); bytes += rune.Utf8SequenceLength; }
            result.Append("\r\n");
        }
        Line("BEGIN:VCALENDAR"); Line("VERSION:2.0"); Line("PRODID:-//Brisa Dock//Calendario local//ES"); Line("CALSCALE:GREGORIAN");
        foreach (var item in CalendarEvents.Normalize(events).Where(e => !e.Archived))
        {
            if (item.RecurrenceIcs != null) { foreach (var componentLine in CalendarSchedule.ComponentLines(item.RecurrenceIcs)) Line(componentLine); continue; }
            Line("BEGIN:VEVENT"); Line("UID:" + Escape(item.ExternalUid ?? "brisa-" + item.Id + "@local")); Line("DTSTAMP:" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
            Line(item.AllDay ? "DTSTART;VALUE=DATE:" + item.Start.LocalDateTime.ToString("yyyyMMdd") : "DTSTART:" + item.Start.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'"));
            Line(item.AllDay ? "DTEND;VALUE=DATE:" + item.End.LocalDateTime.ToString("yyyyMMdd") : "DTEND:" + item.End.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'"));
            Line("SUMMARY:" + Escape(item.Title)); Line("END:VEVENT");
        }
        Line("END:VCALENDAR"); return result.ToString();
    }
    public static (int Added, int Updated) Merge(List<CalendarEvent> current, IReadOnlyList<CalendarEvent> incoming)
    {
        var events = CalendarEvents.Normalize(incoming);
        if (current.Count + events.Count(e => current.All(c => c.Id != e.Id)) > 1000) throw new InvalidDataException("El calendario superaría el límite de 1000 eventos.");
        int added = 0, updated = 0;
        foreach (var item in events)
        {
            int at = current.FindIndex(e => e.Id == item.Id);
            if (at < 0) { current.Add(item); added++; }
            else { var previous = current[at]; current[at] = item with { Archived = previous.Archived, Reminder = previous.Reminder, ReminderSent = previous.Start == item.Start && previous.ReminderSent }; updated++; }
        }
        return (added, updated);
    }
}
