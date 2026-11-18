using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Brisa.Core;

public sealed record ReleaseArchiveInfo(string Version, int FileCount, string ManifestSha256);

public static class ReleaseArchive
{
    const long MaxExpandedBytes = 1024L * 1024 * 1024;
    const long MaxEntryBytes = 256L * 1024 * 1024;
    sealed record Manifest(string Product, int Format, string Platform, string Version, List<string> Files, Dictionary<string, string> Hashes);

    static string Name(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("El paquete contiene una ruta vacía.");
        string path = value.Replace('\\', '/');
        var parts = path.Split('/');
        if (string.IsNullOrWhiteSpace(value) || path.StartsWith('/') || parts.Any(p => p.Length == 0 || p is "." or ".." || p != p.TrimEnd(' ', '.') || p.Any(c => char.IsControl(c) || "<>:\"|?*".Contains(c)) || Regex.IsMatch(p, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase))) throw new InvalidDataException("El paquete contiene una ruta no válida.");
        if (parts[0].Equals("data", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("recovery", StringComparison.OrdinalIgnoreCase) || parts[0].StartsWith("qa", StringComparison.OrdinalIgnoreCase) || parts.Any(p => p.StartsWith('.')) || new[] { ".pfx", ".p12", ".pem", ".key", ".snk" }.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) || path.EndsWith(".key", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) || new[] { "brisa-install.json", "dock.json", "auth.json", "credentials.json", "claude-usage.json" }.Contains(parts[^1], StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("El paquete contiene datos o archivos no distribuibles.");
        return path;
    }
    static void UniqueJson(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in value.EnumerateObject()) { if (!names.Add(item.Name)) throw new InvalidDataException("El manifiesto contiene campos repetidos."); UniqueJson(item.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) UniqueJson(item);
    }
    static string Hash(ZipArchiveEntry entry)
    {
        using var input = entry.Open(); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920]; long total = 0; int read;
        while ((read = input.Read(buffer)) > 0) { total += read; if (total > entry.Length || total > MaxEntryBytes) throw new InvalidDataException("Un archivo supera el tamaño declarado."); hash.AppendData(buffer, 0, read); }
        if (total != entry.Length) throw new InvalidDataException("El paquete está incompleto.");
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static ReleaseArchiveInfo Verify(Stream input)
    {
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count is 0 or > 5001) throw new InvalidDataException("El paquete contiene demasiados archivos o está vacío.");
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase); long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & 0x400) != 0) throw new InvalidDataException("El paquete contiene enlaces.");
            string name = Name(entry.FullName);
            expanded += entry.Length;
            if (entry.Length > MaxEntryBytes || expanded > MaxExpandedBytes || !entries.TryAdd(name, entry)) throw new InvalidDataException("El paquete supera sus límites o repite rutas.");
        }
        if (!entries.TryGetValue("brisa-package.json", out var metadata) || metadata.Length > 4194304) throw new InvalidDataException("No se encontró un manifiesto válido.");
        using var manifestStream = metadata.Open(); using var manifestBytes = new MemoryStream();
        byte[] metadataBuffer = new byte[81920]; int metadataRead;
        while ((metadataRead = manifestStream.Read(metadataBuffer)) > 0)
        {
            if (manifestBytes.Length + metadataRead > metadata.Length || manifestBytes.Length + metadataRead > 4194304) throw new InvalidDataException("El manifiesto supera su tamaño declarado.");
            manifestBytes.Write(metadataBuffer, 0, metadataRead);
        }
        if (manifestBytes.Length != metadata.Length) throw new InvalidDataException("El manifiesto está incompleto.");
        using var document = JsonDocument.Parse(manifestBytes.ToArray()); UniqueJson(document.RootElement);
        var manifest = document.RootElement.Deserialize<Manifest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("No se reconoce el manifiesto.");
        if (manifest.Product != "Brisa Dock" || manifest.Format != 1 || manifest.Platform != "win-x64" || !Version.TryParse(manifest.Version, out _) || manifest.Files is not { Count: > 0 and <= 5000 } || manifest.Hashes == null) throw new InvalidDataException("El paquete no es compatible.");
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in manifest.Files)
        {
            string name = Name(file);
            if (name.Equals("brisa-package.json", StringComparison.OrdinalIgnoreCase) || !expected.Add(name) || !entries.TryGetValue(name, out var entry) || !manifest.Hashes.TryGetValue(file, out string? hash) || hash == null || !Regex.IsMatch(hash, "^[0-9a-fA-F]{64}$") || !Hash(entry).Equals(hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Los archivos no coinciden con el manifiesto.");
        }
        if (!expected.Contains("BrisaDock.exe") || !expected.Contains("maintenance/Install-Brisa.ps1") || expected.Count + 1 != entries.Count) throw new InvalidDataException("Faltan archivos obligatorios o hay archivos ajenos al manifiesto.");
        return new(manifest.Version, expected.Count, Hash(metadata));
    }
    public static ReleaseArchiveInfo Extract(Stream input, string destination)
    {
        if (!input.CanSeek) throw new ArgumentException("La extracción requiere un flujo reposicionable.", nameof(input));
        input.Position = 0;
        var result = Verify(input);
        string root = Path.GetFullPath(destination);
        if (Directory.Exists(root) || File.Exists(root)) throw new IOException("La carpeta de extracción debe ser nueva.");
        for (string? parent = Path.GetDirectoryName(root); parent != null; parent = Path.GetDirectoryName(parent))
            if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) throw new IOException("La ruta de extracción contiene un enlace.");
        Directory.CreateDirectory(root); root = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        input.Position = 0;
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            string output = Path.GetFullPath(Path.Combine(root, Name(entry.FullName).Replace('/', Path.DirectorySeparatorChar)));
            if (!output.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ruta fuera de la carpeta temporal.");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using var target = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None); using var source = entry.Open(); source.CopyTo(target);
        }
        return result;
    }
}
