using System.IO;
using System.Windows.Media;

namespace FBee.Services;

/// <summary>
/// Plays short voice clips with overlap. The four phoeba clips are reserved
/// for dragging; all other interactions choose from the complete audio pool.
/// </summary>
public sealed class AudioPlayerService : IDisposable
{
    private const int DefaultMaxSimultaneous = 4;
    private readonly string audioRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "audio");
    private readonly List<Playback> active = [];
    private readonly Random random = new();
    private bool disposed;

    public int MaxSimultaneous { get; set; } = DefaultMaxSimultaneous;
    public double Volume { get; set; } = 1.0;

    public void PlayRandomVoice()
    {
        PlayFrom(GetAllAudioFiles());
    }

    public void PlayDragVoice()
    {
        // Supports the current "phoeba" spelling and the user's "phoepa"
        // spelling, while keeping the four dedicated drag clips isolated.
        var files = GetAllAudioFiles()
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return name.StartsWith("phoeba_", StringComparison.OrdinalIgnoreCase) ||
                       name.StartsWith("phoepa_", StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();
        PlayFrom(files.Length > 0 ? files : GetAllAudioFiles());
    }

    public void StopAll()
    {
        foreach (var playback in active.ToArray()) Remove(playback);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        StopAll();
    }

    private string[] GetAllAudioFiles()
    {
        if (!Directory.Exists(audioRoot)) return [];
        return Directory.GetFiles(audioRoot, "*.*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var extension = Path.GetExtension(path);
                return extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
                       extension.Equals(".wav", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void PlayFrom(string[] files)
    {
        if (disposed || files.Length == 0) return;
        var limit = Math.Max(1, MaxSimultaneous);
        while (active.Count >= limit) Remove(active[0]);

        var player = new MediaPlayer { Volume = Math.Clamp(Volume, 0, 1) };
        var playback = new Playback(player);
        player.MediaOpened += (_, _) => player.Play();
        player.MediaEnded += (_, _) => Remove(playback);
        player.MediaFailed += (_, _) => Remove(playback);
        active.Add(playback);
        player.Open(new Uri(files[random.Next(files.Length)], UriKind.Absolute));
    }

    private void Remove(Playback playback)
    {
        if (!active.Remove(playback)) return;
        playback.Player.MediaEnded -= (_, _) => Remove(playback);
        playback.Player.MediaFailed -= (_, _) => Remove(playback);
        playback.Player.Close();
    }

    private sealed record Playback(MediaPlayer Player);
}
