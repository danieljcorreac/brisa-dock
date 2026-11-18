using System.Globalization;
using System.Text.Json;
namespace Brisa.Core;
public record WeatherLocation(string Name, string Region, double Latitude, double Longitude);
public record WeatherCache(WeatherLocation Location, WeatherReading Reading);
public record WeatherReading(double Temperature, double? Low, double? High, int Code, bool IsDay, DateTimeOffset UpdatedAt)
{
    public string Description => Code switch { 0 => "Despejado", 1 or 2 => "Parcialmente nublado", 3 => "Nublado", 45 or 48 => "Niebla", >= 51 and <= 57 => "Llovizna", >= 61 and <= 67 => "Lluvia", >= 71 and <= 77 => "Nieve", >= 80 and <= 82 => "Chubascos", >= 85 and <= 86 => "Nieve", >= 95 => "Tormenta", _ => "Tiempo variable" };
}
public sealed class OpenMeteoWeather
{
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };
    public async Task<IReadOnlyList<WeatherLocation>> SearchAsync(string city, CancellationToken token)
    {
        string url = "https://geocoding-api.open-meteo.com/v1/search?count=5&language=es&name=" + Uri.EscapeDataString(city.Trim());
        using var data = JsonDocument.Parse(await http.GetStringAsync(url, token));
        List<WeatherLocation> places = [];
        if (data.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            foreach (var r in results.EnumerateArray())
            {
                if (!r.TryGetProperty("latitude", out var lat) || !r.TryGetProperty("longitude", out var lon)) continue;
                string name = r.GetProperty("name").GetString() ?? city;
                string country = r.TryGetProperty("country", out var c) ? c.GetString() ?? "" : "";
                string region = r.TryGetProperty("admin1", out var a) ? (a.GetString() ?? "") : "";
                places.Add(new(name, string.Join(", ", new[] { region, country }.Where(v => v.Length > 0)), lat.GetDouble(), lon.GetDouble()));
            }
        return places;
    }
    public async Task<WeatherReading> FetchAsync(WeatherLocation place, CancellationToken token)
    {
        string coordinates = "latitude=" + place.Latitude.ToString(CultureInfo.InvariantCulture) + "&longitude=" + place.Longitude.ToString(CultureInfo.InvariantCulture);
        string url = "https://api.open-meteo.com/v1/forecast?" + coordinates + "&current=temperature_2m,weather_code,is_day&daily=temperature_2m_max,temperature_2m_min&forecast_days=1&timezone=auto&temperature_unit=celsius";
        using var data = JsonDocument.Parse(await http.GetStringAsync(url, token));
        return Parse(data.RootElement, DateTimeOffset.Now);
    }
    public static WeatherReading Parse(JsonElement data, DateTimeOffset now)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("current", out var current) || current.ValueKind != JsonValueKind.Object || !current.TryGetProperty("temperature_2m", out var temperature) || temperature.ValueKind != JsonValueKind.Number || !temperature.TryGetDouble(out double degrees) || !double.IsFinite(degrees)) throw new JsonException("No hay temperatura actual.");
        int code = current.TryGetProperty("weather_code", out var wc) && wc.ValueKind == JsonValueKind.Number && wc.TryGetInt32(out int value) ? value : -1;
        bool day = current.TryGetProperty("is_day", out var dayValue) && dayValue.ValueKind == JsonValueKind.Number && dayValue.GetInt32() == 1;
        double? ReadDaily(string key)
        {
            if (data.TryGetProperty("daily", out var daily) && daily.ValueKind == JsonValueKind.Object && daily.TryGetProperty(key, out var values) && values.ValueKind == JsonValueKind.Array && values.GetArrayLength() > 0 && values[0].ValueKind == JsonValueKind.Number && values[0].TryGetDouble(out double result) && double.IsFinite(result)) return result;
            return null;
        }
        return new(degrees, ReadDaily("temperature_2m_min"), ReadDaily("temperature_2m_max"), code, day, now);
    }
}
