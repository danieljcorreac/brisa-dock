using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    FrameworkElement AggregateUsageTile()
    {
        var grid = new Grid { Width = 252, ColumnSpacing = 6, RowSpacing = 3 };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(48) }); for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new());
        for (int i = 0; i < 3; i++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        for (int i = 0; i < 3; i++) { var label = Ui.Text(new[] { "5 h", "Semana", "Fable" }[i], 9, Ui.MutedColor); label.TextAlignment = TextAlignment.Center; Grid.SetColumn(label, i + 1); grid.Children.Add(label); }
        var numbers = new Dictionary<string, TextBlock[]>(); int row = 1;
        foreach (var (id, name) in new[] { ("claude", "Claude"), ("chatgpt", "Codex") })
        {
            var title = Ui.Text(name, 11, Ui.TextColor, true); Grid.SetRow(title, row); grid.Children.Add(title);
            numbers[id] = Enumerable.Range(0, 3).Select(column => { var number = Ui.Text("—", 11, Ui.MutedColor, true); number.TextAlignment = TextAlignment.Center; Grid.SetRow(number, row); Grid.SetColumn(number, column + 1); grid.Children.Add(number); return number; }).ToArray(); row++;
        }
        updates["ai-limits"] = () => {
            foreach (var (id, cells) in numbers)
            {
                var snapshot = usage[id].WithFreshness(DateTimeOffset.Now);
                var windows = new[] { snapshot.Windows.FirstOrDefault(w => w.DurationMinutes == 300), snapshot.Windows.FirstOrDefault(w => QuotaShortLabel(w) == "Semana"), snapshot.Windows.FirstOrDefault(w => w.Label == "Fable") };
                for (int i = 0; i < cells.Length; i++) { var window = windows[i]; cells[i].Text = window == null ? "—" : $"{window.UsedPercent:0}%" + (snapshot.IsPreviousReading ? "*" : ""); cells[i].Foreground = Ui.Brush(window == null || snapshot.IsPreviousReading ? Ui.MutedColor : UsageInk(id, window.UsedPercent)); }
            }
        };
        return Tile(grid, "Límites de Claude y Codex", ShowAggregateUsage, 276);
    }
    void ShowAggregateUsage()
    {
        var body = Ui.Stack(12);
        foreach (var (id, name) in new[] { ("claude", "Claude"), ("chatgpt", "ChatGPT · Codex") })
        {
            var snapshot = usage[id].WithFreshness(DateTimeOffset.Now); var content = Ui.Stack(8); content.Children.Add(Ui.Text(name, 20, bold: true));
            content.Children.Add(Ui.Text(snapshot.Message, 12, Ui.MutedColor));
            foreach (var w in snapshot.Windows)
            {
                content.Children.Add(Ui.Text($"{QuotaShortLabel(w)} · {w.UsedPercent:0}% utilizado", 13)); content.Children.Add(new QuotaMeter(w.UsedPercent, 4, snapshot.IsPreviousReading ? Ui.MutedColor : UsageInk(id, w.UsedPercent)));
                if (w.ResetsAt.HasValue) content.Children.Add(Ui.Text("Reinicio: " + w.DisplayReset!.Value.LocalDateTime.ToString("ddd d MMM HH:mm", System.Globalization.CultureInfo.GetCultureInfo("es-ES")), 11, Ui.MutedColor));
            }
            if (snapshot.UpdatedAt.HasValue) content.Children.Add(Ui.Text($"{snapshot.Source}\nLectura: {snapshot.UpdatedAt.Value.LocalDateTime:dd/MM HH:mm}" + (snapshot.IsPreviousReading ? " · ANTIGUA" : ""), 10, Ui.MutedColor));
            content.Children.Add(Ui.Button("Conexión y detalle", "Configurar " + name, () => ShowUsage(id))); body.Children.Add(Ui.Card(content));
        }
        body.Children.Add(Ui.Button("Actualizar ambos", "Actualizar límites de IA", () => _ = RefreshUsageAsync(true), true));
        ShowPage("ai-limits", "Tus límites de IA", "DOS PROVEEDORES · DATOS Y FUENTES SEPARADOS", body);
    }
}
