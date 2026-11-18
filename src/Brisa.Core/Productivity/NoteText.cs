namespace Brisa.Core;
public static class NoteText
{
    public static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
    public static string Title(string text)
    {
        string title = Normalize(text).Split('\n').FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() ?? "Sin titulo";
        return title[..Math.Min(title.Length, 60)];
    }
}
