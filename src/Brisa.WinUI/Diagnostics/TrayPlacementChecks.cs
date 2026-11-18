using System.Text.Json;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Brisa.WinUI;

public partial class App
{
    async void CheckTrayPlacement(bool review)
    {
        Window? owner = null; WindowsIntegration? integration = null; TrayMenuWindow? menu = null;
        bool cleaned = false; string stage = "start";
        void Cleanup()
        {
            if (cleaned) return; cleaned = true;
            integration?.Dispose(); owner?.Close(); Exit();
        }
        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-tray-placement-check.json"), JsonSerializer.Serialize(new { Passed = false, Stage = "starting" }));
            Ui.Translucent = false;
            stage = "owner-window";
            owner = new Window { Title = "Brisa · Prueba aislada de bandeja" };
            owner.AppWindow.IsShownInSwitchers = false;
            stage = "enumerate-displays";
            var displays = DockMonitors.Read();
            var secondary = displays.FirstOrDefault(display => !display.Primary) ?? displays[0];
            owner.AppWindow.Move(new(secondary.WorkArea.X + 60, secondary.WorkArea.Y + 60)); owner.AppWindow.Hide();
            var original = owner.AppWindow.Position;
            stage = "shell-registration";
            integration = new(WinRT.Interop.WindowNative.GetWindowHandle(owner), () => { }, () => { }, () => { }, () => { }, () => { }, () => { }, () => { }, () => { });
            bool registered = integration.SetTray(true);
            var anchor = integration.QuickMenuAnchor(fromTray: true);
            stage = "anchor-display";
            var target = DockMonitors.WorkAreaAt(anchor.X + anchor.Width / 2, anchor.Y + anchor.Height / 2);
            stage = "menu-window";
            menu = new TrayMenuWindow([
                ("Mostrar Brisa", "\uE737", () => { }), ("Organizar widgets", "\uE70F", () => { }),
                ("Añadir widgets", "\uE710", () => { }), ("", "", null), ("Tareas", "\uE73E", () => { }),
                ("Ajustes", "\uE713", () => { }), ("Calendario", "\uE787", () => { }), ("", "", null), ("Salir de Brisa", "\uE8BB", () => { })
            ], dismissOnDeactivate: review);
            menu.Closed += (_, _) => Cleanup();
            stage = "menu-placement";
            menu.ShowAnchored(anchor, 1);
            await Task.Delay(250);
            if (cleaned) return;
            var position = menu.AppWindow.Position; var size = menu.AppWindow.Size;
            bool inside = position.X >= target.X && position.Y >= target.Y && position.X + size.Width <= target.X + target.Width && position.Y + size.Height <= target.Y + target.Height;
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-tray-placement-check.json"), JsonSerializer.Serialize(new {
                Passed = registered && inside && size.Height > 200 && owner.AppWindow.Position == original,
                DisplayCount = displays.Count, OwnerOnSecondary = !secondary.Primary, Anchor = anchor,
                MenuBounds = new { position.X, position.Y, size.Width, size.Height },
                TargetWorkArea = new { target.X, target.Y, target.Width, target.Height },
                OwnerUnchanged = owner.AppWindow.Position == original, IsolatedWindow = true
            }));
            if (!review) menu.Dismiss();
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-tray-placement-check.json"), JsonSerializer.Serialize(new { Passed = false, Stage = stage, Error = error.Message }));
            menu?.Dismiss(); Cleanup();
        }
    }
}
