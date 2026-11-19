using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Brisa.Core;

public sealed record GitHubUpdate(string Version, long AssetId, long Size, string Sha256, string Notes)
{
    public const string Repository = "danieljcorreac/brisa-dock";
    public string ReleaseUrl => $"https://github.com/{Repository}/releases/tag/v{Version}";
    public static GitHubUpdate? Parse(string json, Version current)
    {
        try { return ParseMetadata(json, current); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { throw new InvalidDataException("La publicación de GitHub no tiene un formato válido.", error); }
    }
    static GitHubUpdate? ParseMetadata(string json, Version current)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+$") || !System.Version.TryParse(tag[1..], out var version)) throw new InvalidDataException("La versión publicada no es válida.");
        if (version <= new Version(current.Major, current.Minor, Math.Max(0, current.Build))) return null;
        string name = $"Brisa-{tag[1..]}-win-x64.zip";
        var assets = root.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == name).ToArray();
        if (assets.Length != 1) throw new InvalidDataException("La versión no tiene un paquete único para Windows x64.");
        var asset = assets[0];
        long id = asset.GetProperty("id").GetInt64(), size = asset.GetProperty("size").GetInt64();
        string digest = asset.TryGetProperty("digest", out var hash) ? hash.GetString() ?? "" : "";
        if (id <= 0 || size is <= 0 or > 268435456 || !Regex.IsMatch(digest, @"^sha256:[a-fA-F0-9]{64}$") || asset.GetProperty("state").GetString() != "uploaded") throw new InvalidDataException("El paquete no tiene un tamaño o una huella verificable.");
        string notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        return new(tag[1..], id, size, digest[7..], notes.Length > 12000 ? notes[..12000] : notes);
    }
    public void Verify(Stream archive)
    {
        if (!archive.CanSeek || archive.Length != Size) throw new InvalidDataException("La descarga está incompleta.");
        archive.Position = 0;
        if (!Convert.ToHexString(SHA256.HashData(archive)).Equals(Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("La huella de la descarga no coincide con GitHub.");
        archive.Position = 0;
        if (ReleaseArchive.Verify(archive).Version != Version) throw new InvalidDataException("La versión del paquete no coincide con la publicación.");
        archive.Position = 0;
    }
}

// Public releases use anonymous HTTPS only; no CLI, account or credential discovery.
public sealed class GitHubUpdates : IDisposable
{
    readonly HttpClient http;
    readonly bool ownsHttp;
    public GitHubUpdates(HttpClient? client = null)
    {
        http = client ?? new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan }; ownsHttp = client == null;
    }
    public async Task<GitHubUpdate?> CheckAsync(Version current, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(30));
        using var output = new MemoryStream();
        try { await FetchAsync($"repos/{GitHubUpdate.Repository}/releases/latest", false, output, 2097152, deadline.Token); }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.NotFound) { throw new IOException("No hay una versión pública disponible. Puedes volver a intentarlo más tarde."); }
        return GitHubUpdate.Parse(System.Text.Encoding.UTF8.GetString(output.ToArray()), current);
    }
    public async Task<string> DownloadAsync(GitHubUpdate update, string cacheRoot, CancellationToken token)
    {
        string destination = Path.Combine(Path.GetFullPath(cacheRoot), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destination);
        string zip = Path.Combine(destination, "download.zip"), package = Path.Combine(destination, "package");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            using (var output = new FileStream(zip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await FetchAsync($"repos/{GitHubUpdate.Repository}/releases/assets/{update.AssetId}", true, output, update.Size, deadline.Token);
            await Task.Run(() =>
            {
                deadline.Token.ThrowIfCancellationRequested();
                using var input = File.OpenRead(zip); update.Verify(input);
                ReleaseArchive.Extract(input, package);
                deadline.Token.ThrowIfCancellationRequested();
            }, deadline.Token);
            File.Delete(zip); return package;
        }
        catch
        {
            // This unique directory was created above and contains no existing user data.
            try { Directory.Delete(destination, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }
    async Task FetchAsync(string endpoint, bool binary, Stream output, long maximum, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/" + endpoint);
        request.Headers.UserAgent.ParseAdd("BrisaDock/1.0"); request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(binary ? "application/octet-stream" : "application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.Forbidden && response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Contains("0"))
            throw new IOException("GitHub ha limitado temporalmente las consultas. Espera un poco antes de volver a intentarlo.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maximum) throw new InvalidDataException("La descarga supera su tamaño permitido.");
        using var input = await response.Content.ReadAsStreamAsync(token); await CopyBounded(input, output, maximum, token);
    }
    static async Task CopyBounded(Stream input, Stream output, long maximum, CancellationToken token)
    {
        byte[] buffer = new byte[81920]; long size = 0; int count;
        while ((count = await input.ReadAsync(buffer, token)) != 0)
        {
            size += count; if (size > maximum) throw new InvalidDataException("La descarga supera su tamaño permitido.");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
    }
    public void Dispose() { if (ownsHttp) http.Dispose(); }
}
