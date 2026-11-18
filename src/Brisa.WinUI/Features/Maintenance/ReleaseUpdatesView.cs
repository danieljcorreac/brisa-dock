using Brisa.Core;
using Microsoft.UI.Xaml.Controls;

namespace Brisa.WinUI;

public sealed partial class MainWindow
{
    bool checkingRelease, downloadingRelease;
    GitHubUpdate? availableRelease;
    string? preparedUpdateSource, preparedUpdateVersion;
    readonly DateTimeOffset updateStartup = DateTimeOffset.UtcNow;
    void PollReleaseUpdates()
    {
        if (!settings.CheckForUpdates || checkingRelease || closing || DateTimeOffset.UtcNow - updateStartup < TimeSpan.FromSeconds(30) ||
            Environment.GetCommandLineArgs().Contains("--check-visual") || !File.Exists(Path.Combine(AppContext.BaseDirectory, "brisa-install.json"))) return;
        var elapsed = DateTimeOffset.UtcNow - (settings.LastUpdateCheck ?? DateTimeOffset.MinValue);
        if (elapsed >= TimeSpan.Zero && elapsed < TimeSpan.FromDays(1)) return;
        _ = CheckReleaseUpdatesAsync(null);
    }
    void AddReleaseUpdates(StackPanel body, TextBlock status, bool managed)
    {
        body.Children.Add(Ui.Text("ACTUALIZACIONES DE GITHUB", 11, Ui.MutedColor, true));
        var automatic = new ToggleSwitch { Header = "Buscar nuevas versiones una vez al día", IsOn = settings.CheckForUpdates };
        automatic.Toggled += (_, _) => { settings.CheckForUpdates = automatic.IsOn; Save(); }; body.Children.Add(automatic);
        body.Children.Add(Ui.Text("Brisa avisa si hay una versión estable y verifica la descarga antes de aplicarla. Para acceder al repositorio privado utiliza tu sesión de GitHub CLI; Brisa no guarda su token.", 12, Ui.MutedColor));
        var check = Ui.Button(checkingRelease ? "Comprobando…" : "Buscar actualizaciones", "Consultar la última versión estable de Brisa", async () =>
        {
            await CheckReleaseUpdatesAsync(status);
            if (!closing && panel?.PageId == "installation" && availableRelease != null) ShowInstallation();
        });
        check.IsEnabled = !checkingRelease && !downloadingRelease; body.Children.Add(check);
        if (availableRelease is { } release)
        {
            body.Children.Add(Ui.Text("Disponible: Brisa " + release.Version, 16, bold: true));
            body.Children.Add(Ui.Button("Ver notas de la versión", "Abrir la publicación oficial en GitHub", () => Launch(release.ReleaseUrl)));
            var download = Ui.Button(downloadingRelease ? "Descargando y comprobando…" : "Descargar y revisar", "Descargar el paquete verificado sin sustituir la instalación todavía", async () =>
            {
                if (downloadingRelease || startingMaintenance) return;
                downloadingRelease = true; status.Text = "Descargando y comprobando el paquete de GitHub…";
                try
                {
                    using var client = new GitHubUpdates();
                    string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrisaDock", "updates");
                    string source = preparedUpdateVersion == release.Version && preparedUpdateSource != null && Directory.Exists(preparedUpdateSource)
                        ? preparedUpdateSource : await client.DownloadAsync(release, cache, lifetime.Token);
                    var package = await Task.Run(() => VerifyLocalPackage(source), lifetime.Token);
                    preparedUpdateSource = source; preparedUpdateVersion = release.Version;
                    if (!closing && panel?.PageId == "installation") ShowUpdatePreview(source, package);
                }
                catch (OperationCanceledException) { if (!closing) status.Text = "La descarga se ha detenido o ha tardado demasiado. Puedes volver a intentarlo."; }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException or System.Text.Json.JsonException or InvalidOperationException or System.ComponentModel.Win32Exception)
                { if (!closing) status.Text = "No se pudo preparar la actualización. " + error.Message; }
                finally { downloadingRelease = false; }
            }, true);
            download.IsEnabled = managed && !downloadingRelease && !startingMaintenance; body.Children.Add(download);
        }
    }
    async Task CheckReleaseUpdatesAsync(TextBlock? status)
    {
        if (checkingRelease || closing) return;
        checkingRelease = true; settings.LastUpdateCheck = DateTimeOffset.UtcNow; Save();
        availableRelease = null;
        if (status != null) status.Text = "Buscando la última versión estable…";
        try
        {
            using var client = new GitHubUpdates();
            availableRelease = await client.CheckAsync(typeof(MainWindow).Assembly.GetName().Version!, lifetime.Token);
            if (closing) return;
            if (status != null) status.Text = availableRelease == null ? "Tienes la última versión estable disponible." : "Hay una nueva versión lista para revisar.";
            if (availableRelease is { } release && settings.NotifiedUpdateVersion != release.Version && windowsIntegration?.TrayActive == true)
                if (windowsIntegration.Notify("Brisa " + release.Version + " disponible", "Abre Ajustes → Instalación y actualizaciones para descargarla y revisar los cambios."))
                { settings.NotifiedUpdateVersion = release.Version; Save(); }
        }
        catch (OperationCanceledException) { if (!closing && status != null) status.Text = "La consulta ha tardado demasiado. Puedes volver a intentarlo."; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException or System.Text.Json.JsonException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { if (!closing && status != null) status.Text = "No se pudo buscar una versión. " + error.Message; }
        finally { checkingRelease = false; }
    }
}
