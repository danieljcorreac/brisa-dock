using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
namespace Brisa.WinUI;

public sealed partial class MainWindow
{
    DateTimeOffset lastEnduranceSample;
    readonly bool endurance = Environment.GetCommandLineArgs().Contains("--check-visual") && Environment.GetCommandLineArgs().Contains("--endurance");
    void SampleEndurance()
    {
        if (!endurance || DateTimeOffset.UtcNow - lastEnduranceSample < TimeSpan.FromSeconds(10)) return;
        lastEnduranceSample = DateTimeOffset.UtcNow;
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "brisa-endurance.jsonl"), JsonSerializer.Serialize(RuntimeHealth.Capture(background.RunningCount)) + Environment.NewLine); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
    void ShowHealth()
    {
        var body = Ui.Stack(12); var values = Ui.Stack(9); body.Children.Add(Ui.Card(values));
        var cpu = Ui.Text("", 16, bold: true); var memory = Ui.Text("", 13); var resources = Ui.Text("", 12, Ui.MutedColor); var frames = Ui.Text("", 12, Ui.MutedColor); var jobs = Ui.Text("", 12, Ui.MutedColor);
        foreach (var text in new[] { cpu, memory, resources, frames, jobs }) values.Children.Add(text);
        HealthSnapshot? snapshot = null; DateTimeOffset sampled = default;
        void Update()
        {
            if (DateTimeOffset.UtcNow - sampled < TimeSpan.FromSeconds(5)) return; sampled = DateTimeOffset.UtcNow;
            snapshot = RuntimeHealth.Capture(background.RunningCount);
            cpu.Text = snapshot.CpuPercent is { } usage ? $"CPU de Brisa · {usage:0.00} %" : "CPU de Brisa · calculando…";
            memory.Text = $"Memoria privada · {snapshot.PrivateBytes / 1048576d:0.0} MB";
            resources.Text = $"{snapshot.Handles} recursos · {snapshot.GdiObjects} objetos gráficos · {snapshot.UserObjects} objetos de interfaz";
            frames.Text = $"Actualización periódica · media {snapshot.MeanTickMs:0.0} ms · máximo {snapshot.MaxTickMs:0.0} ms";
            jobs.Text = $"{snapshot.BackgroundJobs} operaciones en curso · {snapshot.BackgroundErrors} errores de fondo desde el inicio";
        }
        body.Children.Add(Ui.Text("Estas medidas corresponden sólo a Brisa. La primera lectura de CPU necesita una segunda muestra; el consumo puede variar al abrir paneles o consultar una fuente. Se actualiza cada cinco segundos mientras este panel está abierto.", 12, Ui.MutedColor));
        var status = Ui.Status(12, Ui.MutedColor); body.Children.Add(status);
        body.Children.Add(Ui.Button("Guardar diagnóstico", "Guardar medidas de Brisa sin datos de cuentas", async () =>
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker { SuggestedFileName = "Brisa-diagnostico" }; picker.FileTypeChoices.Add("Diagnóstico JSON", new List<string> { ".json" });
            try { var file = await PickForPanel(picker, () => picker.PickSaveFileAsync()); if (file == null || closing) return; var report = new { Version = typeof(MainWindow).Assembly.GetName().Version?.ToString(), Metrics = RuntimeHealth.Capture(background.RunningCount) }; await File.WriteAllTextAsync(file.Path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); status.Text = "Diagnóstico guardado. Contiene medidas de consumo y estado, sin tokens ni contenido de tus cuentas."; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { status.Text = "No se pudo guardar el diagnóstico."; }
        }));
        ShowPage("health", "Estado de Brisa", "CONSUMO · ESTABILIDAD", body); Update(); livePanelUpdate = Update;
    }
}
