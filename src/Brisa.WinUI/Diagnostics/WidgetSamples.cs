using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Automation;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    FrameworkElement WidgetSample(WidgetDefinition definition)
    {
        // Illustrations only: never build a live widget here. Doing so would
        // replace its update handlers, attach drag targets or read accounts.
        var content = Ui.Stack(5); content.HorizontalAlignment = HorizontalAlignment.Center;
        string id = definition.Id;
        if (id is "claude" or "chatgpt")
        {
            var row = Ui.Stack(9, true); row.Children.Add(UsageLogo(id == "ai-limits" ? "claude" : id)); var text = Ui.Stack(4);
            text.Children.Add(Ui.Text(id == "ai-limits" ? "Claude · Codex" : definition.Name, 11, bold: true));
            foreach (var item in (id == "claude" ? new[] { ("5 h", 32d), ("Semana", 64d), ("Fable", 26d) } : new[] { ("5 h", 32d), ("Semana", 64d) })) { var line = Ui.Stack(5, true); line.Children.Add(Ui.Text(item.Item1, 9, Ui.MutedColor)); line.Children.Add(new QuotaMeter(item.Item2, 3, UsageInk(id, item.Item2)) { Width = 45 }); line.Children.Add(Ui.Text($"{item.Item2:0}%", 9)); text.Children.Add(line); }
            row.Children.Add(text); content.Children.Add(row);
        }
        else if (id == "ai-limits")
        {
            var table = new Grid { ColumnSpacing = 10, RowSpacing = 5 }; for (int c = 0; c < 4; c++) table.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); for (int r = 0; r < 3; r++) table.RowDefinitions.Add(new() { Height = GridLength.Auto });
            string[,] values = { { "", "5 h", "Semana", "Fable" }, { "Claude", "32%", "64%", "26%" }, { "Codex", "24%", "48%", "—" } };
            for (int r = 0; r < 3; r++) for (int c = 0; c < 4; c++) { var cell = Ui.Text(values[r, c], r == 0 ? 9 : 11, r == 0 ? Ui.MutedColor : Ui.TextColor, r > 0); Grid.SetRow(cell, r); Grid.SetColumn(cell, c); table.Children.Add(cell); } content.Children.Add(table);
        }
        else if (id == "notes")
        {
            var row = Ui.Stack(9, true); row.Children.Add(Ui.Glyph("\uE70B", 22, Ui.Accent)); var note = Ui.Stack(3); note.Children.Add(Ui.Text("Mi próxima idea", 12, bold: true)); note.Children.Add(Ui.Text("1 NOTA", 10, Ui.MutedColor)); row.Children.Add(note); content.Children.Add(row);
        }
        else if (id == "tasks")
        {
            content.Children.Add(Ui.Text("1/3", 24, bold: true)); content.Children.Add(new QuotaMeter(100d / 3, 3, Ui.Accent) { Width = 52 });
        }
        else if (id == "music")
        {
            var music = Ui.Stack(9, true); music.Children.Add(new Border { Width = 36, Height = 36, CornerRadius = new(10), Background = Ui.Brush(Ui.Light ? "#D5E9E1" : "#314E48"), Child = Ui.Glyph("\uE8D6", 20, Ui.Accent) }); var labels = Ui.Stack(3); labels.Children.Add(Ui.Text("Tu música", 13, bold: true)); labels.Children.Add(Ui.Text("Artista ·  ▶", 10, Ui.MutedColor)); music.Children.Add(labels); content.Children.Add(music);
        }
        else if (id == "apps")
        {
            var apps = Ui.Stack(8, true); foreach (var glyph in new[] { "\uE8B7", "\uE774", "\uE8EF" }) apps.Children.Add(new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Background = Ui.Brush(Ui.Surface), Child = Ui.Glyph(glyph, 22) }); content.Children.Add(apps);
        }
        else if (id is "cpu" or "ram")
        {
            var ring = new Grid { Width = 36, Height = 36 }; ring.Children.Add(new Ellipse { Stroke = Ui.Brush(Ui.Light ? "#467C6C" : "#B8E5D4"), StrokeThickness = 2.5, Opacity = .75 });
            var value = Ui.Text(id == "cpu" ? "24%" : "48%", 10, bold: true); value.TextAlignment = TextAlignment.Center; ring.Children.Add(value); content.Children.Add(ring);
        }
        else if (id is "system" or "battery" or "day-progress" or "month-progress" or "year-progress" or "storage")
        {
            string value = id switch { "cpu" => "24%", "ram" => "48%", "system" => "24%  ·  48%", "battery" => "82%", "storage" => "120 GB", "tasks" => "2/4", _ => "42,5%" };
            content.Children.Add(Ui.Text(value, 23, bold: true)); content.Children.Add(new QuotaMeter(id == "battery" ? 82 : id.EndsWith("-progress") ? 42.5 : 48, 4, Ui.Accent) { Width = Math.Max(58, definition.Width - 30) });
        }
        else if (id == "network")
        {
            content.Children.Add(Ui.Text("↓ 2,4 MB/s", 15, bold: true)); var graph = new Polyline { Width = 100, Height = 12, StrokeThickness = 1.5, Stroke = Ui.Brush(Ui.Light ? "#467C6C" : "#B8E5D4"), Points = new PointCollection { new(0, 10), new(15, 7), new(30, 9), new(45, 1), new(60, 8), new(75, 4), new(100, 6) } }; content.Children.Add(graph);
        }
        else
        {
            string? value = id switch { "timer" => "05:00", "stopwatch" => "01:24", "focus" => "25:00", "clock" or "world-time" => "10:24", "date" => "17 nov 2026", "day-number" => "17", "weather" => "18°", _ => null };
            if (value != null) content.Children.Add(Ui.Text(value, id == "date" ? 16 : 25, bold: true)); else content.Children.Add(Ui.Glyph(definition.Glyph, 27));
        }
        var caption = Ui.Text(id == "weather" ? "TU CIUDAD" : definition.Name.ToUpperInvariant(), 9, Ui.MutedColor); caption.TextAlignment = TextAlignment.Center; if (id is not "claude" and not "chatgpt" and not "ai-limits" and not "notes" and not "music") content.Children.Add(caption);
        var tile = new Border { Width = Math.Max(140, definition.Width + 16), Height = 82, CornerRadius = new CornerRadius(12), Background = Ui.Brush(Ui.Light ? "#E0EAE7" : Ui.Midnight ? "#202A39" : "#354A5C"), Child = content };
        content.VerticalAlignment = VerticalAlignment.Center;
        var preview = new Viewbox { Child = tile, Stretch = Stretch.Uniform, Height = 94, MaxWidth = 270, HorizontalAlignment = HorizontalAlignment.Stretch, IsHitTestVisible = false };
        AutomationProperties.SetName(preview, "Vista de ejemplo de " + definition.Name); return preview;
    }
}
