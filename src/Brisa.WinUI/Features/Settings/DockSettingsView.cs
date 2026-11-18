using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    void ShowDockSettings()
    {
        var body = Ui.Stack(16);
        AddMonitorSettings(body);
        void Toggle(string name, bool value, Action<bool> set)
        {
            var toggle = new ToggleSwitch { Header = name, IsOn = value };
            toggle.Toggled += (_, _) => { set(toggle.IsOn); Save(); BuildDock(); }; body.Children.Add(toggle);
        }
        void Slider(string name, double value, double min, double max, Action<double> set)
        {
            var control = new Slider { Header = name, Value = value, Minimum = min, Maximum = max, StepFrequency = 1, SmallChange = 1, LargeChange = 4 };
            var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            debounce.Tick += (_, _) => { debounce.Stop(); if (!lifetime.IsCancellationRequested) { Save(); BuildDock(); } };
            control.ValueChanged += (_, e) => { set(e.NewValue); debounce.Stop(); debounce.Start(); }; body.Children.Add(control);
        }
        Toggle("Ocultar automáticamente", settings.AutoHide, v => settings.AutoHide = v);
        Toggle("Mantener visible en el escritorio", settings.KeepOnDesktop, v => settings.KeepOnDesktop = v);
        Toggle("Ocultar durante pantalla completa", settings.HideInFullscreen, v => settings.HideInFullscreen = v);
        body.Children.Add(Ui.Text("Con ocultación automática, lleva el puntero al borde superior o inferior para recuperar el dock. Las tarjetas abiertas lo mantienen visible.", 12, Ui.MutedColor));
        body.Children.Add(Ui.Text("ANCHO", 11, Ui.MutedColor, true));
        body.Children.Add(ChoiceUi.Group(2, new[] { ("Automático", 0, .5), ("50%", 50, .5), ("75%", 75, .75), ("Completo", 100, .95) }.Select(option => ChoiceUi.Card(option.Item1, option.Item2 == 0 ? "Según tus widgets" : "De la pantalla", ChoiceUi.Screen(barWidth: option.Item3), settings.WidthPercent == option.Item2, () => { settings.WidthPercent = option.Item2; Save(); BuildDock(); ShowDockSettings(); }))));
        body.Children.Add(Ui.Text("Si el contenido no cabe, la rueda del ratón lo desplaza horizontalmente; los widgets conservan su tamaño.", 12, Ui.MutedColor));
        Slider("Separación entre widgets", settings.WidgetSpacing, 0, 24, v => settings.WidgetSpacing = v);
        Slider("Relleno lateral", settings.DockPadding, 4, 24, v => settings.DockPadding = v);
        Slider("Distancia al borde", settings.EdgeDistance, 0, 48, v => settings.EdgeDistance = v);
        Slider("Ampliación de iconos (%)", Math.Round(settings.Magnification * 100), 100, 165, v => settings.Magnification = v / 100);
        body.Children.Add(Ui.Text("ICONOS DE APLICACIONES", 11, Ui.MutedColor, true));
        body.Children.Add(AppIconSizeOptions(ShowDockSettings));
        Toggle("Mostrar aplicaciones abiertas", settings.ShowRunningApps, v => settings.ShowRunningApps = v);
        Toggle("Tarjetas detrás de las aplicaciones", settings.AppCards, v => settings.AppCards = v);
        Toggle("Nombres al pasar el puntero", settings.HoverNames, v => settings.HoverNames = v);
        Toggle("Bloquear arrastre de widgets", settings.DockLocked, v => settings.DockLocked = v);
        Toggle("Reloj de 12 horas", settings.Clock12Hour, v => settings.Clock12Hour = v);
        Toggle("Mostrar segundos", settings.ClockSeconds, v => settings.ClockSeconds = v);
        body.Children.Add(Ui.Text("El bloqueo impide el arrastre accidental. El menú Orden de los widgets permite reorganizarlo de forma explícita.", 11, Ui.MutedColor));
        ShowPage("dock-settings", "El dock", "COMPORTAMIENTO · ESPACIO · ICONOS · RELOJ", body);
    }
}
