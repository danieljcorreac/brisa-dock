using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
namespace Brisa.WinUI;

public sealed partial class MainWindow
{
    string? soundOutput;
    void RenderSoundMixer()
    {
        var body = Ui.Stack(14); var status = Ui.Status();
        try
        {
            var outputs = Audio.Outputs();
            var selected = outputs.FirstOrDefault(o => o.Id == soundOutput) ?? outputs.FirstOrDefault();
            if (selected == null) body.Children.Add(Ui.Text("No hay salidas de audio disponibles. Conecta tus altavoces o auriculares y actualiza.", 14));
            else
            {
                soundOutput = selected.Id;
                var picker = new ComboBox { Header = "Controlar salida", ItemsSource = outputs.Select(o => o.Name + (o.Default ? " · Predeterminada" : "")).ToArray(), SelectedIndex = outputs.ToList().FindIndex(o => o.Id == selected.Id), HorizontalAlignment = HorizontalAlignment.Stretch };
                picker.SelectionChanged += (_, _) => { if (picker.SelectedIndex >= 0) { soundOutput = outputs[picker.SelectedIndex].Id; RenderSoundMixer(); } };
                body.Children.Add(picker); body.Children.Add(Ui.Text("Controla el volumen de esta salida y sus aplicaciones. Para cambiar la salida predeterminada, abre los ajustes de sonido de Windows.", 11, Ui.MutedColor));
                var state = Audio.Read(selected.Id); bool syncing = false; var lastInteraction = DateTimeOffset.MinValue;
                var number = Ui.Text($"{state.Volume}%", 42, bold: true); number.TextAlignment = TextAlignment.Center; body.Children.Add(Ui.Card(number));
                var slider = new Slider { Minimum = 0, Maximum = 100, Value = state.Volume, Header = "Volumen de la salida", StepFrequency = 1 };
                var outputInteraction = new ControlInteraction(slider);
                slider.ValueChanged += (_, e) => { if (syncing) return; lastInteraction = DateTimeOffset.Now; try { Audio.SetVolume(selected.Id, (float)e.NewValue / 100); number.Text = $"{e.NewValue:0}%"; } catch (COMException) { status.Text = "La salida ha cambiado. Actualiza los dispositivos."; } }; body.Children.Add(slider);
                var mute = Ui.Button(state.Muted ? "Activar sonido" : "Silenciar salida", "Alternar silencio de la salida", () =>
                { try { Audio.SetMute(selected.Id, !Audio.Read(selected.Id).Muted); RenderSoundMixer(); } catch (COMException) { status.Text = "La salida ya no está disponible."; } }); body.Children.Add(mute);
                body.Children.Add(Ui.Text("VOLUMEN POR APLICACIÓN", 11, Ui.MutedColor, true));
                var sessionsHost = Ui.Stack(10); body.Children.Add(sessionsHost);
                var controls = new Dictionary<string, (Slider Slider, Button Mute, TextBlock Value, Action<AudioSession> Update)>();
                void Sessions()
                {
                    var sessions = Audio.ReadSessions(selected.Id);
                    var existing = sessions.Select(s => s.Id).ToHashSet();
                    foreach (var key in controls.Keys.Where(k => !existing.Contains(k)).ToArray()) { var control = controls[key]; sessionsHost.Children.Remove((UIElement)control.Slider.Tag); controls.Remove(key); }
                    if (sessions.Count > 0 && sessionsHost.Children.FirstOrDefault() is TextBlock) sessionsHost.Children.Clear();
                    foreach (var session in sessions)
                    {
                        if (controls.TryGetValue(session.Id, out var current)) { current.Update(session); continue; }
                        var card = Ui.Stack(6); var title = Ui.Text(session.Name, 15, bold: true); var value = Ui.Text("", 11, Ui.MutedColor); card.Children.Add(title); card.Children.Add(value);
                        var volume = new Slider { Minimum = 0, Maximum = 100, StepFrequency = 1, Value = session.Volume }; var sessionInteraction = new ControlInteraction(volume); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(volume, "Volumen de " + session.Name); card.Children.Add(volume);
                        bool updating = false; var changed = DateTimeOffset.MinValue; AudioSession latest = session;
                        var silence = Ui.Button("", "Alternar silencio de " + session.Name, () => { try { if (!Audio.SetSession(selected.Id, session.Id, muted: !latest.Muted)) status.Text = "La sesión terminó. Actualiza las aplicaciones."; } catch (COMException) { status.Text = "No se pudo cambiar esta sesión."; } }); card.Children.Add(silence);
                        volume.ValueChanged += (_, e) => { if (updating) return; changed = DateTimeOffset.Now; try { if (!Audio.SetSession(selected.Id, session.Id, level: (float)e.NewValue / 100)) status.Text = "La sesión terminó."; } catch (COMException) { status.Text = "No se pudo cambiar el volumen."; } };
                        void Update(AudioSession reading) { latest = reading; value.Text = $"{reading.Volume}% · " + (reading.Muted ? "Silenciado" : reading.Active ? "Reproduciendo" : "En espera"); silence.Content = reading.Muted ? "Activar sonido" : "Silenciar"; if (DateTimeOffset.Now - changed > TimeSpan.FromSeconds(1) && !sessionInteraction.Active) { updating = true; volume.Value = reading.Volume; updating = false; } }
                        var host = Ui.Card(card, 12); volume.Tag = host; sessionsHost.Children.Add(host); controls[session.Id] = (volume, silence, value, Update); Update(session);
                    }
                    if (sessions.Count == 0 && sessionsHost.Children.Count == 0) sessionsHost.Children.Add(Ui.Text("Abre una aplicación con audio para mostrar su control aquí.", 12, Ui.MutedColor));
                }
                Sessions();
                var lastPoll = DateTimeOffset.MinValue;
                void Update()
                {
                    if (DateTimeOffset.Now - lastPoll < TimeSpan.FromSeconds(2)) return; lastPoll = DateTimeOffset.Now;
                    try
                    {
                        var reading = Audio.Read(selected.Id); number.Text = $"{reading.Volume}%"; mute.Content = reading.Muted ? "Activar sonido" : "Silenciar salida";
                        if (DateTimeOffset.Now - lastInteraction > TimeSpan.FromSeconds(1) && !outputInteraction.Active) { syncing = true; slider.Value = reading.Volume; syncing = false; }
                        Sessions();
                    }
                    catch (COMException) { slider.IsEnabled = false; mute.IsEnabled = false; status.Text = "Esta salida se ha desconectado. Actualiza para elegir otra."; }
                }
                body.Children.Add(status); body.Children.Add(Ui.Button("Actualizar dispositivos", "Actualizar salidas y aplicaciones de audio", RenderSoundMixer));
                body.Children.Add(Ui.Button("Salida predeterminada y ajustes ↗", "Abrir ajustes de sonido de Windows", () => Launch("ms-settings:sound")));
                ShowPage("sound", "A tu volumen", "SALIDAS · MEZCLADOR · EN DIRECTO", body); livePanelUpdate = Update; return;
            }
        }
        catch (COMException) { body.Children.Add(Ui.Text("Windows no ha facilitado los dispositivos de audio.", 13, Ui.MutedColor)); }
        body.Children.Add(Ui.Button("Actualizar", "Actualizar audio", RenderSoundMixer)); body.Children.Add(Ui.Button("Ajustes de sonido ↗", "Abrir ajustes de sonido", () => Launch("ms-settings:sound"))); ShowPage("sound", "A tu volumen", "SONIDO", body);
    }
}
