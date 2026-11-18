namespace Brisa.Core;
public static class TaskEditing
{
    public static TodoItem Change(TodoItem item, string text, DateTimeOffset? reminder, DateTimeOffset now)
    {
        text = text.Trim();
        if (text.Length is 0 or > 300) throw new ArgumentException("Escribe una tarea de entre 1 y 300 caracteres.");
        if (reminder != item.RemindAt && reminder <= now) throw new ArgumentException("El nuevo recordatorio debe ser futuro.");
        return item with { Text = text, RemindAt = reminder, ReminderSent = reminder == item.RemindAt && item.ReminderSent };
    }
    public static List<TodoItem> ArchiveCompleted(IEnumerable<TodoItem> items) => items.Select(t => t.Done && !t.Archived ? t with { Archived = true } : t).ToList();
}
