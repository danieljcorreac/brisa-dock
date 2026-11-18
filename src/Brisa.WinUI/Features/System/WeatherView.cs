using Brisa.Core;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    WeatherReading? weather;
    string? weatherError;
    bool weatherBusy;
    DateTimeOffset lastWeatherPoll = DateTimeOffset.MinValue;
    async Task RefreshWeatherAsync()
    {
        if (ReviewFixtures) return;
        if (closing || weatherBusy || settings.WeatherLocation == null) return;
        var requestedPlace = settings.WeatherLocation;
        weatherBusy = true; lastWeatherPoll = DateTimeOffset.Now;
        try { var reading = await new OpenMeteoWeather().FetchAsync(requestedPlace, lifetime.Token); if (closing) return; if (settings.WeatherLocation == requestedPlace) { weather = reading; weatherError = null; settings.WeatherCache = new(requestedPlace, reading); Save(); } else lastWeatherPoll = DateTimeOffset.MinValue; }
        catch (OperationCanceledException) { if (settings.WeatherLocation == requestedPlace) weatherError = "La consulta no se ha completado."; }
        catch (Exception e) when (e is HttpRequestException or JsonException or InvalidOperationException) { if (settings.WeatherLocation == requestedPlace) weatherError = "No se pudo consultar el tiempo. Se conserva la última lectura disponible."; }
        finally { weatherBusy = false; }
        if (lifetime.IsCancellationRequested) return;
        Tick(); if (panel?.PageId == "weather" && panel.AppWindow.IsVisible) ShowWeather();
    }
    void ShowWeather()
    {
        var body = Ui.Stack(12);
        if (weather != null && settings.WeatherLocation != null)
        {
            var summary = Ui.Stack(6); summary.Children.Add(Ui.Text($"{weather.Temperature:0}°", 54, bold: true)); summary.Children.Add(Ui.Text(weather.Description, 18));
            if (weather.Low.HasValue && weather.High.HasValue) summary.Children.Add(Ui.Text($"Máx. {weather.High:0}° · Mín. {weather.Low:0}°", 13, Ui.MutedColor));
            summary.Children.Add(Ui.Text($"{settings.WeatherLocation.Name}\nLectura: {weather.UpdatedAt.LocalDateTime:dd/MM HH:mm}" + (DateTimeOffset.Now - weather.UpdatedAt > TimeSpan.FromMinutes(30) ? " · Datos anteriores" : ""), 11, Ui.MutedColor)); foreach (var item in summary.Children.OfType<TextBlock>()) item.TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center; body.Children.Add(Ui.Card(summary));
        }
        else body.Children.Add(Ui.Text(settings.WeatherLocation == null ? "Elige la ciudad cuyo tiempo quieres ver." : "Consulta el tiempo de " + settings.WeatherLocation.Name + ".", 14));
        if (weatherError != null) body.Children.Add(Ui.Text(weatherError, 12, Ui.MutedColor));
        if (settings.WeatherLocation != null)
        {
            var refresh = Ui.Button(weatherBusy ? "Consultando…" : "Actualizar tiempo", "Actualizar tiempo", () => { _ = RefreshWeatherAsync(); ShowWeather(); }); refresh.IsEnabled = !weatherBusy; body.Children.Add(refresh);
            body.Children.Add(Ui.Button("Desconectar ciudad", "Desconectar el tiempo", () => { settings.WeatherLocation = null; settings.WeatherCache = null; weather = null; weatherError = null; Save(); Tick(); ShowWeather(); }));
        }
        var searchPanel = Ui.Stack(10);
        var city = new TextBox { PlaceholderText = "Ciudad, país", MaxLength = 100, CornerRadius = new Microsoft.UI.Xaml.CornerRadius(12) }; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(city, "Ciudad para el tiempo"); searchPanel.Children.Add(city);
        var status = Ui.Text("La búsqueda envía la ciudad a Open-Meteo. No se utiliza tu ubicación del dispositivo.", 11, Ui.MutedColor); searchPanel.Children.Add(status); var results = Ui.Stack(7);
        searchPanel.Children.Add(Ui.Button("Buscar ciudad", "Buscar ciudad para el tiempo", async () =>
        {
            if (city.Text.Trim().Length < 2) { status.Text = "Escribe al menos dos letras."; return; }
            status.Text = "Buscando…"; results.Children.Clear();
            try
            {
                var places = await new OpenMeteoWeather().SearchAsync(city.Text, lifetime.Token);
                status.Text = places.Count == 0 ? "No se encontraron ciudades." : "Elige la ciudad:";
                foreach (var place in places)
                    results.Children.Add(Ui.Button(place.Name + " · " + place.Region, "Elegir " + place.Name + " " + place.Region, () => { settings.WeatherLocation = place; weather = null; weatherError = null; lastWeatherPoll = DateTimeOffset.MinValue; Save(); _ = RefreshWeatherAsync(); ShowWeather(); }));
            }
            catch (OperationCanceledException) { status.Text = "La búsqueda no se ha completado."; }
            catch (Exception e) when (e is HttpRequestException or JsonException or InvalidOperationException) { status.Text = "No se pudo buscar. Comprueba tu conexión."; }
        }, true));
        searchPanel.Children.Add(results);
        if (settings.WeatherLocation != null) body.Children.Add(new Expander { Header = "Cambiar ciudad", Content = searchPanel, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch, HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch }); else body.Children.Add(searchPanel); body.Children.Add(Ui.Button("Datos de Open-Meteo ↗", "Abrir Open-Meteo", () => Launch("https://open-meteo.com/")));
        ShowPage("weather", "El tiempo", "TU CIUDAD · OPEN-METEO", body);
    }
}
