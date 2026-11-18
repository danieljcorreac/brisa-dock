using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.Media;
using Windows.Media.Control;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

var directory = Path.Combine(Path.GetTempPath(), "BrisaMediaProbe-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
string wave = Path.Combine(directory, "silence.wav");
using (var file = new BinaryWriter(File.Create(wave)))
{
    const int sampleRate = 8000, seconds = 30, bytes = sampleRate * seconds * 2;
    file.Write("RIFF"u8.ToArray()); file.Write(36 + bytes); file.Write("WAVEfmt "u8.ToArray()); file.Write(16); file.Write((short)1); file.Write((short)1); file.Write(sampleRate); file.Write(sampleRate * 2); file.Write((short)2); file.Write((short)16); file.Write("data"u8.ToArray()); file.Write(bytes); file.Write(new byte[bytes]);
}
try
{
    SetCurrentProcessExplicitAppUserModelID("Brisa.Dock.MediaProbe");
    using var player = new MediaPlayer { Volume = 0, IsMuted = true, AutoPlay = false };
    player.Source = MediaSource.CreateFromStorageFile(await StorageFile.GetFileFromPathAsync(wave));
    player.CommandManager.IsEnabled = true;
    var controls = player.SystemMediaTransportControls; controls.IsEnabled = true; controls.IsPlayEnabled = true; controls.IsPauseEnabled = true;
    controls.DisplayUpdater.Type = MediaPlaybackType.Music; controls.DisplayUpdater.MusicProperties.Title = "Brisa · Verificación silenciosa"; controls.DisplayUpdater.MusicProperties.Artist = "Prueba local"; controls.DisplayUpdater.Update();
    player.Play();
    var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    GlobalSystemMediaTransportControlsSession? session = null;
    for (int attempt = 0; attempt < 30 && session == null; attempt++)
    {
        foreach (var candidate in manager.GetSessions())
        {
            var properties = await candidate.TryGetMediaPropertiesAsync();
            if (properties.Title == "Brisa · Verificación silenciosa") { session = candidate; break; }
        }
        if (session == null) await Task.Delay(300);
    }
    if (session == null) throw new InvalidOperationException("Windows no publicó la sesión de prueba.");
    bool pauseAccepted = await session.TryPauseAsync();
    await Task.Delay(500); bool paused = player.PlaybackSession.PlaybackState == MediaPlaybackState.Paused;
    bool playAccepted = await session.TryPlayAsync(); await Task.Delay(500); bool playing = player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
    bool seekAccepted = await session.TryChangePlaybackPositionAsync(TimeSpan.FromSeconds(5).Ticks); await Task.Delay(500);
    double position = player.PlaybackSession.Position.TotalSeconds;
    bool toggleAccepted = await session.TryTogglePlayPauseAsync(); await Task.Delay(400); bool togglePaused = player.PlaybackSession.PlaybackState == MediaPlaybackState.Paused;
    var report = new { SessionFound = true, PauseAccepted = pauseAccepted, Paused = paused, PlayAccepted = playAccepted, Playing = playing, SeekAccepted = seekAccepted, PositionSeconds = position, ToggleAccepted = toggleAccepted, TogglePaused = togglePaused, DurationSeconds = session.GetTimelineProperties().EndTime.TotalSeconds, Silent = player.IsMuted && player.Volume == 0 };
    Console.WriteLine(JsonSerializer.Serialize(report));
    player.Pause();
    return pauseAccepted && paused && playAccepted && playing && seekAccepted && position is >= 4 and <= 8 && toggleAccepted && togglePaused ? 0 : 1;
}
catch (Exception e) { Console.WriteLine(JsonSerializer.Serialize(new { Error = e.Message, Artifacts = directory })); return 1; }
finally { if (File.Exists(wave)) File.Delete(wave); }

[DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string id);
