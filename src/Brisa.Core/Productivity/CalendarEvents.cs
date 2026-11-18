namespace Brisa.Core;
public record CalendarEvent(string Id, string Title, DateTimeOffset Start, DateTimeOffset End, bool AllDay = false, bool Archived = false, bool Reminder = false, bool ReminderSent = false, string? ExternalUid = null, string? RecurrenceIcs = null);
public static class CalendarEvents
{
    public static bool OccursOn(CalendarEvent item, DateTime date) => item.RecurrenceIcs == null ? !item.Archived && item.Start.LocalDateTime.Date <= date.Date && item.End.LocalDateTime > date.Date : CalendarSchedule.Between([item], new DateTimeOffset(date.Date), new DateTimeOffset(date.Date.AddDays(1))).Count > 0;
    public static bool ReminderDue(CalendarEvent item, DateTimeOffset now) => !item.Archived && item.Reminder && !item.ReminderSent && now >= item.Start.AddMinutes(-15) && now < item.End;
    public static List<CalendarEvent> Normalize(IEnumerable<CalendarEvent>? items) => (items ?? []).Where(e => e != null && !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Title) && e.End > e.Start).DistinctBy(e => e.Id).Take(1000).Select(e => e with { Title = e.Title.Trim()[..Math.Min(e.Title.Trim().Length, 120)] }).ToList();
}
