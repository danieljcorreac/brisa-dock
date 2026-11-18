using Brisa.Core;
using System.Diagnostics;
using System.Reflection;

namespace Brisa.Setup;

internal static class Program
{
    internal static Stream Payload() => Assembly.GetExecutingAssembly().GetManifestResourceStream("Brisa.Payload.zip") ?? throw new InvalidDataException("Este instalador no contiene un paquete. Prepáralo con New-ReleasePackage.ps1.");
    [STAThread]
    static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--check-payload" }))
        {
            try { using var stream = Payload(); ReleaseArchive.Verify(stream); return 0; }
            catch { return 1; }
        }
        ApplicationConfiguration.Initialize();
        if (args.SequenceEqual(new[] { "--check-layout" }))
        {
            string report = Path.Combine(Path.GetTempPath(), $"Brisa-Setup-layout-{Environment.ProcessId}.json");
            try { using var preview = new SetupWindow(["--review"]); preview.CheckLayout(); File.WriteAllText(report, "{\"Passed\":true}"); return 0; }
            catch (Exception error) { File.WriteAllText(report, System.Text.Json.JsonSerializer.Serialize(new { Passed = false, Error = error.Message })); return 1; }
        }
        Application.Run(new SetupWindow(args)); return 0;
    }
}
