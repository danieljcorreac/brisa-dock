using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    string quotaHistoryProvider = "claude";
    void RecordUsage()
    {
        bool changed = false; var now = DateTimeOffset.Now;
        foreach (var snapshot in usage.Values)
        {
            changed |= UsageHistory.Append(settings.QuotaHistory, snapshot, now);
            if (!settings.QuotaAlerts || QuietNow || windowsIntegration?.TrayActive != true || snapshot.WithFreshness(now).State != ConnectionState.Connected) continue;
            foreach (var window in snapshot.Windows.Where(w => w.ResetsAt.HasValue && !w.Expired(now) && w.UsedPercent >= settings.QuotaAlertPercent))
            {
                string key = UsageHistory.AlertKey(snapshot.ProviderId, window);
                if (settings.QuotaAlerted.Contains(key)) continue;
                if (!windowsIntegration.Notify("Cuota de " + (snapshot.ProviderId == "claude" ? "Claude" : "Codex"), $"{window.Label}: {window.UsedPercent:0}% utilizado.")) continue;
                settings.QuotaAlerted.Add(key); changed = true;
            }
        }
        if (settings.QuotaAlerted.Count > 100) settings.QuotaAlerted.RemoveRange(0, settings.QuotaAlerted.Count - 100);
        if (changed) Save();
    }
    void ShowUsageHistory(string provider)
    {
        quotaHistoryProvider = provider;
        var body = Ui.Stack(12);
        body.Children.Add(Ui.Text("Sólo se guardan porcentajes, ventanas y fechas facilitados por el conector. No se guardan conversaciones ni credenciales.", 12, Ui.MutedColor));
        var alerts = new ToggleSwitch { Header = "Avisos de cuota", IsOn = settings.QuotaAlerts }; alerts.Toggled += (_, _) => { settings.QuotaAlerts = alerts.IsOn; Save(); }; body.Children.Add(alerts);
        var threshold = new NumberBox { Header = "Avisar desde (%)", Minimum = 50, Maximum = 100, Value = settings.QuotaAlertPercent, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        threshold.ValueChanged += (_, _) => { if (double.IsFinite(threshold.Value)) { settings.QuotaAlertPercent = (int)threshold.Value; Save(); } }; body.Children.Add(threshold);
        body.Children.Add(Ui.Text("Un aviso por ventana y reinicio; necesita una fecha de reinicio conocida. El historial local de Claude Desktop no la incluye. Respeta las horas silenciosas y requiere la bandeja activa. Windows puede silenciar la notificación.", 11, Ui.MutedColor));
        var readings = settings.QuotaHistory.Where(s => s.Provider == provider).TakeLast(30).Reverse().ToArray();
        if (readings.Length == 0) body.Children.Add(Ui.Text("Aún no hay lecturas reales. Conecta el proveedor desde su tarjeta.", 14));
        foreach (var label in settings.QuotaHistory.Where(s => s.Provider == provider).Select(s => s.Window).Distinct())
        {
            var segments = UsageHistory.Trend(settings.QuotaHistory, provider, label);
            var samples = segments.SelectMany(s => s).ToArray(); if (samples.Length == 0) continue;
            var trend = Ui.Stack(6); trend.Children.Add(Ui.Text(label + " · " + samples[^1].UsedPercent.ToString("0") + "%", 16, bold: true));
            var canvas = new Canvas { Width = 260, Height = 90 };
            foreach (double percent in new[] { 0d, 50d, 100d })
                canvas.Children.Add(new Line { X1 = 6, X2 = 254, Y1 = 84 - percent * .78, Y2 = 84 - percent * .78, Stroke = Ui.Brush(Ui.Light ? "#D1DDD8" : "#3B4C48"), StrokeThickness = 1 });
            double elapsed = (samples[^1].At - samples[0].At).TotalSeconds;
            double X(QuotaSample s) => elapsed <= 0 ? 130 : 6 + 248 * (s.At - samples[0].At).TotalSeconds / elapsed;
            foreach (var segment in segments)
            {
                var points = new PointCollection();
                foreach (var sample in segment) points.Add(new Point(X(sample), 84 - sample.UsedPercent * .78));
                canvas.Children.Add(new Polyline { Points = points, Stroke = Ui.Brush(Ui.Light ? "#34705D" : "#B8E5D4"), StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round });
                foreach (var sample in segment)
                {
                    var dot = new Ellipse { Width = 4, Height = 4, Fill = Ui.Brush(Ui.Light ? "#34705D" : "#B8E5D4") };
                    Canvas.SetLeft(dot, X(sample) - 2); Canvas.SetTop(dot, 82 - sample.UsedPercent * .78); canvas.Children.Add(dot);
                }
            }
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(canvas, $"Historial de {label}: escala de 0 a 100 por ciento, {samples.Length} lecturas, última {samples[^1].UsedPercent:0} por ciento");
            trend.Children.Add(new Viewbox { Child = canvas, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch, MaxHeight = 100 });
            trend.Children.Add(Ui.Text($"{samples[0].At.LocalDateTime:dd/MM HH:mm} → {samples[^1].At.LocalDateTime:dd/MM HH:mm} · 0–100%", 11, Ui.MutedColor));
            trend.Children.Add(Ui.Text("Cada punto es una lectura guardada. La línea se interrumpe al cambiar el reinicio o la fuente; los datos siguen debajo.", 11, Ui.MutedColor));
            body.Children.Add(Ui.Card(trend, 12));
        }
        foreach (var reading in readings)
        {
            var card = Ui.Stack(4); card.Children.Add(Ui.Text($"{reading.Window} · {reading.UsedPercent:0}%", 16, bold: true)); card.Children.Add(Ui.Text(reading.At.LocalDateTime.ToString("dd/MM HH:mm"), 11, Ui.MutedColor)); if (reading.Source != null) card.Children.Add(Ui.Text(reading.Source, 11, Ui.MutedColor)); body.Children.Add(Ui.Card(card, 12));
        }
        body.Children.Add(Ui.Button("Volver a la conexión", "Volver al proveedor", () => ShowUsage(provider)));
        ShowPage("quota-history", "Historial y avisos", provider == "claude" ? "CLAUDE" : "CHATGPT · CODEX", body);
    }
}
