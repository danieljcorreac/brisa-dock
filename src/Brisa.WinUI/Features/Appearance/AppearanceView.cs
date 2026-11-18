using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Automation;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    void ShowAppearance()
    {
        var body = Ui.Stack(14);
        body.Children.Add(Ui.Text("Elige el color y, por separado, cómo se mezcla con tu escritorio.", 13, Ui.MutedColor));
        body.Children.Add(Ui.Text("COLOR", 11, Ui.MutedColor, true));
        var themes = new List<FrameworkElement>();
        foreach (var (id, name, color) in new[] { ("glass", "Azulado", "#26364A"), ("midnight", "Medianoche", "#111722"), ("light", "Claro", "#EEF3FA") })
        {
            var scene = new Grid { Width = 132, Height = 76, CornerRadius = new CornerRadius(10), Background = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1), GradientStops = { new GradientStop { Color = Ui.Color("#596B9A"), Offset = 0 }, new GradientStop { Color = Ui.Color("#D9A68B"), Offset = 1 } } } };
            scene.Children.Add(new Ellipse { Width = 46, Height = 46, Fill = Ui.Brush("#6FAACCBF"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(7, 4, 0, 0) });
            scene.Children.Add(new Border { Width = 46, Height = 22, CornerRadius = new CornerRadius(5), Background = Ui.Brush("#60FFFFFF"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 9, 0) });
            var mini = Ui.Stack(6, true); mini.HorizontalAlignment = HorizontalAlignment.Center;
            mini.Children.Add(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(4), Background = Ui.Brush("#D3A283"), Child = Ui.Glyph("\uE8D4", 8, "#332A27") });
            mini.Children.Add(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(4), Background = Ui.Brush("#A9D8C6"), Child = Ui.Glyph("\uE99A", 8, "#243B35") });
            mini.Children.Add(new Border { Width = 17, Height = 3, CornerRadius = new CornerRadius(1), Background = Ui.Brush(id == "light" ? "#486D63" : "#B9E9D6"), VerticalAlignment = VerticalAlignment.Center });
            string alpha = settings.Material == "solid" ? "FF" : ((byte)(60 + settings.TintStrength * 170)).ToString("X2");
            scene.Children.Add(new Border { Height = 28, Margin = new Thickness(8, 0, 8, 9), VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(8), Background = Ui.Brush("#" + alpha + color[1..]), Child = mini });
            var preview = new Viewbox { Child = scene, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch };
            themes.Add(ChoiceUi.Card(name, "", preview, settings.Theme == id, () => { settings.Theme = id; ApplyAppearance(); ShowAppearance(); }));
        }
        body.Children.Add(ChoiceUi.Group(3, themes));
        body.Children.Add(Ui.Text("MATERIAL", 11, Ui.MutedColor, true));
        body.Children.Add(ChoiceUi.Group(2, new[] {
            ChoiceUi.Card("Sólido", "Color uniforme", ChoiceUi.Screen(solid: true), settings.Material == "solid", () => { settings.Material = "solid"; ApplyAppearance(); ShowAppearance(); }),
            ChoiceUi.Card("Translúcido", "Fondo difuminado", ChoiceUi.Screen(solid: false), settings.Material == "translucent", () => { settings.Material = "translucent"; ApplyAppearance(); ShowAppearance(); })
        }));
        if (settings.Material == "translucent")
        {
            var strength = new Slider { Header = "Intensidad del color", Minimum = 10, Maximum = 80, StepFrequency = 5, Value = settings.TintStrength * 100 };
            AutomationProperties.SetName(strength, "Intensidad del color del cristal");
            var explanation = Ui.Text("Menos color deja ver más el fondo. Más color mejora el contraste.", 11, Ui.MutedColor); body.Children.Add(strength); body.Children.Add(explanation);
            // Coalesce slider changes instead of rebuilding the material per pointer event.
            var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            debounce.Tick += (_, _) => { debounce.Stop(); if (closing) return; ApplyPreferences(); Save(); SetSurface(); panel?.RefreshMaterial(); };
            strength.ValueChanged += (_, _) => { settings.TintStrength = strength.Value / 100; debounce.Stop(); debounce.Start(); };
            body.Children.Add(Ui.Text("Windows puede usar un fondo sólido si sus efectos de transparencia están desactivados o no están disponibles.", 11, Ui.MutedColor));
        }
        body.Children.Add(Ui.Button("Volver a preferencias", "Volver a preferencias de Brisa", ShowSettings));
        ShowPage("appearance", "A tu estilo", "COLOR · MATERIAL", body);
    }
    void ApplyAppearance() { ApplyPreferences(); Save(); SetSurface(); BuildDock(); }
}
