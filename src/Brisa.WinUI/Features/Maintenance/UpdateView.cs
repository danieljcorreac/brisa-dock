using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace Brisa.WinUI;

internal record LocalPackage(string Product, int Format, string Platform, string Version, List<string> Files, Dictionary<string, string> Hashes)
{ [System.Text.Json.Serialization.JsonIgnore] public string ManifestHash { get; set; } = ""; }
public sealed partial class MainWindow
{
    bool startingMaintenance;
    bool choosingUpdate;
    static bool PayloadName(string name)
    {
        var parts = name.Replace('/', '\\').Split('\\');
        return !Path.IsPathRooted(name) && parts.All(p => p.Length > 0 && p is not "." and not ".." && !p.Any(c => c == ':' || char.IsControl(c))) &&
            !new[] { "data", "recovery", "brisa-install.json", "brisa-package.json" }.Contains(parts[0], StringComparer.OrdinalIgnoreCase) &&
            !parts[0].StartsWith("qa", StringComparison.OrdinalIgnoreCase) && !parts[0].StartsWith(".brisa", StringComparison.OrdinalIgnoreCase) &&
            !(parts[0].StartsWith("brisa-", StringComparison.OrdinalIgnoreCase) && new[] { ".json", ".jsonl", ".log" }.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase));
    }
    internal static LocalPackage VerifyLocalPackage(string directory)
    {
        string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string manifest = Path.Combine(root, "brisa-package.json");
        if (new FileInfo(manifest).Length > 4194304) throw new InvalidDataException("La lista de archivos es demasiado grande.");
        byte[] metadata = File.ReadAllBytes(manifest);
        ReadOnlySpan<byte> json = metadata;
        if (json.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) json = json[3..];
        var package = JsonSerializer.Deserialize<LocalPackage>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("No se reconoce el paquete.");
        package.ManifestHash = Convert.ToHexString(SHA256.HashData(metadata));
        if (package.Product != "Brisa Dock" || package.Format != 1 || package.Platform != "win-x64" || !Version.TryParse(package.Version, out _) || package.Files == null || package.Hashes == null || package.Files.Count is 0 or > 5000 || !package.Files.Contains("BrisaDock.exe", StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("No es un paquete compatible de Brisa.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in package.Files)
        {
            if (string.IsNullOrWhiteSpace(name) || !PayloadName(name) || !seen.Add(name.Replace('/', '\\'))) throw new InvalidDataException("El paquete contiene rutas no permitidas.");
            string file = Path.GetFullPath(Path.Combine(root, name)); if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ruta fuera del paquete.");
            for (string? scan = file; scan != null && scan.Length >= root.TrimEnd(Path.DirectorySeparatorChar).Length; scan = Path.GetDirectoryName(scan))
                if ((File.GetAttributes(scan) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("El paquete contiene enlaces de archivos o carpetas.");
            using var stream = File.OpenRead(file); string hash = Convert.ToHexString(SHA256.HashData(stream));
            if (!package.Hashes.TryGetValue(name, out string? expected) || !hash.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Uno de los archivos no coincide con la lista del paquete.");
        }
        if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest))) != package.ManifestHash) throw new InvalidDataException("El paquete cambió durante la comprobación.");
        return package;
    }
    void ShowInstallation()
    {
        var body = Ui.Stack(12); string version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "";
        body.Children.Add(Ui.Text("Brisa " + version, 22, bold: true));
        bool managed = File.Exists(Path.Combine(AppContext.BaseDirectory, "brisa-install.json")); string? previous = null;
        if (managed)
            try { using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "brisa-install.json"))); if (metadata.RootElement.TryGetProperty("PreviousVersion", out var prior)) previous = prior.GetString(); } catch (Exception e) when (e is IOException or JsonException or InvalidOperationException) { }
        body.Children.Add(Ui.Text("Revisa una actualización local antes de aplicarla. Brisa guarda una copia de la instalación anterior y conserva tus datos al actualizar o volver atrás.", 13, Ui.MutedColor));
        var status = Ui.Status(12, Ui.MutedColor); body.Children.Add(status);
        AddReleaseUpdates(body, status, managed);
        var choose = Ui.Button("Elegir actualización", "Elegir carpeta de una versión de Brisa", async () =>
        {
            if (choosingUpdate || startingMaintenance) return;
            choosingUpdate = true;
            var picker = new Windows.Storage.Pickers.FolderPicker(); picker.FileTypeFilter.Add("*"); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            try { var folder = await picker.PickSingleFolderAsync(); if (folder == null || closing) return; status.Text = "Comprobando los archivos del paquete…"; var package = await Task.Run(() => VerifyLocalPackage(folder.Path)); if (closing || panel?.PageId != "installation") return; ShowUpdatePreview(folder.Path, package); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or System.Runtime.InteropServices.COMException or ArgumentException) { status.Text = "No se pudo comprobar ese paquete. " + e.Message; }
            finally { choosingUpdate = false; }
        }); choose.IsEnabled = managed; body.Children.Add(choose);
        if (!managed) body.Children.Add(Ui.Text("Esta copia aún no tiene un registro de actualización. Puedes instalar una versión preparada de Brisa conservando tus datos.", 12, Ui.MutedColor));
        if (!string.IsNullOrWhiteSpace(previous)) body.Children.Add(Ui.Button("Recuperar Brisa " + previous, "Revisar la vuelta a Brisa " + previous, () => ShowRestorePreview(previous)));
        body.Children.Add(Ui.Button("Abrir carpeta de Brisa", "Abrir carpeta de la aplicación", () => Launch(AppContext.BaseDirectory)));
        ShowPage("installation", "Instalación y actualizaciones", "TU VERSIÓN · RECUPERACIÓN", body);
    }
    void ShowUpdatePreview(string source, LocalPackage package)
    {
        var body = Ui.Stack(12); body.Children.Add(Ui.Text("Actualizar a Brisa " + package.Version, 20, bold: true));
        body.Children.Add(Ui.Text($"{package.Files.Count} archivos comprobados. Se conservarán tus ajustes, widgets y conexiones. Brisa se cerrará y volverá a abrirse al terminar.", 13));
        var location = Ui.Text(source, 11, Ui.MutedColor); location.IsTextSelectionEnabled = true; body.Children.Add(location);
        var status = Ui.Status(12, Ui.MutedColor); body.Children.Add(status);
        body.Children.Add(Ui.Button("Actualizar y reiniciar", "Aplicar la actualización revisada y reiniciar Brisa", () => StartMaintenance("update", source, status, package.ManifestHash), true));
        body.Children.Add(Ui.Button("Volver", "Volver sin actualizar", ShowInstallation));
        ShowPage("update-preview", "Revisar actualización", "ANTES DE APLICAR", body);
    }
    void ShowRestorePreview(string version)
    {
        var body = Ui.Stack(12); body.Children.Add(Ui.Text("Recuperar Brisa " + version, 20, bold: true));
        body.Children.Add(Ui.Text("Se comprobará la copia anterior antes de cerrar Brisa. Tus datos y ajustes actuales se conservan; esta acción recupera los archivos de la aplicación.", 13));
        var status = Ui.Status(12, Ui.MutedColor); body.Children.Add(status);
        body.Children.Add(Ui.Button("Recuperar y reiniciar", "Recuperar la instalación anterior conservando los datos actuales", () => StartMaintenance("restore", null, status), true));
        body.Children.Add(Ui.Button("Volver", "Volver sin recuperar la instalación", ShowInstallation));
        ShowPage("restore-version", "Revisar recuperación", "VERSIÓN ANTERIOR", body);
    }
    async void StartMaintenance(string action, string? source, TextBlock status, string? packageHash = null)
    {
        if (startingMaintenance) return; startingMaintenance = true; Process? helper = null; bool handedOff = false;
        string id = Guid.NewGuid().ToString("N"), request = Path.Combine(AppContext.BaseDirectory, ".brisa-maintenance-" + Guid.NewGuid().ToString("N") + ".json"); string ready = Path.ChangeExtension(request, "ready");
        try
        {
            using var own = Process.GetCurrentProcess();
            File.WriteAllText(request, JsonSerializer.Serialize(new { Id = id, Action = action, Source = source, PackageHash = packageHash, Destination = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar), ProcessId = Environment.ProcessId, StartTicks = own.StartTime.ToUniversalTime().Ticks, Qa = Environment.GetCommandLineArgs().Contains("--check-visual") }));
            var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            info.Environment["PSModulePath"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "Modules");
            foreach (string arg in new[] { "-NoProfile", "-File", Path.Combine(AppContext.BaseDirectory, "maintenance", "Invoke-Maintenance.ps1"), "-RequestFile", request }) info.ArgumentList.Add(arg);
            helper = Process.Start(info) ?? throw new InvalidOperationException("No se pudo iniciar el asistente."); status.Text = "Preparando la copia y comprobando la instalación…";
            for (int i = 0; i < 300 && !helper.HasExited && !File.Exists(ready) && !closing; i++) await Task.Delay(100);
            if (File.Exists(ready) && !closing) { handedOff = true; Close(); return; }
            status.Text = "No se pudo preparar el cambio. Brisa sigue abierta y sus archivos no se han sustituido.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException) { status.Text = "No se pudo iniciar el cambio. " + e.Message; }
        finally
        {
            if (!handedOff)
                try { File.WriteAllText(Path.ChangeExtension(request, "cancel"), "cancel"); if (helper == null || helper.HasExited) { File.Delete(request); File.Delete(Path.ChangeExtension(request, "cancel")); } } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            startingMaintenance = false; helper?.Dispose();
        }
    }
    void ReadMaintenanceResult()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "brisa-maintenance-result.json");
        try
        {
            if (!File.Exists(file)) return; using var report = JsonDocument.Parse(File.ReadAllText(file));
            bool succeeded = report.RootElement.TryGetProperty("Succeeded", out var state) && state.ValueKind == JsonValueKind.True;
            string message = report.RootElement.TryGetProperty("Message", out var detail) ? detail.GetString() ?? "" : "";
            File.Move(file, Path.Combine(AppContext.BaseDirectory, "brisa-maintenance-result.previous.json"), true);
            if (!succeeded) ShowMessage("Actualización sin aplicar", "Brisa conserva la instalación disponible. " + message);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
    }
}
