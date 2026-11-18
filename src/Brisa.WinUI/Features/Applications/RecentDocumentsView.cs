using Brisa.Core;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    int recentRequest;
    async void ShowRecentDocuments(string name, string? shortcut, AppWindowInfo? window)
    {
        int request = ++recentRequest;
        ShowPage("recent-documents", name, "ARCHIVOS RECIENTES DE WINDOWS", Ui.Text("Consultando los documentos facilitados por Windows…", 13));
        if (window != null && !DesktopWindows.MatchesWindow(window)) window = null;
        var result = await Task.Run(() => RecentDocuments.Read(window == null ? 0 : (nint)window.Handle, shortcut));
        if (closing || request != recentRequest || panel?.PageId != "recent-documents" || !panel.AppWindow.IsVisible) return;
        var body = Ui.Stack(12);
        body.Children.Add(Ui.Text("Documentos recientes y frecuentes facilitados por Windows. No se modifica el historial ni se guardan rutas en Brisa.", 12, Ui.MutedColor));
        if (!result.Identified) body.Children.Add(Ui.Text("Windows no facilita una identidad de aplicación para consultar sus documentos recientes. Prueba con su acceso .lnk original o con la aplicación abierta.", 14));
        else if (result.Items.Count == 0) body.Children.Add(Ui.Text("Windows no ha facilitado documentos para esta aplicación. La lista puede estar vacía o desactivada en sus ajustes.", 14));
        foreach (var group in result.Items.GroupBy(item => item.Frequent))
        {
            body.Children.Add(Ui.Text(group.Key ? "FRECUENTES" : "RECIENTES", 11, Ui.MutedColor, true));
            foreach (var item in group)
            {
                var content = Ui.Stack(4); content.Children.Add(Ui.Text(item.Name, 15, bold: true)); content.Children.Add(Ui.Text(item.Path, 11, Ui.MutedColor));
                body.Children.Add(Ui.Button(content, "Abrir documento reciente " + item.Name, () => Launch(item.Path)));
            }
        }
        ShowPage("recent-documents", name, "ARCHIVOS RECIENTES DE WINDOWS", body);
    }
}
