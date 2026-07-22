using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TasbihCounter;

/// <summary>
/// Renders the app's own UI to PNG files for documentation (`--shot [dir]`).
///
/// Why the app screenshots itself: the HUD is a layered, DWM-composited window,
/// which external capture cannot see — BitBlt and CopyFromScreen omit it
/// entirely and PrintWindow returns a black frame. Rendering the live visual
/// tree through RenderTargetBitmap sidesteps all of that, and captures exactly
/// what WPF draws: the frosted backdrop, naqsh overlay, text and effects.
/// </summary>
internal static class ScreenshotMode
{
    /// <summary>Render at 2x so the images stay crisp on high-DPI displays.</summary>
    private const double Dpi = 192;

    /// <summary>Render a visual to a PNG at <paramref name="width"/>x<paramref name="height"/> DIPs.</summary>
    private static void Render(Visual visual, double width, double height, string path)
    {
        double scale = Dpi / 96.0;
        int w = (int)Math.Ceiling(width * scale);
        int h = (int)Math.Ceiling(height * scale);
        if (w <= 0 || h <= 0) return;

        var rtb = new RenderTargetBitmap(w, h, Dpi, Dpi, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>Render an element to a PNG, transparency preserved.</summary>
    public static void Save(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        Render(element, element.ActualWidth, element.ActualHeight, path);
    }

    /// <summary>
    /// Render a whole window. Rendering <c>window.Content</c> instead would drop
    /// the window's own Background (the dark theme) and clip anything laid out
    /// beyond the content element's measured width.
    /// </summary>
    public static void SaveWindow(Window window, string path)
    {
        window.UpdateLayout();

        // Window.ActualHeight counts the title bar and borders, which aren't part
        // of the rendered client area — sizing by it leaves a blank strip at the
        // bottom. The descendant bounds are exactly what actually gets drawn.
        var bounds = VisualTreeHelper.GetDescendantBounds(window);
        double w = bounds.Width > 0 ? bounds.Width : window.ActualWidth;
        double h = bounds.Height > 0 ? bounds.Height : window.ActualHeight;

        Render(window, w, h, path);
    }

    /// <summary>
    /// Drive the HUD and Settings window through a short sequence, saving each.
    /// Steps are chained on a timer so layout, the fade-in, and the backdrop
    /// capture have all settled before anything is rendered.
    /// </summary>
    public static void Run(AppConfig config, string outputDir, Action onDone)
    {
        Directory.CreateDirectory(outputDir);

        var hud = new HudWindow
        {
            Position = HudPosition.TopRight,
            Size = HudSize.Medium,
            HoldTime = TimeSpan.FromSeconds(30), // don't fade mid-capture
        };

        var steps = new Queue<Action>();

        // 1. HUD mid-count, using the first configured dhikr.
        var first = config.Adhkar.FirstOrDefault();
        steps.Enqueue(() => hud.Flash(first?.Label ?? "سُبْحَانَ اللّٰه", 33,
                                      first?.Color ?? Color.FromRgb(0x66, 0xE0, 0xA3)));
        steps.Enqueue(() => Save((FrameworkElement)hud.Content,
                                 Path.Combine(outputDir, "hud.png")));

        // 2. The same HUD showing a different dhikr, to show the color coding.
        var third = config.Adhkar.Skip(2).FirstOrDefault();
        steps.Enqueue(() => hud.Flash(third?.Label ?? "اللّٰهُ أَكْبَر", 7,
                                      third?.Color ?? Color.FromRgb(0xF0, 0xC6, 0x74)));
        steps.Enqueue(() => Save((FrameworkElement)hud.Content,
                                 Path.Combine(outputDir, "hud-alt.png")));

        // 3. The settings window.
        SettingsWindow? settings = null;
        steps.Enqueue(() =>
        {
            settings = new SettingsWindow(config, () => { })
            {
                // Keep it off-screen: this is a render, not a window to look at.
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000,
                ShowInTaskbar = false,
            };
            settings.Show();
        });
        steps.Enqueue(() =>
        {
            if (settings is not null)
                SaveWindow(settings, Path.Combine(outputDir, "settings.png"));
            settings?.Close();
        });

        steps.Enqueue(() => { hud.Close(); onDone(); });

        // 350ms between steps: comfortably past the 90ms fade-in and layout.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        timer.Tick += (_, _) =>
        {
            if (steps.Count == 0) { timer.Stop(); return; }
            steps.Dequeue()();
        };
        timer.Start();
    }
}
