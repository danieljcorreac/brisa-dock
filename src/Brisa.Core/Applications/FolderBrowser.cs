namespace Brisa.Core;
public sealed record FolderEntry(string Name, string Path, bool Directory);
public sealed record FolderListing(string Path, IReadOnlyList<FolderEntry> Entries, bool Truncated);
public static class FolderBrowser
{
    public static FolderListing Read(string path, int limit = 200)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        var directory = new DirectoryInfo(path);
        var entries = directory.EnumerateFileSystemInfos().Take(limit + 1).ToArray();
        var visible = entries.Take(limit).Select(item => new FolderEntry(item.Name, item.FullName, (item.Attributes & FileAttributes.Directory) != 0)).OrderByDescending(item => item.Directory).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        return new(directory.FullName, visible, entries.Length > limit);
    }
}
