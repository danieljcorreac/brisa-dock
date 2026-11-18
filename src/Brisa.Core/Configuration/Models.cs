using System.Text.Json;

namespace Brisa.Core;

public record AppShortcut(string Id, string Name, string Target, bool Folder = false);
public record TodoItem(string Id, string Text, bool Done = false, DateTimeOffset? RemindAt = null, bool ReminderSent = false, bool Archived = false);
public record WidgetDefinition(string Id, string Name, string Category, string Description, string Glyph, double Width);
public static class WidgetCatalog
{
    public static readonly WidgetDefinition[] All = [
        new("apps", "Aplicaciones", "Escritorio", "Accesos a aplicaciones, archivos y carpetas, y tus ventanas abiertas.", "\uE8A5", 220),
        new("claude", "Claude", "Inteligencia artificial", "Cuota del plan desde Claude Desktop o Claude Code.", "\uE8D4", 226),
        new("chatgpt", "ChatGPT · Codex", "Inteligencia artificial", "Límites del plan a través del conector oficial de Codex.", "\uE99A", 226),
        new("system", "Sistema", "Escritorio", "CPU y memoria de tu PC, en tiempo real.", "\uE9D9", 112),
        new("battery", "Batería", "Escritorio", "Carga y alimentación del equipo.", "\uE83F", 80),
        new("sound", "Sonido", "Escritorio", "Volumen y silencio del dispositivo de salida.", "\uE767", 62),
        new("clock", "Hoy", "Tu día", "Hora, fecha y calendario mensual.", "\uE787", 114),
        new("tasks", "Tareas", "Tu día", "Anota lo pendiente y márcalo al terminar.", "\uE73A", 80),
        new("notes", "Notas", "Tu día", "Una nota rápida que se guarda en este equipo.", "\uE70B", 66),
        new("focus", "Foco", "Tu día", "Temporizador de 25 minutos, con pausa y reinicio.", "\uE916", 84),
        new("music", "Reproducción", "Tu día", "Título y controles de la sesión multimedia de Windows.", "\uE8D6", 164)
        ,new("weather", "Tiempo", "Tu día", "Temperatura y previsión del día en la ciudad que elijas.", "\uE753", 96),
        new("ai-limits", "Límites de IA", "Inteligencia artificial", "Claude y Codex juntos, con cuota y reinicios.", "\uE99A", 276),
        new("timer", "Temporizador", "Tu día", "Horas, minutos y segundos, pausa y duraciones favoritas.", "\uE916", 92),
        new("stopwatch", "Cronómetro", "Tu día", "Tiempo transcurrido y vueltas.", "\uE768", 96),
        new("day-progress", "Progreso del día", "Tiempo y relojes", "El día transcurrido, con porcentaje y barra.", "\uE787", 100),
        new("month-progress", "Progreso del mes", "Tiempo y relojes", "El mes transcurrido, incluidos días y horas.", "\uE787", 100),
        new("year-progress", "Progreso del año", "Tiempo y relojes", "El año transcurrido, respetando años bisiestos.", "\uE787", 100),
        new("date", "Fecha", "Tiempo y relojes", "Fecha completa y acceso al calendario.", "\uE787", 128),
        new("day-number", "Día del mes", "Tiempo y relojes", "El día como una pequeña hoja de calendario.", "\uE787", 72),
        new("world-time", "Hora mundial", "Tiempo y relojes", "Una zona horaria elegida, con su diferencia horaria.", "\uE774", 120),
        new("storage", "Almacenamiento", "Escritorio", "Espacio libre y ocupado de la unidad elegida.", "\uEDA2", 110),
        new("network", "Red", "Escritorio", "Descarga y subida agregadas de interfaces activas.", "\uE839", 120),
        new("search", "Buscar", "Aplicaciones y archivos", "Busca en la web o abre una ruta de carpeta.", "\uE721", 62),
        new("recycle-bin", "Papelera", "Aplicaciones y archivos", "Elementos y tamaño de la papelera de Windows.", "\uE74D", 72),
        new("keyboard", "Teclado en pantalla", "Escritorio", "Abre o cierra el teclado en pantalla de Windows.", "\uE765", 62),
        new("cpu", "CPU", "Escritorio", "Carga de CPU con indicador circular.", "\uE9D9", 76),
        new("ram", "RAM", "Escritorio", "Memoria en uso con indicador circular.", "\uE950", 76)
    ];
    public static WidgetDefinition? Find(string id) => All.FirstOrDefault(w => w.Id == id);
}
public class DockSettings
{
    public int Version { get; set; } = 2;
    public string Theme { get; set; } = "glass";
    public string Material { get; set; } = "auto";
    public double TintStrength { get; set; } = .3;
    public string Edge { get; set; } = "bottom";
    public string? MonitorId { get; set; }
    public double Scale { get; set; } = 1;
    public bool AlwaysOnTop { get; set; } = true;
    public bool ReducedMotion { get; set; }
    public bool TrayIcon { get; set; } = true;
    public bool GlobalShortcuts { get; set; }
    public bool CheckForUpdates { get; set; } = true;
    public DateTimeOffset? LastUpdateCheck { get; set; }
    public string? NotifiedUpdateVersion { get; set; }
    public bool AutoHide { get; set; }
    public bool ReserveSpace { get; set; }
    public bool FollowSystemAccessibility { get; set; } = true;
    public bool HideInFullscreen { get; set; } = true;
    public bool KeepOnDesktop { get; set; } = true;
    public bool DockLocked { get; set; }
    public bool Clock12Hour { get; set; }
    public bool ClockSeconds { get; set; }
    public double WidgetSpacing { get; set; } = 4;
    public double DockPadding { get; set; } = 12;
    public double EdgeDistance { get; set; } = 12;
    public int WidthPercent { get; set; }
    public double Magnification { get; set; } = 1.18;
    public int IconSize { get; set; } = 30;
    public bool ShowRunningApps { get; set; } = true;
    public bool AppCards { get; set; }
    public bool HoverNames { get; set; } = true;
    public string WorldTimeZone { get; set; } = "UTC";
    public int TimerSeconds { get; set; } = 300;
    public List<int> TimerPresets { get; set; } = [300, 600, 1500];
    public bool TimerSound { get; set; } = true;
    public string? StorageDrive { get; set; }
    public bool CodexEnabled { get; set; }
    public string ClaudeSource { get; set; } = "code";
    public bool ClaudeDesktopDirect { get; set; }
    public string? CodexExecutable { get; set; }
    public List<string> Widgets { get; set; } = ["apps", "claude", "chatgpt", "system", "battery", "sound", "tasks", "clock"];
    public List<AppShortcut> Apps { get; set; } = [new("files", "Archivos", "explorer.exe"), new("browser", "Navegador", "https://www.bing.com"), new("calculator", "Calculadora", "calc.exe")];
    public List<AppGroup> AppGroups { get; set; } = [];
    public List<string> PinnedOrder { get; set; } = [];
    public List<TodoItem> Tasks { get; set; } = [];
    public List<CalendarEvent> Events { get; set; } = [];
    public List<string> CalendarReminderKeys { get; set; } = [];
    public FocusOptions Focus { get; set; } = new();
    public FocusCheckpoint? FocusState { get; set; }
    public double? TimerRemainingSeconds { get; set; }
    public bool QuietHours { get; set; }
    public int QuietStart { get; set; } = 22;
    public int QuietEnd { get; set; } = 8;
    public List<NoteItem> Notes { get; set; } = [];
    public string Note { get; set; } = "";
    public WeatherLocation? WeatherLocation { get; set; }
    public WeatherCache? WeatherCache { get; set; }
    public List<QuotaSample> QuotaHistory { get; set; } = [];
    public bool QuotaAlerts { get; set; }
    public int QuotaAlertPercent { get; set; } = 85;
    public List<string> QuotaAlerted { get; set; } = [];
    public void Normalize()
    {
        ClaudeSource = ClaudeSource is "code" or "desktop" or "off" ? ClaudeSource : "code";
        Theme = Theme is "glass" or "midnight" or "light" ? Theme : "glass";
        Material = Material is "solid" or "translucent" ? Material : Theme == "midnight" ? "solid" : "translucent";
        TintStrength = double.IsFinite(TintStrength) ? Math.Clamp(TintStrength, .1, .8) : .3;
        Edge = Edge == "top" ? "top" : "bottom";
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, .75, 1.25) : 1;
        WidgetSpacing = double.IsFinite(WidgetSpacing) ? Math.Clamp(WidgetSpacing, 0, 24) : 4;
        DockPadding = double.IsFinite(DockPadding) ? Math.Clamp(DockPadding, 4, 24) : 12;
        EdgeDistance = double.IsFinite(EdgeDistance) ? Math.Clamp(EdgeDistance, 0, 48) : 12;
        Magnification = double.IsFinite(Magnification) ? Math.Clamp(Magnification, 1, 1.65) : 1.18;
        WidthPercent = WidthPercent is 0 or 50 or 75 or 100 ? WidthPercent : 0;
        IconSize = IconSize is 30 or 38 or 46 ? IconSize : 30;
        TimerSeconds = Math.Clamp(TimerSeconds, 1, 359999);
        TimerPresets = (TimerPresets ?? []).Where(v => v > 0 && v <= 359999).Distinct().Take(8).ToList();
        WorldTimeZone ??= "UTC";
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(WorldTimeZone, out _)) WorldTimeZone = "UTC";
        Widgets = (Widgets ?? []).Where(id => WidgetCatalog.Find(id) != null).Distinct().ToList();
        Apps = (Apps ?? []).Where(a => a != null && !string.IsNullOrWhiteSpace(a.Target)).DistinctBy(a => a.Id).Take(20).ToList();
        AppGroups = Brisa.Core.AppGroups.Normalize(AppGroups, Apps);
        PinnedOrder = PinnedLayout.Normalize(this);
        QuotaHistory = (QuotaHistory ?? []).Where(s => s != null && s.Provider is "claude" or "chatgpt" && double.IsFinite(s.UsedPercent) && s.UsedPercent is >= 0 and <= 100).TakeLast(500).ToList();
        QuotaAlertPercent = Math.Clamp(QuotaAlertPercent, 50, 100); QuotaAlerted = (QuotaAlerted ?? []).Where(k => k != null).Distinct().TakeLast(100).ToList();
        Tasks = (Tasks ?? []).Where(t => t != null && !string.IsNullOrWhiteSpace(t.Text)).ToList();
        Events = CalendarEvents.Normalize(Events);
        CalendarReminderKeys = (CalendarReminderKeys ?? []).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().TakeLast(2000).ToList();
        Note ??= "";
        Focus = (Focus ?? new()).Normalize(); QuietStart = Math.Clamp(QuietStart, 0, 23); QuietEnd = Math.Clamp(QuietEnd, 0, 23);
        if (TimerRemainingSeconds is double remaining && (!double.IsFinite(remaining) || remaining < 0 || remaining > 359999)) TimerRemainingSeconds = null;
        Notes = (Notes ?? []).Where(n => n != null && !string.IsNullOrWhiteSpace(n.Id)).DistinctBy(n => n.Id).Take(100).Select(n => n with { Text = n.Text ?? "" }).ToList();
        if (Notes.Count == 0 && !string.IsNullOrEmpty(Note)) Notes.Add(new("legacy-note", Note, DateTimeOffset.Now));
        if (WeatherLocation != null && (!double.IsFinite(WeatherLocation.Latitude) || !double.IsFinite(WeatherLocation.Longitude) || Math.Abs(WeatherLocation.Latitude) > 90 || Math.Abs(WeatherLocation.Longitude) > 180)) WeatherLocation = null;
        if (WeatherCache != null && (WeatherCache.Location == null || WeatherCache.Reading == null || WeatherCache.Location != WeatherLocation || !double.IsFinite(WeatherCache.Reading.Temperature))) WeatherCache = null;
    }
}
public class SettingsStore(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public string? Warning { get; private set; }
    public DockSettings Load()
    {
        var path = Path.Combine(DirectoryPath, "dock.json");
        try { if (File.Exists(path)) { var value = JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(path)) ?? throw new JsonException(); value.Normalize(); return value; } }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            Warning = "Los ajustes guardados no se pudieron leer. Se conservará una copia antes de guardar cambios.";
            try
            {
                var previous = JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(path + ".previous"));
                if (previous != null) { previous.Normalize(); Warning = "Se recuperó la última copia de tus ajustes. El archivo dañado se conservará antes de guardar."; return previous; }
            }
            catch (Exception fallback) when (fallback is IOException or JsonException or UnauthorizedAccessException) { }
        }
        return new();
    }
    public void Save(DockSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, "dock.json");
        string serialized = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        if (File.Exists(path))
        {
            if (Warning == null && File.ReadAllText(path) == serialized) return;
            if (Warning != null) File.Copy(path, path + ".backup-" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..8]);
            else File.Copy(path, path + ".previous", true);
        }
        var temp = path + ".tmp";
        File.WriteAllText(temp, serialized);
        File.Move(temp, path, true);
        Warning = null;
    }
}
public record QuotaWindow(string Label, double UsedPercent, DateTimeOffset? ResetsAt, int? DurationMinutes = null)
{
    public bool Expired(DateTimeOffset now) => ResetsAt.HasValue && ResetsAt.Value <= now;
    // The provider may return 23:59:59.999 for a midnight boundary. Display
    // minutes rounded up, keeping the exact instant for expiry and alert identity.
    public DateTimeOffset? DisplayReset => ResetsAt is { } reset && reset.Ticks % TimeSpan.TicksPerMinute is long remainder && remainder != 0 && reset.Ticks <= DateTimeOffset.MaxValue.Ticks - (TimeSpan.TicksPerMinute - remainder) ? reset.AddTicks(TimeSpan.TicksPerMinute - remainder) : ResetsAt;
}
public enum ConnectionState { Disconnected, Loading, Connected, Stale, Unavailable, Error }
public record UsageSnapshot(string ProviderId, ConnectionState State, string Message, string Source, DateTimeOffset? UpdatedAt, IReadOnlyList<QuotaWindow> Windows)
{
    public bool IsPreviousReading => Windows.Count > 0 && State is ConnectionState.Stale or ConnectionState.Error;
    public static UsageSnapshot Empty(string id, string message) => new(id, ConnectionState.Disconnected, message, "", null, []);
    public UsageSnapshot WithFreshness(DateTimeOffset now)
    {
        if (State != ConnectionState.Connected) return this;
        if (!UpdatedAt.HasValue || now - UpdatedAt.Value > TimeSpan.FromMinutes(15) || Windows.Any(w => w.Expired(now)))
            return this with { State = ConnectionState.Stale, Message = "Última lectura · pendiente de actualizar" };
        return this;
    }
}
public interface IUsageProvider
{
    string Id { get; }
    Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken);
}
public static class UsageParser
{
    public static UsageSnapshot ClaudeDirect(JsonElement input, DateTimeOffset now)
    {
        List<QuotaWindow> windows = [];
        if (input.ValueKind == JsonValueKind.Object)
        {
            DateTimeOffset? Reset(JsonElement row)
            {
                if (row.TryGetProperty("resets_at", out var at) && at.ValueKind == JsonValueKind.String && at.TryGetDateTimeOffset(out var date)) return date;
                return Unix(row, "resets_at");
            }
            foreach (var (key, label, minutes) in new[] { ("five_hour", "Sesión", 300), ("seven_day", "Semana", 10080) })
                if (input.TryGetProperty(key, out var row) && row.ValueKind == JsonValueKind.Object && Percentage(row, "utilization") is double p) windows.Add(new(label, p, Reset(row), minutes));
            if (input.TryGetProperty("limits", out var limits) && limits.ValueKind == JsonValueKind.Array)
                foreach (var row in limits.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String || Percentage(row, "percent") is not double p) continue;
                    string? label = kind.GetString() switch { "session" => "Sesión", "weekly_all" => "Semana", _ => null };
                    if (kind.GetString() == "weekly_scoped" && row.TryGetProperty("scope", out var scope) && scope.ValueKind == JsonValueKind.Object && scope.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.Object && model.TryGetProperty("display_name", out var name) && name.ValueKind == JsonValueKind.String && name.GetString()?.Contains("Fable", StringComparison.OrdinalIgnoreCase) == true) label = "Fable";
                    if (label != null && !windows.Any(w => w.Label == label)) windows.Add(new(label, p, Reset(row), label == "Sesión" ? 300 : 10080));
                }
        }
        return new UsageSnapshot("claude", windows.Count > 0 ? ConnectionState.Connected : ConnectionState.Unavailable, windows.Count > 0 ? "Cuotas de tu cuenta de Claude · consulta directa" : "Claude no ha facilitado porcentajes de cuota.", "Claude Desktop · consulta directa", now, windows.OrderBy(w => w.Label == "Sesión" ? 0 : w.Label == "Semana" ? 1 : 2).ToArray()).WithFreshness(now);
    }
    static DateTimeOffset? Unix(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var seconds)) return null;
        try { return DateTimeOffset.FromUnixTimeSeconds(seconds); } catch (ArgumentOutOfRangeException) { return null; }
    }
    static double? Percentage(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetDouble(out double p) || !double.IsFinite(p) || p < 0 || p > 100) return null;
        return p;
    }
    public static UsageSnapshot Codex(JsonElement result, DateTimeOffset now)
    {
        if (result.ValueKind != JsonValueKind.Object) return new("chatgpt", ConnectionState.Unavailable, "Respuesta de cuota no disponible.", "Codex app-server", now, []);
        JsonElement bucket = default;
        if (result.TryGetProperty("rateLimitsByLimitId", out var buckets) && buckets.ValueKind == JsonValueKind.Object && buckets.TryGetProperty("codex", out var codex)) bucket = codex;
        else if (result.TryGetProperty("rateLimits", out var single) && single.ValueKind == JsonValueKind.Object) bucket = single;
        if (bucket.ValueKind != JsonValueKind.Object) return new("chatgpt", ConnectionState.Unavailable, "El servicio no ha devuelto límites para este plan.", "Codex app-server", now, []);
        List<QuotaWindow> windows = [];
        foreach (var key in new[] { "primary", "secondary" })
        {
            if (!bucket.TryGetProperty(key, out var w) || w.ValueKind != JsonValueKind.Object || Percentage(w, "usedPercent") is not double used) continue;
            int? minutes = w.TryGetProperty("windowDurationMins", out var duration) && duration.ValueKind == JsonValueKind.Number && duration.TryGetInt32(out int d) ? d : null;
            string label = minutes switch { >= 10080 => "Semana", >= 1440 => "Día", >= 60 => $"{minutes / 60} h", > 0 => $"{minutes} min", _ => key == "primary" ? "Sesión" : "Otra cuota" };
            windows.Add(new(label, used, Unix(w, "resetsAt"), minutes));
        }
        return new UsageSnapshot("chatgpt", windows.Count > 0 ? ConnectionState.Connected : ConnectionState.Unavailable, windows.Count > 0 ? "Límites del plan · Codex" : "Sin ventanas de cuota disponibles", "Codex app-server", now, windows).WithFreshness(now);
    }
    public static UsageSnapshot ClaudeStatusLine(JsonElement input, DateTimeOffset now)
    {
        List<QuotaWindow> windows = [];
        if (input.ValueKind == JsonValueKind.Object && input.TryGetProperty("rate_limits", out var limits) && limits.ValueKind == JsonValueKind.Object)
        {
            foreach (var (key, name, minutes) in new[] { ("five_hour", "Sesión", 300), ("seven_day", "Semana", 10080) })
                if (limits.TryGetProperty(key, out var w) && w.ValueKind == JsonValueKind.Object && Percentage(w, "used_percentage") is double used)
                    windows.Add(new(name, used, Unix(w, "resets_at"), minutes));
            if (limits.TryGetProperty("model_scoped", out var models) && models.ValueKind == JsonValueKind.Array)
                foreach (var row in models.EnumerateArray())
                    if (row.ValueKind == JsonValueKind.Object && row.TryGetProperty("display_name", out var name) && name.ValueKind == JsonValueKind.String && name.GetString()?.Contains("Fable", StringComparison.OrdinalIgnoreCase) == true && Percentage(row, "used_percentage") is double p && !windows.Any(w => w.Label == "Fable")) windows.Add(new("Fable", p, Unix(row, "resets_at"), 10080));
        }
        return new UsageSnapshot("claude", windows.Count > 0 ? ConnectionState.Connected : ConnectionState.Unavailable,
            windows.Count > 0 ? "Cuota del plan de Claude" : "Claude Code aún no ha facilitado límites.", "Claude Code · statusLine", now, windows).WithFreshness(now);
    }
}
