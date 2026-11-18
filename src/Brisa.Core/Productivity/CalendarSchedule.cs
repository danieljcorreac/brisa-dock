using Ical.Net;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;
using Ical.Net.Serialization;
using LibEvent = Ical.Net.CalendarComponents.CalendarEvent;
namespace Brisa.Core;
public record CalendarOccurrence(CalendarEvent Series, string Title, DateTimeOffset Start, DateTimeOffset End, DateTimeOffset? OriginalStart = null)
{
    public string Key => Series.Id + ":" + Start.UtcTicks;
}
public static class CalendarSchedule
{
    static DateTimeOffset Instant(CalDateTime value) => value.HasTime ? new(value.AsUtc) : new(value.Value.Date);
    static readonly object gate = new();
    static readonly Dictionary<string, Calendar> parsed = [];
    public static bool Supported(Calendar calendar) => calendar.Events.All(e => e.RecurrenceRule is not { } r || ((r.Frequency is FrequencyType.Daily or FrequencyType.Weekly or FrequencyType.Monthly or FrequencyType.Yearly) && r.ByHour.Count == 0 && r.ByMinute.Count == 0 && r.BySecond.Count == 0));
    public static IReadOnlyList<CalendarOccurrence> Between(IEnumerable<CalendarEvent> events, DateTimeOffset from, DateTimeOffset to)
    {
        var result = new List<CalendarOccurrence>();
        foreach (var item in events.Where(e => !e.Archived))
        {
            if (item.RecurrenceIcs == null) { if (item.End > from && item.Start < to) result.Add(new(item, item.Title, item.Start, item.End)); continue; }
            lock (gate)
            {
                try
                {
                    if (!parsed.TryGetValue(item.RecurrenceIcs, out var calendar))
                    { calendar = Calendar.Load(item.RecurrenceIcs) ?? throw new InvalidDataException(); if (parsed.Count >= 128) parsed.Clear(); parsed[item.RecurrenceIcs] = calendar; }
                    if (!Supported(calendar)) continue;
                    var duration = item.End - item.Start; var earliest = from.UtcDateTime.Ticks > duration.Ticks ? from - duration : DateTimeOffset.MinValue;
                    var occurrences = calendar.GetOccurrences(new CalDateTime(earliest.UtcDateTime), new EvaluationOptions { MaxUnmatchedIncrementsLimit = 1000 }).TakeWhileBefore(new CalDateTime(to.UtcDateTime)).Take(4096);
                    foreach (var occurrence in occurrences)
                    {
                        var source = occurrence.Source as LibEvent;
                        if (source?.Status?.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) == true) continue;
                        var start = Instant(occurrence.Period.StartTime); var end = Instant(occurrence.Period.EffectiveEndTime ?? occurrence.Period.StartTime);
                        if (end > from && start < to) result.Add(new(item, string.IsNullOrWhiteSpace(source?.Summary) ? item.Title : source.Summary, start, end, source?.RecurrenceIdentifier is { } original ? Instant(original.StartTime) : start));
                    }
                }
                catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException or InvalidDataException or TimeZoneNotFoundException or EvaluationException) { }
            }
        }
        return result.DistinctBy(o => o.Key).OrderBy(o => o.Start).ToArray();
    }
    static CalDateTime Zoned(DateTimeOffset instant, bool allDay)
    {
        if (allDay) return new CalDateTime(DateOnly.FromDateTime(instant.LocalDateTime));
        string zone = TimeZoneInfo.Local.Id;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone, out string? iana)) zone = iana;
        return new CalDateTime(instant.LocalDateTime, zone);
    }
    public static CalendarEvent WithRule(CalendarEvent item, string? rule, bool preserve = false)
    {
        if (string.IsNullOrWhiteSpace(rule) && !preserve) return item with { RecurrenceIcs = null };
        var calendar = preserve && item.RecurrenceIcs != null ? Calendar.Load(item.RecurrenceIcs) ?? throw new InvalidDataException() : new Calendar();
        var master = calendar.Events.FirstOrDefault(e => e.RecurrenceIdentifier == null);
        if (master == null) { master = new LibEvent(); calendar.Events.Add(master); }
        master.Uid = item.ExternalUid ?? "brisa-" + item.Id + "@local"; master.Summary = item.Title;
        master.DtStart = Zoned(item.Start, item.AllDay); master.DtEnd = Zoned(item.End, item.AllDay);
        if (!preserve) master.RecurrenceRule = new RecurrencePattern(rule!);
        if (!Supported(calendar)) throw new InvalidDataException("La regla de repetición no es compatible.");
        return item with { RecurrenceIcs = new CalendarSerializer(calendar).SerializeToString() };
    }
    public record ExceptionInfo(DateTimeOffset OriginalStart, string Title, bool Cancelled);
    public static IReadOnlyList<ExceptionInfo> Exceptions(CalendarEvent series)
    {
        if (series.RecurrenceIcs == null) return [];
        var calendar = Calendar.Load(series.RecurrenceIcs) ?? throw new InvalidDataException();
        return calendar.Events.Where(e => e.RecurrenceIdentifier != null).Select(e => new ExceptionInfo(Instant(e.RecurrenceIdentifier!.StartTime), e.Summary ?? series.Title, e.Status?.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) == true)).OrderBy(e => e.OriginalStart).ToArray();
    }
    public static CalendarEvent RestoreOccurrence(CalendarEvent series, DateTimeOffset original)
    {
        var calendar = Calendar.Load(series.RecurrenceIcs ?? throw new InvalidDataException()) ?? throw new InvalidDataException();
        calendar = WithoutException(calendar, original);
        return series with { RecurrenceIcs = new CalendarSerializer(calendar).SerializeToString() };
    }
    static Calendar WithoutException(Calendar calendar, DateTimeOffset original)
    {
        // Ical event equality compares UID, shared by the master and its exceptions.
        // Rebuild the collection to avoid removing the master through Remove(exception).
        var retained = calendar.Events.Where(e => e.RecurrenceIdentifier is not { } id || Instant(id.StartTime) != original).ToArray();
        var result = new Calendar();
        foreach (var item in retained) result.Events.Add(item);
        return result;
    }
    public static CalendarEvent ChangeOccurrence(CalendarOccurrence occurrence, CalendarEvent? replacement)
    {
        var series = occurrence.Series;
        var calendar = Calendar.Load(series.RecurrenceIcs ?? throw new InvalidDataException()) ?? throw new InvalidDataException();
        var master = calendar.Events.FirstOrDefault(e => e.RecurrenceIdentifier == null) ?? throw new InvalidDataException();
        var original = occurrence.OriginalStart ?? occurrence.Start;
        if (replacement != null && (replacement.End <= replacement.Start || replacement.AllDay != series.AllDay || string.IsNullOrWhiteSpace(replacement.Title))) throw new ArgumentException("La cita debe conservar el tipo y tener título y duración válidos.");
        calendar = WithoutException(calendar, original);
        calendar.Events.Add(new LibEvent
        {
            Uid = master.Uid,
            RecurrenceIdentifier = new RecurrenceIdentifier(series.AllDay ? new CalDateTime(DateOnly.FromDateTime(original.LocalDateTime)) : new CalDateTime(original.UtcDateTime).ToTimeZone(master.DtStart?.TzId ?? "UTC"), null),
            DtStart = Zoned(replacement?.Start ?? occurrence.Start, series.AllDay),
            DtEnd = Zoned(replacement?.End ?? occurrence.End, series.AllDay),
            Summary = replacement?.Title ?? occurrence.Title,
            Status = replacement == null ? "CANCELLED" : "CONFIRMED"
        });
        string raw = new CalendarSerializer(calendar).SerializeToString() ?? throw new InvalidDataException();
        if (raw.Length > 131072) throw new InvalidDataException("La serie ha alcanzado el límite de cambios individuales.");
        return series with { RecurrenceIcs = raw };
    }
    public static IReadOnlyList<string> ComponentLines(string raw)
    {
        var lines = raw.Replace("\r\n", "\n").Split('\n'); var result = new List<string>(); bool inside = false;
        foreach (var line in lines)
        { if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase)) inside = true; if (inside) result.Add(line); if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase)) inside = false; }
        return result;
    }
    public static (IReadOnlyList<CalendarEvent> Events, HashSet<string> Handled, int Omitted) ReadSeries(IReadOnlyList<string> lines)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal); var block = new List<string>(); bool inside = false;
        foreach (var line in lines)
        {
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase)) { block = []; inside = true; }
            if (inside) block.Add(line);
            if (!line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase) || !inside) continue;
            inside = false; string? uid = block.FirstOrDefault(l => l.StartsWith("UID:", StringComparison.OrdinalIgnoreCase))?[4..];
            if (uid == null) continue;
            if (!groups.TryGetValue(uid, out var combined)) groups[uid] = combined = [];
            combined.AddRange(block);
        }
        var result = new List<CalendarEvent>(); var handled = new HashSet<string>(StringComparer.Ordinal); int omitted = 0;
        foreach (var (uid, components) in groups)
        {
            if (!components.Any(l => l.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase) || l.StartsWith("RDATE", StringComparison.OrdinalIgnoreCase) || l.StartsWith("EXDATE", StringComparison.OrdinalIgnoreCase) || l.StartsWith("RECURRENCE-ID", StringComparison.OrdinalIgnoreCase))) continue;
            handled.Add(uid);
            try
            {
                string zone = TimeZoneInfo.Local.Id; if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone, out string? iana)) zone = iana;
                var normalized = components.Select(line =>
                {
                    int colon = line.IndexOf(':'); if (colon < 0) return line;
                    string key = line[..colon], name = key.Split(';')[0], value = line[(colon + 1)..];
                    int tzAt = key.IndexOf("TZID=", StringComparison.OrdinalIgnoreCase);
                    if (tzAt >= 0)
                    {
                        string tz = key[(tzAt + 5)..].Split(';')[0].Trim('"');
                        if (!TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _)) throw new FormatException("Zona horaria no reconocida.");
                        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tz, out string? mapped)) key = key.Replace(tz, mapped, StringComparison.Ordinal);
                    }
                    if ((name is "DTSTART" or "DTEND" or "RECURRENCE-ID" or "RDATE" or "EXDATE") && !key.Contains("TZID=", StringComparison.OrdinalIgnoreCase) && value.Length >= 15 && value[8] == 'T' && !value.EndsWith('Z')) return key + ";TZID=" + zone + ":" + value;
                    return key + ":" + value;
                });
                string raw = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" + string.Join("\r\n", normalized) + "\r\nEND:VCALENDAR\r\n";
                if (raw.Length > 131072) throw new InvalidDataException();
                var calendar = Calendar.Load(raw) ?? throw new InvalidDataException(); if (!Supported(calendar)) throw new InvalidDataException();
                var master = calendar.Events.FirstOrDefault(e => e.RecurrenceIdentifier == null) ?? throw new InvalidDataException();
                if (master.Status?.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) == true) throw new InvalidDataException();
                var dtStart = master.DtStart ?? throw new InvalidDataException();
                var start = Instant(dtStart); var end = Instant(master.DtEnd ?? dtStart.Add(master.EffectiveDuration));
                if (end <= start || string.IsNullOrWhiteSpace(master.Summary)) throw new InvalidDataException();
                result.Add(new(ICalendar.Id(master.Uid ?? uid), master.Summary[..Math.Min(120, master.Summary.Length)], start, end, !dtStart.HasTime, ExternalUid: master.Uid, RecurrenceIcs: raw));
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException or InvalidDataException) { omitted++; }
        }
        return (result, handled, omitted);
    }
}
