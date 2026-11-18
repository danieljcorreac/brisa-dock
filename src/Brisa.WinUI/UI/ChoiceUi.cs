using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
namespace Brisa.WinUI;
internal static class ChoiceUi
{
    public static Grid Group(int columns, IEnumerable<FrameworkElement> items)
    {
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        for (int c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new());
        int index = 0;
        foreach (var item in items) { if (index % columns == 0) grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); Grid.SetColumn(item, index % columns); Grid.SetRow(item, index / columns); grid.Children.Add(item); index++; }
        return grid;
    }
    public static Button Card(string title, string description, FrameworkElement preview, bool selected, Action action)
    {
        var content = Ui.Stack(7); content.Children.Add(preview);
        var label = Ui.Text((selected ? "✓ " : "") + title, 12, bold: selected); label.TextAlignment = TextAlignment.Center; content.Children.Add(label);
        if (!string.IsNullOrWhiteSpace(description)) { var detail = Ui.Text(description, 10, Ui.MutedColor); detail.TextAlignment = TextAlignment.Center; content.Children.Add(detail); }
        var button = Ui.Button(content, title + (selected ? ", seleccionado" : ""), action);
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.Padding = new Thickness(7, 9, 7, 9); button.BorderThickness = new Thickness(2);
        button.BorderBrush = Ui.Brush(selected ? Ui.Light ? "#376F66" : "#B9E9D6" : Ui.Light ? "#16000000" : "#16FFFFFF");
        AutomationProperties.SetHelpText(button, description); ToolTipService.SetToolTip(button, null); return button;
    }
    public static FrameworkElement Screen(bool top = false, double barWidth = .7, bool? solid = null, string? color = null, string? number = null, double aspect = 1.8)
    {
        var frame = new Grid { Width = 132, Height = 82 };
        double height = Math.Clamp(114 / aspect, 24, 63);
        var scene = new Grid { Width = 114, Height = height, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(2), BorderBrush = Ui.Brush(Ui.Light ? "#60778B" : "#748194"), VerticalAlignment = VerticalAlignment.Center,
            Background = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1), GradientStops = { new GradientStop { Color = Ui.Color("#627DA0"), Offset = 0 }, new GradientStop { Color = Ui.Color("#B48E98"), Offset = .55 }, new GradientStop { Color = Ui.Color("#A6C0A6"), Offset = 1 } } } };
        scene.Children.Add(new Border { Background = Ui.Brush("#40FFFFFF"), CornerRadius = new CornerRadius(3), Width = 35, Height = Math.Min(16, height / 3), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
        var mini = Ui.Stack(4, true); mini.HorizontalAlignment = HorizontalAlignment.Center;
        foreach (string ink in new[] { "#E2B797", "#B0D6C5", "#BAC9E8" }) mini.Children.Add(new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(2), Background = Ui.Brush(ink) });
        scene.Children.Add(new Border { Width = 104 * barWidth, Height = Math.Min(15, height / 3), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, top ? 4 : 0, 0, top ? 0 : 4), VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom, Background = Ui.Brush((solid ?? !Ui.Translucent ? "#FF" : "#88") + (color ?? Ui.BaseColor)[1..]), Child = mini });
        if (number != null) { var label = Ui.Text(number, 11, "#FFFFFF", true); label.HorizontalAlignment = HorizontalAlignment.Center; label.VerticalAlignment = VerticalAlignment.Top; label.Margin = new Thickness(0, 2, 0, 0); scene.Children.Add(label); }
        frame.Children.Add(scene); return new Viewbox { Child = frame, Stretch = Stretch.Uniform, MaxHeight = 82 };
    }
}
