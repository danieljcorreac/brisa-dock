using Brisa.Core;
using Microsoft.Win32;
namespace Brisa.WinUI;
internal static class StartupRegistration
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "BrisaDock";
    static string Command => StartupCommand.Build(Environment.ProcessPath ?? throw new InvalidOperationException("No se encontro el ejecutable de Brisa."));
    public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(Key); return string.Equals(key?.GetValue(Name) as string, Command, StringComparison.OrdinalIgnoreCase); } }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key, true);
        if (enabled) key.SetValue(Name, Command, RegistryValueKind.String); else key.DeleteValue(Name, false);
    }
}
