namespace Brisa.Core;
public static class AppTargets
{
    public const string AppsFolder = "shell:AppsFolder\\";
    public static string? AppId(string target)
    {
        if (!target.StartsWith(AppsFolder, StringComparison.OrdinalIgnoreCase)) return null;
        string id = target[AppsFolder.Length..];
        return id.Length is > 0 and <= 512 && !id.Any(c => char.IsControl(c) || c is '\\' or '/' or '"') ? id : null;
    }
    public static string? SteamId(string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme != "steam" || (uri.Host != "rungameid" && uri.Host != "run") || uri.UserInfo.Length != 0 || uri.Port != -1 || uri.Query.Length != 0 || uri.Fragment.Length != 0) return null;
        string id = uri.AbsolutePath.Trim('/');
        return id.Length > 0 && id.All(char.IsAsciiDigit) && uint.TryParse(id, out uint number) && number > 0 ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
    }
    public static string Identity(string target)
    {
        if (AppId(target) is string id) return "APPID:" + id.ToUpperInvariant();
        if (SteamId(target) is string game) return "STEAM:" + game;
        return target.Trim().Replace('/', '\\').ToUpperInvariant();
    }
    public static string ShortcutKey(string target, string windowsDirectory)
    {
        string key = Identity(target);
        string windows = windowsDirectory.TrimEnd('\\', '/');
        if (key == "CALC.EXE" || key == Identity(windows + "\\System32\\calc.exe") ||
            string.Equals(AppId(target), "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", StringComparison.OrdinalIgnoreCase)) return "BUILTIN:CALCULATOR";
        if (key == "EXPLORER.EXE" || key == Identity(windows + "\\explorer.exe")) return "BUILTIN:EXPLORER";
        return key;
    }
}
