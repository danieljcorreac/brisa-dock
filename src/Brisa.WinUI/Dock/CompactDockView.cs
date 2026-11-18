using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    FrameworkElement SystemDockTile()
    {
        var columns = new Grid { Width = 100, ColumnSpacing = 8 }; columns.ColumnDefinitions.Add(new()); columns.ColumnDefinitions.Add(new());
        TextBlock Add(string label, int column)
        {
            var stack = Ui.Stack(2); var value = Ui.Text("", 18, bold: true); value.TextAlignment = TextAlignment.Center; value.HorizontalAlignment = HorizontalAlignment.Center;
            var face = new Grid { Height = 34 }; face.Children.Add(value); stack.Children.Add(face);
            var caption = Ui.Text(label, 10, Ui.MutedColor); caption.CharacterSpacing = 100; caption.Height = 14; caption.TextAlignment = TextAlignment.Center; caption.HorizontalAlignment = HorizontalAlignment.Center; stack.Children.Add(caption);
            Grid.SetColumn(stack, column); columns.Children.Add(stack); return value;
        }
        var cpu = Add("CPU", 0); var ram = Add("RAM", 1);
        updates["system"] = () => { cpu.Text = $"{stats.Cpu:0}%"; ram.Text = $"{stats.MemoryPercent:0}%"; };
        return Tile(columns, "Sistema", ShowSystem, 112);
    }
}
