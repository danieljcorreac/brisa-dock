using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Brisa.Core;

public sealed class ClaudeDesktopCredential(string token, string identity)
{
    internal string Token { get; } = token;
    public string Identity { get; } = identity;
    public override string ToString() => "Claude Desktop session (redacted)";
}
public sealed class ClaudeDesktopSessionException(string message) : Exception(message);
public static class ClaudeDesktopSession
{
    [StructLayout(LayoutKind.Sequential)] struct Blob { public int Length; public nint Data; }
    [DllImport("crypt32.dll", SetLastError = true)] static extern bool CryptUnprotectData(ref Blob input, nint description, nint entropy, nint reserved, nint prompt, uint flags, out Blob output);
    [DllImport("kernel32.dll")] static extern nint LocalFree(nint memory);
    public static IEnumerable<string> Directories()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string moved = Path.Combine(local, "Claude-Data");
        if (Directory.Exists(moved)) foreach (var dir in Directory.EnumerateDirectories(moved).Where(d => Regex.IsMatch(Path.GetFileName(d), "^claude(_[a-z0-9]+)?$", RegexOptions.IgnoreCase))) yield return dir;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude");
        string packages = Path.Combine(local, "Packages");
        if (Directory.Exists(packages)) foreach (var dir in Directory.EnumerateDirectories(packages, "Claude_*").Where(d => Regex.IsMatch(Path.GetFileName(d), "^Claude_[a-z0-9]+$", RegexOptions.IgnoreCase))) yield return Path.Combine(dir, "LocalCache", "Roaming", "Claude");
    }
    static string ReadBounded(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (input.Length > 4 * 1024 * 1024) throw new IOException();
        using var reader = new StreamReader(input); char[] data = new char[4 * 1024 * 1024 + 1]; int count = reader.ReadBlock(data, 0, data.Length);
        if (count == data.Length) throw new IOException(); return new string(data, 0, count);
    }
    static byte[] Unprotect(byte[] encrypted)
    {
        if (!OperatingSystem.IsWindows()) throw new ClaudeDesktopSessionException("La consulta de la sesión Desktop sólo está disponible en Windows.");
        nint memory = Marshal.AllocHGlobal(encrypted.Length); var output = new Blob();
        try
        {
            Marshal.Copy(encrypted, 0, memory, encrypted.Length); var input = new Blob { Data = memory, Length = encrypted.Length };
            if (!CryptUnprotectData(ref input, 0, 0, 0, 0, 1, out output) || output.Length != 32) throw new CryptographicException();
            byte[] key = new byte[output.Length]; Marshal.Copy(output.Data, key, 0, key.Length); return key;
        }
        finally
        {
            if (output.Data != 0) { Marshal.Copy(new byte[Math.Max(0, output.Length)], 0, output.Data, Math.Max(0, output.Length)); LocalFree(output.Data); }
            Marshal.FreeHGlobal(memory);
        }
    }
    public static JsonDocument DecryptCache(string encoded, byte[] key)
    {
        byte[] data = Convert.FromBase64String(encoded); byte[] plain = new byte[Math.Max(0, data.Length - 31)];
        try
        {
            if (data.Length < 32 || !data.AsSpan(0, 3).SequenceEqual("v10"u8)) throw new CryptographicException();
            using var aes = new AesGcm(key, 16); aes.Decrypt(data.AsSpan(3, 12), data.AsSpan(15, data.Length - 31), data.AsSpan(data.Length - 16), plain);
            return JsonDocument.Parse(Encoding.UTF8.GetString(plain));
        }
        finally { CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(data); }
    }
    public static ClaudeDesktopCredential Select(string account, IEnumerable<JsonElement> caches, DateTimeOffset now)
    {
        var entries = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var cache in caches) if (cache.ValueKind == JsonValueKind.Object) foreach (var entry in cache.EnumerateObject()) entries[entry.Name] = entry.Value;
        var candidates = new List<(string Token, string Org, long Expires, bool Inference)>();
        foreach (var (name, value) in entries)
        {
            var match = Regex.Match(name, @"^acct:([^|]+)\|([^:]+):([^:]+):(https://api\.anthropic\.com):(.+)$");
            if (!match.Success || !match.Groups[1].Value.Equals(account, StringComparison.OrdinalIgnoreCase) || !match.Groups[5].Value.Split(' ').Contains("user:profile") || value.ValueKind != JsonValueKind.Object) continue;
            if (!value.TryGetProperty("token", out var token) || token.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(token.GetString()) || !value.TryGetProperty("expiresAt", out var expires) || expires.ValueKind != JsonValueKind.Number || !expires.TryGetInt64(out long at) || at <= now.AddSeconds(30).ToUnixTimeMilliseconds()) continue;
            candidates.Add((token.GetString()!, match.Groups[3].Value, at, match.Groups[5].Value.Split(' ').Contains("user:inference")));
        }
        if (candidates.Count == 0) throw new ClaudeDesktopSessionException("Abre Claude Desktop para renovar su sesión de cuenta y vuelve a actualizar.");
        if (candidates.Select(c => c.Org).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1) throw new ClaudeDesktopSessionException("Desktop tiene sesiones de varias organizaciones. No se consultará una cuenta ambigua.");
        var selected = candidates.OrderByDescending(c => c.Inference).ThenByDescending(c => c.Expires).First();
        return new(selected.Token, account + ":" + selected.Org);
    }
    public static ClaudeDesktopCredential Read()
    {
        try
        {
            foreach (var directory in Directories())
            {
                string path = Path.Combine(directory, "config.json"); if (!File.Exists(path)) continue;
                using var config = JsonDocument.Parse(ReadBounded(path)); var root = config.RootElement;
                if (!root.TryGetProperty("lastKnownAccountUuid", out var account) || account.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(account.GetString())) continue;
                if (!root.TryGetProperty("oauth:tokenCache", out _) && !root.TryGetProperty("oauth:tokenCacheV2", out _)) continue;
                using var state = JsonDocument.Parse(ReadBounded(Path.Combine(directory, "Local State")));
                byte[] protectedKey = Convert.FromBase64String(state.RootElement.GetProperty("os_crypt").GetProperty("encrypted_key").GetString() ?? "");
                if (protectedKey.Length < 6 || !protectedKey.AsSpan(0, 5).SequenceEqual("DPAPI"u8)) throw new CryptographicException();
                byte[] key = Unprotect(protectedKey[5..]); var caches = new List<JsonDocument>();
                try
                {
                    foreach (string name in new[] { "oauth:tokenCache", "oauth:tokenCacheV2" })
                        if (root.TryGetProperty(name, out var encoded) && encoded.ValueKind == JsonValueKind.String) caches.Add(DecryptCache(encoded.GetString()!, key));
                    return Select(account.GetString()!, caches.Select(c => c.RootElement), DateTimeOffset.Now);
                }
                finally { CryptographicOperations.ZeroMemory(key); foreach (var cache in caches) cache.Dispose(); }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or CryptographicException or FormatException or KeyNotFoundException or InvalidOperationException)
        { throw new ClaudeDesktopSessionException("No se pudo acceder a la sesión cifrada de Desktop. Abre Claude e inténtalo de nuevo."); }
        throw new ClaudeDesktopSessionException("No se encontró una sesión compatible de Claude Desktop.");
    }
}
