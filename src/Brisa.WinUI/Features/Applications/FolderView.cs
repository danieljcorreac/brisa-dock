using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using System.IO;
using Windows.Storage.Pickers;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    int folderRequest;
    bool pickingShortcut;
    async void AddFolderShortcut()
    {
        if (pickingShortcut) return;
        pickingShortcut = true;
        try
        {
            var picker = new FolderPicker(); picker.FileTypeFilter.Add("*"); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync(); if (folder == null || closing) return;
            if (settings.Apps.Count >= 20) { ShowMessage("Dock completo", "Puedes fijar hasta 20 accesos. Retira uno antes de añadir otra carpeta."); return; }
            if (PinApplication(folder.DisplayName, folder.Path, true)) ShowAppsManager();
            else ShowMessage("Carpeta ya fijada", "Esta carpeta ya está en tus accesos. Puedes verla y organizarla en Aplicaciones y carpetas.");
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException) { if (!closing) ShowMessage("No se pudo elegir la carpeta", "Vuelve a intentarlo desde Aplicaciones y grupos."); }
        finally { pickingShortcut = false; }
    }
    async void ShowFolderStack(string path)
    {
        string page = "folder-stack-" + ++folderRequest;
        string title = Path.GetFileName(Path.TrimEndingDirectorySeparator(path)); if (string.IsNullOrWhiteSpace(title)) title = "Carpeta";
        ShowPage(page, title, "VISTA RÁPIDA DE CARPETA", Ui.Text("Leyendo la carpeta…", 13));
        bool Current() => !closing && panel?.PageId == page && panel.AppWindow.IsVisible;
        try
        {
            var listing = await Task.Run(() => FolderBrowser.Read(path), lifetime.Token).WaitAsync(TimeSpan.FromSeconds(5), lifetime.Token);
            if (!Current()) return;
            var body = Ui.Stack(12); body.Children.Add(Ui.Text(listing.Path, 11, Ui.MutedColor));
            body.Children.Add(Ui.Button("Abrir en el Explorador", "Abrir esta carpeta en el Explorador", () => Launch(listing.Path)));
            string? parent = Directory.GetParent(listing.Path)?.FullName;
            if (parent != null) body.Children.Add(Ui.Button("Subir una carpeta", "Ver la carpeta superior", () => ShowFolderStack(parent)));
            var query = new TextBox { PlaceholderText = "Filtrar esta vista…", CornerRadius = new CornerRadius(12) }; AutomationProperties.SetName(query, "Filtrar elementos de la carpeta"); body.Children.Add(query);
            var results = Ui.Stack(10); body.Children.Add(results);
            void Render()
            {
                results.Children.Clear(); var matches = listing.Entries.Where(entry => entry.Name.Contains(query.Text, StringComparison.CurrentCultureIgnoreCase)).ToArray();
                results.Children.Add(Ui.Text($"{matches.Length} " + (matches.Length == 1 ? "elemento" : "elementos") + (listing.Truncated ? " · vista parcial" : ""), 11, Ui.MutedColor));
                if (matches.Length == 0) results.Children.Add(Ui.Text(listing.Entries.Count == 0 ? "Esta carpeta está vacía." : "No hay coincidencias en esta vista.", 13, Ui.MutedColor));
                var cards = matches.Select(entry => {
                    var content = Ui.Stack(7); content.Children.Add(Ui.Glyph(entry.Directory ? "\uE8B7" : "\uE8A5", 26, entry.Directory ? Ui.Light ? "#9A6835" : "#E5BE85" : Ui.MutedColor));
                    var name = Ui.Text(entry.Name, 12, bold: true); name.MaxLines = 2; name.TextTrimming = TextTrimming.CharacterEllipsis; content.Children.Add(name);
                    var button = Ui.Button(content, "Abrir " + entry.Name, () => { if (entry.Directory) ShowFolderStack(entry.Path); else Launch(entry.Path); });
                    button.HorizontalContentAlignment = HorizontalAlignment.Stretch; return (FrameworkElement)button;
                });
                results.Children.Add(ChoiceUi.Group(2, cards));
            }
            query.TextChanged += (_, _) => Render(); Render();
            if (listing.Truncated) body.Children.Add(Ui.Text("La vista rápida muestra hasta 200 elementos. Abre el Explorador para consultar la carpeta completa.", 11, Ui.MutedColor));
            ShowPage(page, title, "VISTA RÁPIDA DE CARPETA", body);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or TimeoutException or ArgumentException)
        { if (Current()) ShowMessage("Carpeta no disponible", "La carpeta ha cambiado, no tienes acceso o la lectura está tardando demasiado. Puedes volver a intentarlo desde su acceso."); }
    }
}
