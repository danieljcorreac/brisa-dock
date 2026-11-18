using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    void AddPlacementOptions(StackPanel body, Action refresh)
    {
        body.Children.Add(Ui.Text("POSICIÓN", 11, Ui.MutedColor, true));
        body.Children.Add(ChoiceUi.Group(2, new[] {
            ChoiceUi.Card("Abajo", "Borde inferior", ChoiceUi.Screen(), settings.Edge == "bottom", () => { settings.Edge = "bottom"; Save(); PositionDock(); refresh(); }),
            ChoiceUi.Card("Arriba", "Borde superior", ChoiceUi.Screen(top: true), settings.Edge == "top", () => { settings.Edge = "top"; Save(); PositionDock(); refresh(); })
        }));
        body.Children.Add(Ui.Text("TAMAÑO", 11, Ui.MutedColor, true));
        var sizes = new[] { ("Pequeño", .85, .45), ("Normal", 1d, .65), ("Grande", 1.15, .86) };
        body.Children.Add(ChoiceUi.Group(3, sizes.Select(s => ChoiceUi.Card(s.Item1, $"{s.Item2 * 100:0}%", ChoiceUi.Screen(barWidth: s.Item3), s.Item2 < 1 ? settings.Scale < 1 : s.Item2 > 1 ? settings.Scale > 1 : settings.Scale == 1, () => { settings.Scale = s.Item2; Save(); BuildDock(); refresh(); }))));
    }
}
