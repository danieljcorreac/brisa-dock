using Brisa.Core;
using System.IO;
using Windows.Storage;
using Windows.Storage.Pickers;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    async void ImportCalendar()
    {
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".ics"); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        try
        {
            var file = await picker.PickSingleFileAsync(); if (file == null || closing) return;
            if ((await file.GetBasicPropertiesAsync()).Size > ICalendar.MaxBytes) throw new InvalidDataException();
            var import = ICalendar.Parse(await FileIO.ReadTextAsync(file)); if (closing) return;
            var body = Ui.Stack(12); body.Children.Add(Ui.Text(file.Name, 18, bold: true));
            int updates = import.Events.Count(e => settings.Events.Any(c => c.Id == e.Id));
            body.Children.Add(Ui.Text($"{import.Events.Count - updates} eventos nuevos · {updates} actualizaciones\n{import.Omitted} eventos omitidos", 14));
            body.Children.Add(Ui.Text("Se admiten eventos individuales, días completos y series diarias, semanales, mensuales o anuales, con excepciones. Se omiten las reglas no compatibles. Los archivos no activan recordatorios nuevos. Importar otra vez el mismo UID actualiza la serie sin duplicarla.", 12, Ui.MutedColor));
            foreach (var item in import.Events.Take(5)) body.Children.Add(Ui.Text($"{item.Start.LocalDateTime:dd/MM/yyyy HH:mm} · {item.Title}", 12));
            var apply = Ui.Button("Importar estos eventos", "Confirmar importación de calendario", () =>
            {
                try
                {
                    var result = ICalendar.Merge(settings.Events, import.Events); Save();
                    ShowMessage("Calendario importado", $"{result.Added} eventos añadidos y {result.Updated} actualizados. Los eventos existentes conservan su archivo y preferencias de aviso.");
                }
                catch (InvalidDataException e) { ShowMessage("No se pudo importar", e.Message); }
            }, true); apply.IsEnabled = import.Events.Count > 0; body.Children.Add(apply);
            body.Children.Add(Ui.Button("Cancelar", "Cancelar importación", ShowCalendar)); ShowPage("calendar-import", "Revisar calendario", "IMPORTACIÓN LOCAL", body);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { ShowMessage("Calendario no válido", "Elige un archivo .ics completo de hasta 5 MB. No se modificaron tus eventos."); }
    }
    async void ExportCalendar()
    {
        var picker = new FileSavePicker { SuggestedFileName = "Brisa-calendario-" + DateTime.Now.ToString("yyyyMMdd"), SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeChoices.Add("Calendario iCalendar", new List<string> { ".ics" }); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        try
        {
            var file = await picker.PickSaveFileAsync(); if (file == null || closing) return;
            await FileIO.WriteTextAsync(file, ICalendar.Export(settings.Events)); ShowMessage("Calendario exportado", "Se guardaron los eventos activos en " + file.Name + ". Los archivados y las preferencias de aviso no se exportan.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { ShowMessage("No se pudo exportar", "Elige una carpeta que permita guardar archivos."); }
    }
}
