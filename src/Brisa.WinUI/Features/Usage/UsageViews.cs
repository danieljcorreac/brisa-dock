using Brisa.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    readonly ClaudeDesktopDirectProvider claudeDirect = new();
    readonly UsagePollSchedule usageSchedule = new();
    static string UsageInk(string id, double percent) => Ui.HighContrast ? Ui.Accent : percent >= 85 ? Ui.Light ? "#966019" : "#E9B16C" : Ui.Light ? id == "claude" ? "#A35D3E" : "#326E62" : id == "claude" ? "#EAB494" : "#A8DECE";
    static string QuotaShortLabel(QuotaWindow window) => window.DurationMinutes == 300 ? "5 h" : window.Label == "Semana" || window.DurationMinutes == 10080 && window.Label != "Fable" ? "Semana" : window.Label;
    static Border UsageLogo(string id)
    {
        var art = new Canvas { Width = 24, Height = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var ink = Ui.Brush(id == "claude" ? "#A66142" : "#275E53");
        if (id == "claude")
        {
            for (int i = 0; i < 12; i++)
            {
                double angle = i * Math.PI / 6;
                art.Children.Add(new Microsoft.UI.Xaml.Shapes.Line { X1 = 12 + Math.Cos(angle) * 3, Y1 = 12 + Math.Sin(angle) * 3, X2 = 12 + Math.Cos(angle) * 10, Y2 = 12 + Math.Sin(angle) * 10, Stroke = ink, StrokeThickness = 2, StrokeStartLineCap = Microsoft.UI.Xaml.Media.PenLineCap.Round, StrokeEndLineCap = Microsoft.UI.Xaml.Media.PenLineCap.Round });
            }
        }
        else
        {
            art.Children.Add(new Image { Width = 24, Height = 24, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform, Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri("ms-appx:///Assets/OpenAI.svg")) });
        }
        return new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Background = Ui.Brush(id == "claude" ? "#FFE9DB" : "#ECF7F3"), Child = art, VerticalAlignment = VerticalAlignment.Center };
    }
    FrameworkElement UsageTile(string id)
    {
        string accent = id == "claude" ? "#EAB494" : "#A8DECE";
        var layout = Ui.Stack(9, true); layout.VerticalAlignment = VerticalAlignment.Center;
        layout.Children.Add(UsageLogo(id));
        var info = Ui.Stack(2); info.Width = 164;
        var title = Ui.Text(id == "claude" ? "Claude" : "ChatGPT · Codex", 11, bold: true); title.Height = 15; info.Children.Add(title);
        title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis;
        var line = Ui.Text("Conectar mi plan", 10, Ui.MutedColor); info.Children.Add(line);
        var bars = Ui.Stack(1); info.Children.Add(bars); layout.Children.Add(info);
        var meters = new List<(TextBlock Label, QuotaMeter Meter, TextBlock Number)>();
        ConnectionState? displayedState = null;
        QuotaWindow[] displayedWindows = [];
        IReadOnlyList<QuotaWindow>? lastWindowsReference = null;
        updates[id] = () =>
        {
            var snapshot = usage[id].WithFreshness(DateTimeOffset.Now);
            if (displayedState == snapshot.State && ReferenceEquals(lastWindowsReference, snapshot.Windows)) return;
            lastWindowsReference = snapshot.Windows;
            var windows = snapshot.Windows.Take(id == "claude" ? 3 : 2).ToArray();
            if (displayedState == snapshot.State && displayedWindows.SequenceEqual(windows)) return;
            displayedState = snapshot.State; displayedWindows = windows;
            line.Visibility = snapshot.Windows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            title.Text = (id == "claude" ? "Claude" : "ChatGPT · Codex") + (snapshot.IsPreviousReading ? " · anterior" : "");
            while (meters.Count > windows.Length) { bars.Children.RemoveAt(bars.Children.Count - 1); meters.RemoveAt(meters.Count - 1); }
            if (snapshot.Windows.Count == 0) { line.Text = snapshot.State == ConnectionState.Loading ? "Consultando…" : snapshot.State == ConnectionState.Error ? "Revisar conexión" : snapshot.State == ConnectionState.Unavailable ? "Cuota no disponible" : "Conectar mi plan  ↗"; return; }
            for (int i = 0; i < windows.Length; i++)
            {
                var w = windows[i]; string color = snapshot.IsPreviousReading ? Ui.MutedColor : UsageInk(id, w.UsedPercent);
                if (i < meters.Count)
                {
                    var existing = meters[i]; existing.Label.Text = QuotaShortLabel(w); existing.Meter.Set(w.UsedPercent, color); existing.Number.Text = $"{w.UsedPercent:0}%"; continue;
                }
                var barRow = new Grid { Height = 13, ColumnSpacing = 5 }; barRow.ColumnDefinitions.Add(new() { Width = new GridLength(43) }); barRow.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); barRow.ColumnDefinitions.Add(new() { Width = new GridLength(32) });
                var label = Ui.Text(QuotaShortLabel(w), 10, Ui.MutedColor); barRow.Children.Add(label);
                var progress = new QuotaMeter(w.UsedPercent, 4, color);
                Grid.SetColumn(progress, 1); barRow.Children.Add(progress);
                var number = Ui.Text($"{w.UsedPercent:0}%", 10, bold: true); number.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(number, 2); barRow.Children.Add(number); bars.Children.Add(barRow);
                meters.Add((label, progress, number));
            }
        };
        var tile = Tile(layout, id == "claude" ? "Claude · uso del plan" : "ChatGPT · límites de Codex", () => ShowUsage(id), 226); tile.Padding = new Thickness(6, 2, 6, 2); return tile;
    }
    void ShowUsage(string id)
    {
        var snapshot = usage[id].WithFreshness(DateTimeOffset.Now); var body = Ui.Stack(12);
        string accent = id == "claude" ? "#EAB494" : "#A8DECE";
        var state = snapshot.State switch { ConnectionState.Connected => "Conectado", ConnectionState.Loading => "Consultando tu cuota…", ConnectionState.Stale => "Lectura anterior", ConnectionState.Error => "Revisar conexión", ConnectionState.Unavailable => "Cuota no disponible", _ => "Conecta tu plan" };
        var identity = Ui.Stack(10, true); identity.Children.Add(UsageLogo(id));
        var identityText = Ui.Stack(3); identityText.Children.Add(Ui.Text(id == "claude" ? "Claude" : "ChatGPT · Codex", 18, bold: true)); identityText.Children.Add(Ui.Text(state, 12, Ui.Accent)); identity.Children.Add(identityText); body.Children.Add(identity);
        if (snapshot.State != ConnectionState.Connected) body.Children.Add(Ui.Text(snapshot.Message, 12, Ui.MutedColor));
        var quotas = Ui.Stack(18);
        foreach (var window in snapshot.Windows)
        {
            var card = Ui.Stack(8); var numbers = new Grid { ColumnSpacing = 12 }; numbers.ColumnDefinitions.Add(new()); numbers.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            numbers.Children.Add(Ui.Text(QuotaShortLabel(window), 14, bold: true)); var number = Ui.Text($"{window.UsedPercent:0}%", 23, bold: true); Grid.SetColumn(number, 1); numbers.Children.Add(number); card.Children.Add(numbers);
            card.Children.Add(new QuotaMeter(window.UsedPercent, 6, snapshot.IsPreviousReading ? Ui.MutedColor : UsageInk(id, window.UsedPercent)));
            card.Children.Add(Ui.Text(window.ResetsAt.HasValue ? (window.Expired(DateTimeOffset.Now) ? "Esta ventana ya venció. Actualiza para conocer la cuota." : "Se restablece " + window.DisplayReset!.Value.LocalDateTime.ToString("ddd d MMM, HH:mm", System.Globalization.CultureInfo.GetCultureInfo("es-ES"))) : "Reinicio no facilitado por el proveedor", 11, Ui.MutedColor)); quotas.Children.Add(card);
        }
        if (snapshot.Windows.Count > 0) body.Children.Add(Ui.Card(quotas));
        if (snapshot.UpdatedAt.HasValue) body.Children.Add(Ui.Text($"{snapshot.Source}\nÚltima lectura: {snapshot.UpdatedAt.Value.LocalDateTime:dd/MM HH:mm:ss}", 11, Ui.MutedColor));
        var options = Ui.Stack(12);
        if (id == "chatgpt")
        {
            options.Children.Add(Ui.Text("Esta lectura corresponde a los límites que devuelve Codex para tu plan. No cuenta mensajes del chat ni gasto de API.", 12, Ui.MutedColor));
            if (!settings.CodexEnabled)
            {
                options.Children.Add(Ui.Text("Al conectar, Brisa consulta los límites mediante tu Codex CLI. Codex gestiona la sesión; Brisa no copia sus credenciales ni inicia conversaciones.", 12));
                options.Children.Add(Ui.Button("Conectar con Codex CLI", "Conectar con Codex CLI", () => { settings.CodexEnabled = true; Save(); _ = RefreshUsageAsync(true, "chatgpt"); }, true));
            }
            else
            {
                options.Children.Add(Ui.Text("Actualización automática cada cinco minutos. La actualización manual se limita a una cada treinta segundos.", 11, Ui.MutedColor));
                options.Children.Add(Ui.Button("Desconectar de Brisa", "Desconectar cuota de Codex", () => { settings.CodexEnabled = false; usage[id] = UsageSnapshot.Empty(id, "Conecta tu plan"); Save(); Tick(); ShowUsage(id); }));
            }
            options.Children.Add(Ui.Button("Elegir codex.exe…", "Elegir ejecutable de Codex", SelectCodex));
            options.Children.Add(Ui.Text("Si falta una sesión, inicia sesión desde Codex CLI con codex login y vuelve a actualizar.", 12, Ui.MutedColor));
            options.Children.Add(Ui.Button("Abrir panel oficial", "Abrir uso de Codex", () => Launch("https://chatgpt.com/codex/settings/usage")));
        }
        else
        {
            options.Children.Add(Ui.Text("ELIGE CÓMO LEER TU CUOTA", 11, Ui.MutedColor, true));
            void SelectSource(string source)
            {
                settings.ClaudeSource = source; usage[id] = UsageSnapshot.Empty(id, source == "off" ? "Lectura de Claude desactivada." : "Comprobando la fuente elegida…");
                lastAiPoll = DateTimeOffset.MinValue; Save(); Tick(); ShowUsage(id); _ = RefreshUsageAsync(true, "claude");
            }
            options.Children.Add(Ui.Button(settings.ClaudeSource == "desktop" ? "✓ Usando Claude Desktop" : "Conectar con Claude Desktop", "Leer cuotas del historial local de Claude Desktop", () => SelectSource("desktop"), settings.ClaudeSource == "desktop"));
            options.Children.Add(Ui.Button(settings.ClaudeSource == "code" ? "✓ Usando Claude Code" : "Usar Claude Code", "Leer cuotas del puente de Claude Code", () => SelectSource("code"), settings.ClaudeSource == "code"));
            if (settings.ClaudeSource == "desktop")
            {
                if (settings.ClaudeDesktopDirect)
                {
                    options.Children.Add(Ui.Text("Consulta directa activada: Brisa usa en memoria la sesión cifrada de Desktop para pedir únicamente tus cuotas a api.anthropic.com. No guarda el token ni inicia conversaciones. Muestra 5 h, semana y Fable cuando Claude las facilita, con sus reinicios.", 12, Ui.MutedColor));
                    options.Children.Add(Ui.Text("Se consulta como máximo cada cinco minutos. Ante límites de consulta se espera al menos quince minutos. Si la sesión caduca, abre Desktop para renovarla.", 11, Ui.MutedColor));
                    options.Children.Add(Ui.Button("Usar sólo historial local", "Desactivar consulta directa y acceso a la sesión de Desktop", () => { settings.ClaudeDesktopDirect = false; SelectSource("desktop"); }));
                }
                else
                {
                    options.Children.Add(Ui.Text("Modo local: el historial de Desktop puede quedar antiguo y no incluye Fable ni reinicios. Comprobar lectura local sólo vuelve a leerlo; no fuerza su actualización.", 12, Ui.MutedColor));
                    options.Children.Add(Ui.Button("Activar consulta directa…", "Revisar y autorizar la consulta de cuotas mediante la sesión de Desktop", ShowClaudeDirectConsent, true));
                }
                options.Children.Add(Ui.Text("La integración depende de interfaces internas de Claude que pueden cambiar. Brisa indica los fallos sin reemplazarlos por porcentajes inventados.", 11, Ui.MutedColor));
            }
            else if (settings.ClaudeSource == "code")
            {
                options.Children.Add(Ui.Text("Brisa recibe la cuota del plan que Claude Code entrega a statusLine. Se actualiza al usar Claude Code y puede incluir los reinicios.", 12, Ui.MutedColor));
                options.Children.Add(Ui.Button("Configurar puente de Claude Code", "Configurar puente de Claude Code", ShowClaudeSetup, true));
            }
            if (settings.ClaudeSource != "off") options.Children.Add(Ui.Button("Desconectar de Brisa", "Desactivar la lectura de cuotas de Claude", () => SelectSource("off")));
            options.Children.Add(Ui.Button("Abrir uso de Claude", "Abrir uso oficial de Claude", () => Launch("https://claude.ai/settings/usage")));
        }
        var actions = new List<FrameworkElement>();
        bool enabled = id == "chatgpt" ? settings.CodexEnabled : settings.ClaudeSource != "off";
        if (enabled)
        {
            var refresh = Ui.Button(id == "claude" && !(settings.ClaudeSource == "desktop" && settings.ClaudeDesktopDirect) ? "Comprobar lectura" : "Actualizar cuotas", "Actualizar lectura de " + (id == "claude" ? "Claude" : "Codex"), () => _ = RefreshUsageAsync(true, id)); refresh.IsEnabled = snapshot.State != ConnectionState.Loading; actions.Add(refresh);
        }
        actions.Add(Ui.Button("Historial y avisos", "Historial y avisos de cuota", () => ShowUsageHistory(id))); body.Children.Add(ChoiceUi.Group(actions.Count, actions));
        body.Children.Add(new Expander { Header = "Conexión y opciones", Content = options, IsExpanded = snapshot.Windows.Count == 0, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        ShowPage(id, id == "claude" ? "Tu plan de Claude" : "Tu plan de ChatGPT", id == "claude" ? "CLAUDE · LÍMITES DEL PLAN" : "CHATGPT · CODEX", body);
    }
    async Task RefreshUsageAsync(bool force = false, string? provider = null)
    {
        if (ReviewFixtures) { lastAiPoll = DateTimeOffset.Now; return; }
        if (closing || !await usageGate.WaitAsync(0)) return;
        try
        {
            lastAiPoll = DateTimeOffset.Now;
            bool changed = false;
            if ((provider == null || provider == "claude") && (settings.Widgets.Contains("claude") || settings.Widgets.Contains("ai-limits") || force))
            {
                string source = settings.ClaudeSource;
                bool direct = settings.ClaudeDesktopDirect;
                string key = "claude:" + source + ":" + direct;
                if (source == "off")
                {
                    if (usage["claude"].State != ConnectionState.Disconnected) { usage["claude"] = UsageSnapshot.Empty("claude", "Lectura de Claude desactivada."); changed = true; }
                }
                else if (usageSchedule.TryStart(key, DateTimeOffset.Now, TimeSpan.FromSeconds(source == "desktop" && direct ? 300 : 30), force))
                {
                    try
                    {
                        var claude = source switch
                        {
                            "desktop" when direct => await claudeDirect.RefreshAsync(lifetime.Token),
                            "desktop" => await new ClaudeDesktopProvider().RefreshAsync(lifetime.Token),
                            _ => await new ClaudeFileProvider(store.DirectoryPath).RefreshAsync(lifetime.Token)
                        };
                        if (source == "desktop" && direct) usageSchedule.SetAutomaticDue(key, claudeDirect.NextAttemptAt);
                        if (source == settings.ClaudeSource && direct == settings.ClaudeDesktopDirect) { usage["claude"] = claude; changed = true; }
                        else lastAiPoll = DateTimeOffset.MinValue;
                    }
                    finally { usageSchedule.Complete(key); }
                }
            }
            string codexKey = "chatgpt:" + settings.CodexExecutable;
            if ((provider == null || provider == "chatgpt") && settings.CodexEnabled && (settings.Widgets.Contains("chatgpt") || settings.Widgets.Contains("ai-limits") || force) && usageSchedule.TryStart(codexKey, DateTimeOffset.Now, TimeSpan.FromMinutes(5), force))
            {
                string? executable = settings.CodexExecutable;
                try
                {
                    usage["chatgpt"] = usage["chatgpt"] with { State = ConnectionState.Loading, Message = "Consultando mediante Codex CLI…" }; Tick();
                    if (panel?.PageId == "chatgpt" && panel.AppWindow.IsVisible) ShowUsage("chatgpt");
                    var result = await new CodexUsageProvider(executable).RefreshAsync(lifetime.Token);
                    if (settings.CodexEnabled && settings.CodexExecutable == executable) { usage["chatgpt"] = result; changed = true; }
                }
                finally { usageSchedule.Complete(codexKey); }
            }
            if (closing || !changed) return;
            RecordUsage();
            Tick(); if (panel?.AppWindow.IsVisible == true) { if (panel.PageId is "claude" or "chatgpt") ShowUsage(panel.PageId); else if (panel.PageId == "ai-limits") ShowAggregateUsage(); }
        }
        catch (OperationCanceledException) { }
        finally { usageGate.Release(); }
    }
    void ShowClaudeDirectConsent()
    {
        var body = Ui.Stack(12);
        body.Children.Add(Ui.Text("Para obtener cuotas actuales, Fable y reinicios, Brisa necesita usar la sesión de Claude Desktop.", 16, bold: true));
        body.Children.Add(Ui.Text("Sólo se descifra en memoria la sesión de la cuenta local. El token se envía exclusivamente a https://api.anthropic.com/api/oauth/usage, mediante una consulta de lectura sin conversaciones. No se guarda en Brisa, no se renueva ni se copia a otros destinos. Se conserva únicamente el historial de porcentajes y reinicios.", 13));
        body.Children.Add(Ui.Text("Puedes desactivarlo desde Usar sólo historial local o Desconectar de Brisa. No se cambia tu inicio de sesión en Claude.", 12, Ui.MutedColor));
        body.Children.Add(Ui.Button("Autorizar consulta directa", "Autorizar el uso de la sesión de Desktop sólo para consultar cuotas", () => { settings.ClaudeSource = "desktop"; settings.ClaudeDesktopDirect = true; usage["claude"] = UsageSnapshot.Empty("claude", "Consultando cuotas…"); Save(); lastAiPoll = DateTimeOffset.MinValue; _ = RefreshUsageAsync(true, "claude"); ShowUsage("claude"); }, true));
        body.Children.Add(Ui.Button("Volver", "Volver sin activar consulta directa", () => ShowUsage("claude")));
        ShowPage("claude-direct-consent", "Consulta directa de Claude", "SESIÓN LOCAL · SÓLO CUOTAS", body);
    }
    async void SelectCodex()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker(); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this)); picker.FileTypeFilter.Add(".exe");
        try { var file = await picker.PickSingleFileAsync(); if (file == null) return; if (!file.Name.Equals("codex.exe", StringComparison.OrdinalIgnoreCase)) { ShowMessage("Selecciona Codex", "Elige el archivo codex.exe de tu instalación de Codex CLI."); return; } settings.CodexExecutable = file.Path; Save(); ShowUsage("chatgpt"); }
        catch { ShowMessage("No se pudo seleccionar", "Inténtalo de nuevo desde el widget de ChatGPT."); }
    }
    void ShowClaudeSetup()
    {
        string bridge = System.IO.Path.Combine(AppContext.BaseDirectory, "bridge", "BrisaBridge.exe").Replace('\\', '/');
        string directory = store.DirectoryPath.Replace('\\', '/');
        string command = $"\"{bridge}\" --data-dir \"{directory}\"";
        string snippet = JsonSerializer.Serialize(new { statusLine = new { type = "command", command } }, new JsonSerializerOptions { WriteIndented = true });
        var body = Ui.Stack(12);
        body.Children.Add(Ui.Text("1. Ten Claude Code instalado e inicia sesión en él.\n2. Añade la propiedad statusLine de abajo a tu archivo .claude/settings.json. Conserva las demás propiedades.\n3. Usa Claude Code y vuelve al dock: la cuota llegará al puente local.", 13));
        body.Children.Add(Ui.Text("Si ya tienes una statusLine personalizada, tendrás que combinar ambas. Brisa no sustituye tu configuración automáticamente.", 12, Ui.MutedColor));
        var code = Ui.Text(snippet, 11); code.IsTextSelectionEnabled = true; code.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas");
        var codeView = new ScrollViewer { Content = code, MinHeight = 120, MaxHeight = 210, HorizontalScrollMode = ScrollMode.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(codeView, "Configuración del puente de Claude Code"); body.Children.Add(Ui.Card(codeView, 12));
        body.Children.Add(Ui.Button("Copiar configuración", "Copiar configuración del puente", () => { var data = new DataPackage(); data.SetText(snippet); Clipboard.SetContent(data); }, true));
        body.Children.Add(Ui.Button("Abrir documentación de Claude", "Abrir documentación de statusLine", () => Launch("https://code.claude.com/docs/en/statusline")));
        body.Children.Add(Ui.Text("El puente guarda sólo porcentajes y reinicios en este PC. No recibe claves ni guarda el resto de los datos de la sesión.", 12, Ui.MutedColor));
        ShowPage("claude-setup", "Conectar Claude", "PUENTE LOCAL · CONFIGURACIÓN VOLUNTARIA", body);
    }
}
