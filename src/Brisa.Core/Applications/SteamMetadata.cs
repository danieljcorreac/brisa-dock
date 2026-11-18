namespace Brisa.Core;
public record ValveEntry(string Key, string? Value, IReadOnlyList<ValveEntry> Children);
public static class ValveKeyValues
{
    public static IReadOnlyList<ValveEntry> Parse(string text)
    {
        if (text.Length > 1048576) throw new FormatException("Metadata too large.");
        int at = 0, nodes = 0;
        string? Token()
        {
            while (at < text.Length)
            {
                if (char.IsWhiteSpace(text[at])) { at++; continue; }
                if (text[at] == '/' && at + 1 < text.Length && text[at + 1] == '/') { while (at < text.Length && text[at] != '\n') at++; continue; }
                break;
            }
            if (at >= text.Length) return null;
            char first = text[at++]; if (first is '{' or '}') return first.ToString();
            var value = new System.Text.StringBuilder();
            if (first == '"')
            {
                while (at < text.Length)
                {
                    char c = text[at++]; if (c == '"') return value.ToString();
                    if (c == '\\' && at < text.Length && text[at] is '\\' or '"') c = text[at++];
                    value.Append(c);
                }
                throw new FormatException("Unclosed value.");
            }
            value.Append(first); while (at < text.Length && !char.IsWhiteSpace(text[at]) && text[at] is not '{' and not '}') value.Append(text[at++]); return value.ToString();
        }
        List<ValveEntry> Entries(int depth)
        {
            if (depth > 16) throw new FormatException("Metadata too deep.");
            var result = new List<ValveEntry>();
            while (true)
            {
                string? key = Token(); if (key == null) { if (depth != 0) throw new FormatException("Unclosed section."); return result; }
                if (key == "}") { if (depth == 0) throw new FormatException("Unexpected section end."); return result; }
                if (key == "{" || ++nodes > 10000) throw new FormatException("Invalid metadata.");
                string? value = Token(); if (value == null || value == "}") throw new FormatException("Missing value.");
                result.Add(value == "{" ? new(key, null, Entries(depth + 1)) : new(key, value, []));
            }
        }
        return Entries(0);
    }
}
public record SteamGameMetadata(string Id, string Name, string InstallFolder)
{
    public static SteamGameMetadata? Parse(string text)
    {
        var root = ValveKeyValues.Parse(text).FirstOrDefault(n => n.Key.Equals("AppState", StringComparison.OrdinalIgnoreCase));
        string? Read(string key) => root?.Children.FirstOrDefault(n => n.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
        string? id = Read("appid"), name = Read("name"), folder = Read("installdir");
        if (!uint.TryParse(id, out uint number) || number == 0 || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(folder) || folder is "." or ".." || folder.Any(c => c is '/' or '\\' or ':' || char.IsControl(c))) return null;
        return new(number.ToString(System.Globalization.CultureInfo.InvariantCulture), name, folder);
    }
    public static IReadOnlyList<string> Libraries(string text)
    {
        var root = ValveKeyValues.Parse(text).FirstOrDefault(n => n.Key.Equals("libraryfolders", StringComparison.OrdinalIgnoreCase));
        return (root?.Children ?? []).Where(n => uint.TryParse(n.Key, out _)).Select(n => n.Value ?? n.Children.FirstOrDefault(c => c.Key == "path")?.Value).OfType<string>().Where(Path.IsPathFullyQualified).Distinct(StringComparer.OrdinalIgnoreCase).Take(32).ToArray();
    }
}
