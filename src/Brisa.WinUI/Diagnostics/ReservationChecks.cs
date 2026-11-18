using Microsoft.UI.Xaml;
using Windows.Graphics;
namespace Brisa.WinUI;

public partial class App
{
    async void CheckReservation()
    {
        Window? probe = null; DesktopReservation? reservation = null;
        var checks = new List<string>(); object? error = null;
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add(name); }
        try
        {
            var monitor = DockMonitors.Read().OrderBy(m => m.Primary).First();
            RectInt32 Work() => DockMonitors.Read().First(m => m.Id == monitor.Id).WorkArea;
            RectInt32 before = Work();
            probe = new Window { Title = "Brisa · Comprobando espacio de Windows", Content = new Microsoft.UI.Xaml.Controls.TextBlock { Text = "Prueba temporal de espacio de Brisa" } };
            probe.AppWindow.IsShownInSwitchers = false;
            reservation = new(WinRT.Interop.WindowNative.GetWindowHandle(probe), () => { });
            void Place() { if (reservation.Band is { } band) probe.AppWindow.MoveAndResize(band); }
            try
            {
                Check(reservation.Update(true, monitor.Bounds, true, 24), "Register top reservation"); Place(); probe.AppWindow.Show(false); await Task.Delay(200);
                Check(reservation.Band is { } top && Work().Y == top.Y + top.Height, "Windows work area respects top strip");
                var band = reservation.Band; Check(reservation.Update(true, monitor.Bounds, true, 24) && reservation.Band == band, "Repeated layout remains stable");
                Check(reservation.Update(true, monitor.Bounds, true, 40), "Resize top reservation"); Place(); await Task.Delay(200);
                Check(reservation.Band is { } resized && Work().Y == resized.Y + resized.Height, "Windows work area follows resized strip");
                Check(reservation.Update(true, monitor.Bounds, false, 30), "Move reservation to bottom"); Place(); await Task.Delay(200);
                Check(reservation.Band is { } bottom && Work().Y + Work().Height == bottom.Y, "Windows work area respects bottom strip");
                reservation.ProbeShellRestartForChecks();
                Check(reservation.Update(true, monitor.Bounds, true, 24), "Reservation re-registers after isolated Shell restart message"); Place(); await Task.Delay(200);
                Check(reservation.Band is { } recovered && Work().Y == recovered.Y + recovered.Height, "Recovered reservation restores its work area");
            }
            finally { reservation.Release(); }
            await Task.Delay(200);
            Check(Work() == before, "Original work area restored after release");
            Check(reservation.Update(false, monitor.Bounds, false, 30) && !reservation.Active, "Disabling remains harmless after release");
        }
        catch (Exception e) { error = new { Type = e.GetType().Name, e.Message }; }
        finally
        {
            reservation?.Dispose(); probe?.Close();
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-reservation-check.json"), System.Text.Json.JsonSerializer.Serialize(new { Passed = error == null, Checks = checks, Error = error }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Exit();
        }
    }
}
