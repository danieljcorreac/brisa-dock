using System.Text.Json;
namespace Brisa.Core;

/// <summary>Reads only Desktop's quota history; never opens session stores or credentials.</summary>
public sealed class ClaudeDesktopProvider(string? historyPath = null) : IUsageProvider
{
    public const int MaxBytes = 4 * 1024 * 1024;
    public string Id => "claude";
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "plan-usage-history.json");
    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            string path = historyPath ?? DefaultPath;
            if (!File.Exists(path)) return Empty("Abre Claude Desktop e inicia sesión. Todavía no se encuentra su historial local de cuotas.");
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, true);
            if (input.Length > MaxBytes) return Empty("El historial de Claude Desktop supera el tamaño admitido.");
            using var output = new MemoryStream(); var buffer = new byte[8192];
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (output.Length + read > MaxBytes) return Empty("El historial de Claude Desktop supera el tamaño admitido.");
                output.Write(buffer, 0, read);
            }
            using var doc = JsonDocument.Parse(output.ToArray());
            return Parse(doc.RootElement, DateTimeOffset.Now);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { return new("claude", ConnectionState.Error, "No se pudo leer el historial de Claude Desktop. Puede estar actualizándose; vuelve a comprobarlo.", "Claude Desktop · historial local", null, []); }
    }
    static UsageSnapshot Empty(string message) => new("claude", ConnectionState.Unavailable, message, "Claude Desktop · historial local", null, []);
    public static UsageSnapshot Parse(JsonElement root, DateTimeOffset now)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int v) || v != 2 || !root.TryGetProperty("samples", out var samples) || samples.ValueKind != JsonValueKind.Array)
            return Empty("El formato del historial de Claude Desktop no es compatible con esta versión de Brisa.");
        if (samples.GetArrayLength() > 20000) return Empty("El historial de Claude Desktop tiene demasiadas muestras.");
        string? organization = null; JsonElement latest = default; DateTimeOffset? latestAt = null;
        foreach (var sample in samples.EnumerateArray())
        {
            if (sample.ValueKind != JsonValueKind.Object || !sample.TryGetProperty("org", out var org) || org.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(org.GetString())) continue;
            string identity = org.GetString()!;
            if (organization != null && organization != identity) return Empty("El historial contiene varias cuentas u organizaciones. Usa Claude Code para evitar mezclar sus cuotas.");
            organization = identity;
            if (!sample.TryGetProperty("t", out var time) || time.ValueKind != JsonValueKind.Number || !time.TryGetInt64(out long ms)) continue;
            DateTimeOffset at;
            try { at = DateTimeOffset.FromUnixTimeMilliseconds(ms); } catch (ArgumentOutOfRangeException) { continue; }
            if (at > now.AddMinutes(2) || (latestAt.HasValue && at <= latestAt.Value)) continue;
            latestAt = at; latest = sample;
        }
        if (latestAt == null || !latest.TryGetProperty("u", out var usage) || usage.ValueKind != JsonValueKind.Object) return Empty("Claude Desktop todavía no ha guardado una lectura de cuotas válida.");
        List<QuotaWindow> windows = [];
        foreach (var (key, label, minutes) in new[] { ("fh", "Sesión", 300), ("sd", "Semana", 10080) })
            if (usage.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double p) && double.IsFinite(p) && p is >= 0 and <= 100) windows.Add(new(label, p, null, minutes));
        return new UsageSnapshot("claude", windows.Count > 0 ? ConnectionState.Connected : ConnectionState.Unavailable,
            windows.Count > 0 ? "Cuota guardada por Claude Desktop. Las fechas de reinicio no están disponibles en este historial." : "La última lectura de Claude Desktop no contiene porcentajes válidos.",
            "Claude Desktop · historial local", latestAt, windows).WithFreshness(now);
    }
}
