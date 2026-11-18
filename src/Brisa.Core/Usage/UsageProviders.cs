using System.Diagnostics;
using System.Text.Json;

namespace Brisa.Core;

public sealed class ClaudeFileProvider(string directory) : IUsageProvider
{
    public string Id => "claude";
    public Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, "claude-usage.json");
        try
        {
            if (!File.Exists(path)) return Task.FromResult(UsageSnapshot.Empty(Id, "Conecta Claude Code para ver tu cuota."));
            using var input = JsonDocument.Parse(File.ReadAllText(path));
            var root = input.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("capturedAt", out var at) || at.ValueKind != JsonValueKind.String || !at.TryGetDateTimeOffset(out var date)) throw new JsonException();
            return Task.FromResult(UsageParser.ClaudeStatusLine(root, date).WithFreshness(DateTimeOffset.Now));
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        { return Task.FromResult(new UsageSnapshot(Id, ConnectionState.Error, "No se pudo leer la última lectura de Claude.", "Puente local", null, [])); }
    }
}

/// <summary>Only uses the documented account/rateLimits/read API; never starts an agent turn.</summary>
public sealed class CodexUsageProvider(string? configuredExecutable = null) : IUsageProvider
{
    public string Id => "chatgpt";
    public static string? FindExecutable(string? configured = null)
    {
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured) && Path.GetExtension(configured).Equals(".exe", StringComparison.OrdinalIgnoreCase)) return configured;
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try { var path = Path.Combine(entry.Trim('"'), "codex.exe"); if (File.Exists(path)) return path; } catch (ArgumentException) { }
        }
        return null;
    }
    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        var executable = FindExecutable(configuredExecutable);
        if (executable == null) return UsageSnapshot.Empty(Id, "Selecciona codex.exe o instala Codex CLI.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        using var process = new Process { StartInfo = new ProcessStartInfo(executable) { Arguments = "app-server", RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
        Task? errorDrain = null;
        try
        {
            process.Start();
            // Drain stderr without persisting diagnostics that may contain account information.
            async Task DrainErrors()
            {
                char[] discarded = new char[4096];
                while (await process.StandardError.ReadAsync(discarded.AsMemory(), timeout.Token) > 0) { }
            }
            errorDrain = DrainErrors();
            async Task Send(object value) { await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(value).AsMemory(), timeout.Token); await process.StandardInput.FlushAsync(timeout.Token); }
            async Task<JsonElement> Response(int id)
            {
                while (await process.StandardOutput.ReadLineAsync(timeout.Token) is string line)
                {
                    using var doc = JsonDocument.Parse(line); var r = doc.RootElement;
                    if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("id", out var responseId) || responseId.ValueKind != JsonValueKind.Number || !responseId.TryGetInt32(out int value) || value != id) continue;
                    if (r.TryGetProperty("error", out _)) throw new InvalidOperationException("El servicio no pudo consultar la cuota. Comprueba la sesión de Codex.");
                    if (r.TryGetProperty("result", out var result)) return result.Clone();
                }
                throw new IOException("Codex se cerró antes de responder.");
            }
            await Send(new { id = 1, method = "initialize", @params = new { clientInfo = new { name = "brisa_dock", title = "Brisa Dock", version = "0.2.0" } } });
            await Response(1);
            await Send(new { method = "initialized", @params = new { } });
            await Send(new { id = 2, method = "account/rateLimits/read" });
            var result = await Response(2);
            return UsageParser.Codex(result, DateTimeOffset.Now);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(Id, ConnectionState.Error, "Codex no respondió a tiempo. Vuelve a intentarlo.", "Codex app-server", null, []); }
        catch (Exception e) when (e is IOException or InvalidOperationException or JsonException or System.ComponentModel.Win32Exception)
        { return new(Id, ConnectionState.Error, "No se pudo consultar la cuota. Comprueba que Codex CLI tenga sesión iniciada.", "Codex app-server", null, []); }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            timeout.Cancel();
            if (errorDrain != null) try { await errorDrain; } catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException) { }
        }
    }
}
