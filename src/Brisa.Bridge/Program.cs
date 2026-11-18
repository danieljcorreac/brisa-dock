using System.Text.Json;
using Brisa.Core;

// Claude sends session data on stdin. Persist only quota numbers and timestamps.
try
{
    string directory = args.Length == 2 && args[0] == "--data-dir" ? args[1] : Path.Combine(AppContext.BaseDirectory, "data");
    string raw = await Console.In.ReadToEndAsync();
    using var doc = JsonDocument.Parse(raw);
    var now = DateTimeOffset.Now;
    var snapshot = UsageParser.ClaudeStatusLine(doc.RootElement, now);
    var five = snapshot.Windows.FirstOrDefault(w => w.Label == "Sesión");
    var seven = snapshot.Windows.FirstOrDefault(w => w.Label == "Semana");
    Directory.CreateDirectory(directory);
    // Unique temp file also works with multiple Claude sessions writing concurrently.
    string destination = Path.Combine(directory, "claude-usage.json");
    string temp = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
    await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(new { capturedAt = now, rate_limits = new {
        five_hour = five == null ? null : new { used_percentage = five.UsedPercent, resets_at = five.ResetsAt?.ToUnixTimeSeconds() },
        seven_day = seven == null ? null : new { used_percentage = seven.UsedPercent, resets_at = seven.ResetsAt?.ToUnixTimeSeconds() },
        model_scoped = snapshot.Windows.Where(w => w.Label == "Fable").Select(w => new { display_name = w.Label, used_percentage = w.UsedPercent, resets_at = w.ResetsAt?.ToUnixTimeSeconds() }).ToArray()
    } }));
    File.Move(temp, destination, true);
    Console.WriteLine(snapshot.Windows.Count > 0 ? string.Join(" · ", snapshot.Windows.Select(w => $"{w.Label} {w.UsedPercent:0}%")) : "Brisa · cuota pendiente");
    return 0;
}
catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
{
    Console.WriteLine("Brisa · cuota no disponible");
    return 0; // A status line must never block Claude Code.
}
