using System.Reflection;
using System.Text;
using System.Text.Json;
namespace Brisa.Core;
public record BackupDocument(string Format, int Version, DateTimeOffset CreatedAt, DockSettings Settings);
public static class SettingsBackup
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public static string Export(DockSettings settings) => JsonSerializer.Serialize(new BackupDocument("BrisaDock.Backup", 1, DateTimeOffset.UtcNow, settings), new JsonSerializerOptions { WriteIndented = true });
    public static BackupDocument Read(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException("La copia supera el límite de 5 MB.");
        using var parsed = JsonDocument.Parse(json);
        RejectDuplicates(parsed.RootElement);
        var document = parsed.RootElement.Deserialize<BackupDocument>() ?? throw new InvalidDataException("La copia está vacía.");
        if (document.Format != "BrisaDock.Backup" || document.Version != 1 || document.Settings == null || document.Settings.Version > 2) throw new InvalidDataException("No es una copia compatible de Brisa.");
        document.Settings.Normalize(); return document;
    }
    static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("La copia contiene campos repetidos y no se puede interpretar con seguridad.");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) RejectDuplicates(item);
    }
    public static void Apply(DockSettings current, DockSettings incoming)
    {
        // Clone to avoid sharing mutable collections with the preview. Connections are local to this installation.
        var source = Read(Export(incoming)).Settings;
        source.CodexEnabled = current.CodexEnabled; source.CodexExecutable = current.CodexExecutable;
        source.ClaudeSource = current.ClaudeSource;
        source.ClaudeDesktopDirect = current.ClaudeDesktopDirect;
        foreach (var property in typeof(DockSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite))
            property.SetValue(current, property.GetValue(source));
    }
    public static string RestoreToStore(SettingsStore store, DockSettings current, DockSettings incoming)
    {
        // Prepare a separate candidate. A failed disk commit must not replace live data.
        var candidate = Read(Export(current)).Settings;
        Apply(candidate, incoming);
        Directory.CreateDirectory(store.DirectoryPath);
        string recovery = Path.Combine(store.DirectoryPath, "antes-de-restaurar-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        using (var output = new FileStream(recovery, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(output)) writer.Write(Export(current));
        store.Save(candidate);
        foreach (var property in typeof(DockSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite)) property.SetValue(current, property.GetValue(candidate));
        return recovery;
    }
}
