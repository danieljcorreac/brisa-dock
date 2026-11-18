namespace Brisa.Core;
public static class StartupCommand
{
    public static string Build(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || executable.IndexOfAny(['"', '\r', '\n']) >= 0)
            throw new ArgumentException("El ejecutable de inicio no es valido.", nameof(executable));
        string command = "\"" + executable + "\"";
        if (command.Length > 260) throw new ArgumentException("La ruta supera el limite admitido para el inicio de Windows.", nameof(executable));
        return command;
    }
}
