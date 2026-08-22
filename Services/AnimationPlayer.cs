using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FBee.Services;

public sealed class AnimationPlayer
{
    private readonly Image target;
    private readonly DispatcherTimer timer = new();
    private readonly string root = AppContext.BaseDirectory;
    private string[] frames = [];
    private int index;
    private bool loop;

    public AnimationPlayer(Image target)
    {
        this.target = target;
        timer.Tick += (_, _) => Advance();
    }

    public bool Play(string name, bool repeat = true, int fps = 12)
    {
        var folder = Path.Combine(root, name);
        if (!Directory.Exists(folder)) return false;
        var nextFrames = Directory.GetFiles(folder, "*.png")
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
        if (nextFrames.Length == 0) return false;
        frames = nextFrames;
        index = 0;
        loop = repeat;
        timer.Interval = TimeSpan.FromSeconds(1d / Math.Clamp(fps, 1, 60));
        ShowCurrentFrame();
        timer.Start();
        return true;
    }

    public void Stop() => timer.Stop();

    private void Advance()
    {
        if (frames.Length == 0) return;
        index++;
        if (index >= frames.Length)
        {
            if (!loop) { index = frames.Length - 1; timer.Stop(); }
            else index = 0;
        }
        ShowCurrentFrame();
    }

    private void ShowCurrentFrame()
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(frames[index], UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 512;
        bitmap.EndInit();
        bitmap.Freeze();
        target.Source = bitmap;
    }
}
