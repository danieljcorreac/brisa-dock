using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Control;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    GlobalSystemMediaTransportControlsSessionManager? mediaManager;
    GlobalSystemMediaTransportControlsSession? mediaSession;
    GlobalSystemMediaTransportControlsSession? preferredMediaSession;
    IReadOnlyList<GlobalSystemMediaTransportControlsSession> mediaSessions = [];
    string mediaSessionListKey = "";
    string mediaTitle = "Sin reproducci\u00f3n", mediaArtist = "Abre m\u00fasica o v\u00eddeo", mediaIdentity = "";
    bool mediaPlaying, mediaReading, mediaCanToggle, mediaCanPrevious, mediaCanNext;
    BitmapImage? mediaArt;
    DateTimeOffset lastMediaPoll;
    async Task RefreshMediaAsync(bool force = false)
    {
        if (closing || mediaReading || (!force && (!settings.Widgets.Contains("music") || DateTimeOffset.Now - lastMediaPoll < TimeSpan.FromSeconds(2)))) return;
        var before = (mediaTitle, mediaArtist, mediaPlaying, mediaArt, available: mediaSession != null, mediaCanToggle, mediaCanPrevious, mediaCanNext, mediaSessionListKey);
        mediaReading = true; lastMediaPoll = DateTimeOffset.Now;
        using var refreshTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); refreshTimeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            mediaManager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(refreshTimeout.Token).WaitAsync(TimeSpan.FromSeconds(6), refreshTimeout.Token);
            if (closing) return;
            mediaSessions = mediaManager.GetSessions().ToArray();
            mediaSessionListKey = string.Join("|", mediaSessions.Select(s => s.SourceAppUserModelId));
            var session = mediaSessions.FirstOrDefault(s => s.Equals(preferredMediaSession)) ?? mediaManager.GetCurrentSession();
            if (preferredMediaSession != null && !mediaSessions.Any(s => s.Equals(preferredMediaSession))) preferredMediaSession = null;
            if (session == null)
            {
                mediaSession = null; mediaTitle = "Sin reproducci\u00f3n"; mediaArtist = "Abre m\u00fasica o v\u00eddeo"; mediaPlaying = mediaCanToggle = mediaCanPrevious = mediaCanNext = false; mediaArt = null; mediaIdentity = "";
            }
            else
            {
                var properties = await session.TryGetMediaPropertiesAsync().AsTask(refreshTimeout.Token).WaitAsync(TimeSpan.FromSeconds(6), refreshTimeout.Token); if (closing) return; var playback = session.GetPlaybackInfo();
                string identity = session.SourceAppUserModelId + "\n" + properties.Title + "\n" + properties.Artist + "\n" + properties.AlbumTitle;
                mediaSession = session; mediaTitle = string.IsNullOrWhiteSpace(properties.Title) ? "Reproducci\u00f3n activa" : properties.Title; mediaArtist = properties.Artist;
                mediaCanToggle = playback.Controls.IsPlayPauseToggleEnabled; mediaCanPrevious = playback.Controls.IsPreviousEnabled; mediaCanNext = playback.Controls.IsNextEnabled;
                mediaPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                if (identity != mediaIdentity)
                {
                    mediaIdentity = identity; mediaArt = null;
                    if (properties.Thumbnail != null)
                    {
                        try { using var stream = await properties.Thumbnail.OpenReadAsync().AsTask(refreshTimeout.Token).WaitAsync(TimeSpan.FromSeconds(6), refreshTimeout.Token); var bitmap = new BitmapImage { DecodePixelWidth = 224 }; await bitmap.SetSourceAsync(stream).AsTask(refreshTimeout.Token).WaitAsync(TimeSpan.FromSeconds(6), refreshTimeout.Token); mediaArt = bitmap; }
                        catch { /* Artwork is optional; keep the native icon. */ }
                    }
                }
            }
        }
        catch { mediaSession = null; mediaTitle = "Multimedia no disponible"; mediaArtist = "Windows no ha facilitado la sesi\u00f3n"; mediaArt = null; mediaPlaying = false; }
        finally { mediaReading = false; }
        if (!lifetime.IsCancellationRequested && updates.TryGetValue("music", out var update)) update();
        if (!lifetime.IsCancellationRequested && panel?.PageId == "music" && panel.AppWindow.IsVisible && before != (mediaTitle, mediaArtist, mediaPlaying, mediaArt, mediaSession != null, mediaCanToggle, mediaCanPrevious, mediaCanNext, mediaSessionListKey)) RenderMusic();
    }
    FrameworkElement MusicTile()
    {
        var art = new Grid { Width = 34, Height = 34 }; var title = Ui.Text(mediaTitle, 12, bold: true); title.MaxLines = 1; title.TextTrimming = TextTrimming.CharacterEllipsis;
        var artist = Ui.Text(mediaArtist, 10, Ui.MutedColor); artist.MaxLines = 1; artist.TextTrimming = TextTrimming.CharacterEllipsis;
        var text = Ui.Stack(2); text.Width = 100; text.Children.Add(title); text.Children.Add(artist);
        var content = Ui.Stack(8, true); content.Children.Add(art); content.Children.Add(text);
        var tile = Tile(content, "Reproducci\u00f3n actual", ShowMusic, 158);
        var pauseGlyph = Ui.Glyph("\uE768", 14); var pause = Ui.Button(pauseGlyph, "Pausar o reproducir", () => MediaCommand(0)); pause.Width = 30; pause.Height = 36; pause.Padding = new Thickness(3);
        var line = Ui.Stack(0, true); line.VerticalAlignment = VerticalAlignment.Center; line.Children.Add(tile); line.Children.Add(pause);
        updates["music"] = () =>
        {
            title.Text = mediaTitle; artist.Text = mediaArtist;
            if (mediaArt != null)
            {
                if (art.Children.FirstOrDefault() is not Image image || image.Source != mediaArt) { art.Children.Clear(); art.Children.Add(new Image { Source = mediaArt, Width = 34, Height = 34, Stretch = Stretch.UniformToFill }); }
            }
            else if (art.Children.FirstOrDefault() is not FontIcon) { art.Children.Clear(); art.Children.Add(Ui.Glyph("\uE8D6", 26, Ui.Light ? "#49796A" : "#B9E9D6")); }
            pauseGlyph.Glyph = mediaPlaying ? "\uE769" : "\uE768"; pause.IsEnabled = mediaSession != null && mediaCanToggle;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pause, mediaPlaying ? "Pausar reproducci\u00f3n" : "Reproducir");
            ToolTipService.SetToolTip(tile, mediaTitle + (string.IsNullOrWhiteSpace(mediaArtist) ? "" : " \u00b7 " + mediaArtist));
        };
        updates["music"](); return new Border { Child = line, Background = Ui.Brush(Ui.TileSurface), CornerRadius = new(14) };
    }
    async void MediaCommand(int command)
    {
        var session = mediaSession; if (session == null) return;
        try
        {
            bool accepted = command switch { -1 => await session.TrySkipPreviousAsync(), 1 => await session.TrySkipNextAsync(), _ => await session.TryTogglePlayPauseAsync() };
            if (!accepted) { ShowMessage("Multimedia", "La aplicaci\u00f3n no ha aceptado el control. Puedes seguir usando sus propios controles."); return; }
            await RefreshMediaAsync(true);
        }
        catch { ShowMessage("Multimedia", "La sesi\u00f3n ha cambiado o ya no est\u00e1 disponible."); }
    }
    async void ShowMusic()
    {
        RenderMusic(); await RefreshMediaAsync(true);
    }
    void RenderMusic()
    {
        var body = Ui.Stack(14);
        if (mediaSessions.Count > 1)
        {
            var choices = mediaSessions.ToArray();
            var sources = new ComboBox { Header = "Aplicación multimedia", ItemsSource = choices.Select(s => s.SourceAppUserModelId.Split('!')[0].Split('_')[0]).ToArray(), SelectedIndex = Math.Max(0, Array.FindIndex(choices, s => s.Equals(mediaSession))), HorizontalAlignment = HorizontalAlignment.Stretch };
            sources.SelectionChanged += async (_, _) => { if (sources.SelectedIndex < 0 || sources.SelectedIndex >= choices.Length) return; preferredMediaSession = choices[sources.SelectedIndex]; await RefreshMediaAsync(true); if (!closing && panel?.PageId == "music" && panel.AppWindow.IsVisible) RenderMusic(); };
            body.Children.Add(sources);
        }
        var art = new Grid { Width = 112, Height = 112, HorizontalAlignment = HorizontalAlignment.Center };
        if (mediaArt != null) art.Children.Add(new Image { Source = mediaArt, Width = 112, Height = 112, Stretch = Stretch.UniformToFill });
        else art.Children.Add(Ui.Glyph("\uE8D6", 54, Ui.Light ? "#52766E" : "#B9E9D6"));
        body.Children.Add(art); body.Children.Add(Ui.Text(mediaTitle, 24, bold: true)); body.Children.Add(Ui.Text(mediaArtist, 13, Ui.MutedColor));
        Action? timelineUpdate = null;
        if (mediaSession != null)
        {
            var timeline = Ui.Text("", 12, Ui.MutedColor); body.Children.Add(timeline);
            var seek = new Slider { Minimum = 0, Maximum = 1, Value = 0, StepFrequency = 1, Header = "Posición de reproducción" }; body.Children.Add(seek);
            var seeking = new ControlInteraction(seek);
            bool updatingSeek = false;
            var seekDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            var timelineSession = mediaSession;
            seekDelay.Tick += async (_, _) =>
            {
                seekDelay.Stop(); if (closing || !timelineSession.Equals(mediaSession) || panel?.PageId != "music" || !panel.AppWindow.IsVisible) return; if (seeking.Active) { seekDelay.Start(); return; }
                try
                {
                    var data = timelineSession.GetTimelineProperties();
                    bool accepted = await timelineSession.TryChangePlaybackPositionAsync(data.StartTime.Ticks + TimeSpan.FromSeconds(seek.Value).Ticks);
                    if (!accepted) timeline.Text = "La aplicación no aceptó el cambio de posición.";
                }
                catch { timeline.Text = "La sesión ya no está disponible."; }
            };
            seek.ValueChanged += (_, _) => { if (!updatingSeek) { seekDelay.Stop(); seekDelay.Start(); } };
            void UpdateTimeline()
            {
                try
                {
                    var data = timelineSession.GetTimelineProperties(); var playback = timelineSession.GetPlaybackInfo();
                    double length = (data.EndTime - data.StartTime).TotalSeconds;
                    if (length <= 0 || length > TimeSpan.FromDays(7).TotalSeconds) { seek.IsEnabled = false; timeline.Text = "Sin duración disponible"; return; }
                    double position = (data.Position - data.StartTime).TotalSeconds;
                    if (mediaPlaying && data.LastUpdatedTime.Year > 1970) position += Math.Max(0, (DateTimeOffset.Now - data.LastUpdatedTime).TotalSeconds) * (playback.PlaybackRate ?? 1);
                    position = Math.Clamp(position, 0, length);
                    timeline.Text = DurationText(TimeSpan.FromSeconds(position)) + " / " + DurationText(TimeSpan.FromSeconds(length));
                    seek.IsEnabled = playback.Controls.IsPlaybackPositionEnabled;
                    if (!seekDelay.IsEnabled && !seeking.Active) { updatingSeek = true; seek.Maximum = length; seek.Value = position; updatingSeek = false; }
                }
                catch { seek.IsEnabled = false; timeline.Text = "La sesión ha terminado."; }
            }
            UpdateTimeline();
            timelineUpdate = UpdateTimeline;
            var buttons = Ui.Stack(8, true);
            var previous = Ui.Button(Ui.Glyph("\uE892", 18), "Pista anterior", () => MediaCommand(-1)); previous.IsEnabled = mediaCanPrevious; buttons.Children.Add(previous);
            var toggle = Ui.Button(mediaPlaying ? "Pausar" : "Reproducir", "Pausar o reproducir", () => MediaCommand(0), true); toggle.IsEnabled = mediaCanToggle; buttons.Children.Add(toggle);
            var next = Ui.Button(Ui.Glyph("\uE893", 18), "Pista siguiente", () => MediaCommand(1)); next.IsEnabled = mediaCanNext; buttons.Children.Add(next); body.Children.Add(buttons);
        }
        else body.Children.Add(Ui.Text("Abre una aplicaci\u00f3n que publique su sesi\u00f3n multimedia en Windows.", 12, Ui.MutedColor));
        body.Children.Add(Ui.Button("Actualizar", "Actualizar reproducci\u00f3n", () => { _ = RefreshMediaAsync(true); }));
        ShowPage("music", "Lo que suena", "MULTIMEDIA DE WINDOWS", body);
        livePanelUpdate = timelineUpdate;
    }
}
