using Brisa.Core;
using System.Runtime.InteropServices;
namespace Brisa.WinUI;
internal record InstalledApplication(string Name, string Target, string Source)
{ public string Identity { get; init; } = ""; }
internal static class InstalledApplications
{
    static IReadOnlyList<InstalledApplication>? cached;
    static DateTimeOffset stamp;
    public static Task<IReadOnlyList<InstalledApplication>> Read(bool force = false) => ShellWorker.Run(() =>
    {
        if (!force && cached != null && DateTimeOffset.Now - stamp < TimeSpan.FromMinutes(10)) return cached;
        var result = new List<InstalledApplication>();
        object? shell = null, folder = null, items = null;
        void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            folder = ((dynamic)shell!).NameSpace("shell:AppsFolder");
            if (folder != null)
            {
                items = ((dynamic)folder).Items(); int count = Math.Min((int)((dynamic)items).Count, 2000);
                for (int i = 0; i < count; i++)
                {
                    object? item = null;
                    try
                    {
                        item = ((dynamic)items).Item(i); string name = ((dynamic)item).Name;
                        string id = ((dynamic)item).ExtendedProperty("System.AppUserModel.ID") as string ?? "";
                        string path = ((dynamic)item).Path;
                        string target = id.Length > 0 ? AppTargets.AppsFolder + id : path;
                        if (!string.IsNullOrWhiteSpace(name) && (AppTargets.AppId(target) != null || File.Exists(target))) result.Add(new(name, target, "Windows"));
                    }
                    catch (Exception e) when (e is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
                    finally { Release(item); }
                }
            }
        }
        catch (Exception e) when (e is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
        finally { Release(items); Release(folder); Release(shell); }
        foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) })
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).Where(p => Path.GetExtension(p).ToLowerInvariant() is ".lnk" or ".url").Take(2000))
                    result.Add(new(Path.GetFileNameWithoutExtension(file), file, "Menú Inicio"));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        result.AddRange(SteamApplications.Read(force).Where(g => g.Id != "228980").Select(g => new InstalledApplication(g.Name, g.Target, "Steam")));
        // Prefer a real shortcut for Win32 applications: it preserves its arguments and working directory.
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in result.Where(a => a.Source == "Menú Inicio"))
        {
            var link = ShortcutIdentity.Describe(app.Target);
            if (link.AppId is string id) aliases.TryAdd(AppTargets.Identity(AppTargets.AppsFolder + id), AppTargets.Identity(ShortcutIdentity.Resolve(app.Target)));
        }
        string Identity(InstalledApplication app) { string key = AppTargets.Identity(ShortcutIdentity.Resolve(app.Target)); return aliases.GetValueOrDefault(key, key); }
        cached = result.OrderBy(a => a.Source == "Menú Inicio" ? 0 : 1).Select(app => app with { Identity = Identity(app) }).DistinctBy(app => app.Identity, StringComparer.OrdinalIgnoreCase).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        stamp = DateTimeOffset.Now; return cached;
    });
}
