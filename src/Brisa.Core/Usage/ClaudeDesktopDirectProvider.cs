using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
namespace Brisa.Core;

public sealed class ClaudeDesktopDirectProvider : IUsageProvider
{
    public string Id => "claude";
    readonly HttpClient client;
    readonly Func<ClaudeDesktopCredential> credential;
    readonly Func<DateTimeOffset> clock;
    readonly SemaphoreSlim gate = new(1, 1);
    DateTimeOffset next;
    public DateTimeOffset NextAttemptAt => next;
    UsageSnapshot? previous;
    string? identity;
    public ClaudeDesktopDirectProvider(HttpClient? client = null, Func<ClaudeDesktopCredential>? credential = null, Func<DateTimeOffset>? clock = null)
    {
        this.client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        this.credential = credential ?? ClaudeDesktopSession.Read; this.clock = clock ?? (() => DateTimeOffset.Now);
    }
    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            DateTimeOffset now = clock();
            if (now < next && previous != null) return previous.WithFreshness(now);
            next = now.AddMinutes(5);
            try
            {
                var session = credential();
                if (identity != session.Identity) { previous = null; identity = session.Identity; }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(15));
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
                request.Headers.Add("anthropic-beta", "oauth-2025-04-20"); request.Headers.Accept.Add(new("application/json"));
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    string message = response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized => "La sesión de Desktop ha caducado. Abre Claude para renovarla.",
                        HttpStatusCode.Forbidden => "Claude no permite consultar las cuotas con esta sesión de Desktop.",
                        HttpStatusCode.TooManyRequests => "Claude ha limitado las consultas. Se reintentará más tarde.",
                        _ => "No se pudo consultar la cuota de Claude. Se reintentará más tarde."
                    };
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var retry = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - now) ?? TimeSpan.FromMinutes(15);
                        var wait = retry < TimeSpan.FromMinutes(15) ? TimeSpan.FromMinutes(15) : retry;
                        var utcNow = now.ToUniversalTime();
                        next = wait >= DateTimeOffset.MaxValue - utcNow ? DateTimeOffset.MaxValue : utcNow.Add(wait);
                    }
                    return previous = Failure(message, response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden));
                }
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token); using var data = new MemoryStream(); byte[] buffer = new byte[8192]; int count;
                while ((count = await stream.ReadAsync(buffer, timeout.Token)) > 0) { if (data.Length + count > 1024 * 1024) throw new IOException(); data.Write(buffer, 0, count); }
                using var body = JsonDocument.Parse(data.ToArray());
                return previous = UsageParser.ClaudeDirect(body.RootElement, now);
            }
            catch (ClaudeDesktopSessionException e) { return previous = Failure(e.Message); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return previous = Failure("Claude no respondió a tiempo. Se reintentará más tarde.", true); }
            catch (Exception e) when (e is IOException or JsonException or HttpRequestException)
            { return previous = Failure("No se pudo consultar la cuota de Claude. Comprueba la conexión y vuelve a intentarlo.", true); }
        }
        finally { gate.Release(); }
    }
    UsageSnapshot Failure(string message, bool retainReading = false) =>
        retainReading && previous is { Windows.Count: > 0 } reading
            ? reading with { State = ConnectionState.Error, Message = message + " Se conserva la última lectura, sin actualizar." }
            : new("claude", ConnectionState.Error, message, "Claude Desktop · consulta directa", null, []);
}
