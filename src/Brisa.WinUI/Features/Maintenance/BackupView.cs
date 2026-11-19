using Brisa.Core;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using System.Text.Json;
using Windows.Storage;
using Windows.Storage.Pickers;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    void ShowBackupSettings()
    {
        var body = Ui.Stack(14);
        body.Children.Add(Ui.Text("Guarda una copia de tus widgets, accesos, notas, tareas, eventos y preferencias. Puedes revisarla antes de restaurar.", 14));
        body.Children.Add(Ui.Button("Exportar copia…", "Exportar copia de seguridad de Brisa", ExportBackup, true));
        body.Children.Add(Ui.Button("Restaurar desde una copia…", "Elegir copia para restaurar", PreviewBackup));
        body.Children.Add(Ui.Button("Recuperar una restauración anterior", "Revisar las copias guardadas antes de restaurar", ShowRecoveryBackups));
        body.Children.Add(Ui.Text("La restauración conserva las conexiones locales de Claude y Codex, pausa Foco y el temporizador y guarda una copia del estado anterior. El inicio automático de Windows se configura por separado.", 12, Ui.MutedColor));
        ShowPage("backups", "Tus copias", "DATOS · PREFERENCIAS · RECUPERACIÓN", body);
    }
    async void ExportBackup()
    {
        var picker = new FileSavePicker { SuggestedFileName = "Brisa-copia-" + DateTime.Now.ToString("yyyyMMdd-HHmm"), SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeChoices.Add("Copia de Brisa", new List<string> { ".json" }); try
        {
            var file = await PickForPanel(picker, () => picker.PickSaveFileAsync()); if (file == null || closing) return;
            SaveSessionState(); await FileIO.WriteTextAsync(file, SettingsBackup.Export(settings));
            ShowMessage("Copia guardada", "Se ha guardado " + file.Name + ".");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { ShowMessage("No se pudo exportar", "Elige una carpeta que permita guardar archivos."); }
    }
    async void PreviewBackup()
    {
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".json"); try
        {
            var file = await PickForPanel(picker, () => picker.PickSingleFileAsync()); if (file == null || closing) return;
            if ((await file.GetBasicPropertiesAsync()).Size > SettingsBackup.MaxBytes) throw new InvalidDataException("La copia supera el límite de 5 MB.");
            var backup = SettingsBackup.Read(await FileIO.ReadTextAsync(file)); if (closing) return;
            ShowBackupPreview(backup, file.Name);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or System.Runtime.InteropServices.COMException) { ShowMessage("Copia no válida", "No se pudo leer una copia compatible de Brisa. Tus datos actuales se conservan."); }
    }
    void ShowBackupPreview(BackupDocument backup, string name)
    {
            var source = backup.Settings; var body = Ui.Stack(12);
            body.Children.Add(Ui.Text(name, 18, bold: true)); body.Children.Add(Ui.Text("Creada el " + backup.CreatedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm"), 12, Ui.MutedColor));
            body.Children.Add(Ui.Text($"{Ui.Count(source.Widgets.Count, "widget", "widgets")} · {Ui.Count(source.Apps.Count, "acceso", "accesos")} · {Ui.Count(source.Tasks.Count, "tarea", "tareas")}\n{Ui.Count(source.Notes.Count, "nota", "notas")} · {Ui.Count(source.Events.Count, "evento", "eventos")}", 14));
            body.Children.Add(Ui.Text("Color: " + (source.Theme == "glass" ? "Azulado" : source.Theme == "light" ? "Claro" : "Medianoche") + " · " + (source.Material == "solid" ? "Sólido" : "Translúcido") + "\nCiudad: " + (source.WeatherLocation?.Name ?? "Sin configurar"), 12, Ui.MutedColor));
            body.Children.Add(Ui.Text("Se sustituirán los datos actuales por los de esta copia. Antes se guardará el estado actual para poder recuperarlo. Las conexiones locales se conservan.", 13));
            body.Children.Add(Ui.Button("Restaurar esta copia", "Aplicar la copia revisada", () => RestoreBackup(source), true));
            body.Children.Add(Ui.Button("Cancelar", "Cancelar restauración", ShowBackupSettings));
            ShowPage("backup-preview", "Revisar copia", "ANTES DE RESTAURAR", body);
    }
    void ShowRecoveryBackups()
    {
        var body = Ui.Stack(12);
        body.Children.Add(Ui.Text("Cada restauración guarda el estado anterior. Revisa una copia antes de recuperarla; también se guardará el estado actual.", 13));
        try
        {
            var files = Directory.Exists(store.DirectoryPath) ? new DirectoryInfo(store.DirectoryPath).GetFiles("antes-de-restaurar-*.json").OrderByDescending(f => f.LastWriteTimeUtc).Take(20).ToArray() : [];
            if (files.Length == 0) body.Children.Add(Ui.Text("Todavía no hay restauraciones anteriores.", 13, Ui.MutedColor));
            foreach (var file in files)
                body.Children.Add(Ui.Button(file.LastWriteTime.ToString("dd/MM/yyyy HH:mm:ss") + " · Revisar", "Revisar copia anterior " + file.Name, () =>
                {
                    try { if (file.Length > SettingsBackup.MaxBytes) throw new InvalidDataException(); ShowBackupPreview(SettingsBackup.Read(File.ReadAllText(file.FullName)), file.Name); }
                    catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { ShowMessage("Copia no válida", "No se pudo leer esta copia. Tus datos se conservan."); }
                }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { body.Children.Add(Ui.Text("No se pudieron consultar las copias de recuperación.", 13)); }
        body.Children.Add(Ui.Button("Volver", "Volver a copias de seguridad", ShowBackupSettings)); ShowPage("backup-recovery", "Recuperar datos", "ESTADOS ANTERIORES", body);
    }
    void RestoreBackup(DockSettings source)
    {
        bool committed = false;
        try
        {
            SaveSessionState();
            SettingsBackup.RestoreToStore(store, settings, source);
            committed = true;
            countdown.Set(TimeSpan.FromSeconds(settings.TimerRemainingSeconds ?? settings.TimerSeconds)); focusSession = new(settings.Focus); focusSession.Restore(settings.FocusState);
            weather = settings.WeatherCache?.Reading; lastWeatherPoll = DateTimeOffset.MinValue;
            ApplyPreferences(); SetSurface(); Native.Style(this, settings.AlwaysOnTop);
            windowsIntegration?.SetTray(settings.TrayIcon); if (windowsIntegration?.SetHotkeys(settings.GlobalShortcuts) != true) settings.GlobalShortcuts = false;
            editingDock = false; settings.PinnedOrder = PinnedLayout.Normalize(settings); store.Save(settings); BuildDock();
            ShowMessage("Copia restaurada", "Tus datos y preferencias se han restaurado. Se guardó una copia del estado anterior en la carpeta data de Brisa.");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { ShowMessage("Revisar restauración", committed ? "La copia se guardó, pero no se pudo completar el reajuste de Brisa. Reinicia la aplicación; también puedes recuperar el estado anterior desde Copias." : "No se pudo completar la restauración. Tus datos anteriores se conservan; revisa el acceso a la carpeta data."); }
    }
}
