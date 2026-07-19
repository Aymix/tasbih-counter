using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TasbihCounter;

/// <summary>Where on the primary screen the HUD appears.</summary>
public enum HudPosition
{
    TopLeft, TopCenter, TopRight,
    MiddleLeft, Center, MiddleRight,
    BottomLeft, BottomCenter, BottomRight,
}

/// <summary>Relative HUD scale.</summary>
public enum HudSize { Small, Medium, Large }

/// <summary>
/// A borderless, topmost, click-through HUD that flashes the current dhikr count
/// on a frosted-glass card, then fades away.
///
/// The frost is done by hand: Windows 11 dropped supported per-window blur (the
/// accent-policy API silently stopped blurring, and the DWM system backdrop
/// rendered opaque here), so the HUD snapshots the screen behind itself, blurs
/// that image in WPF, and paints it as its background. Because the HUD is
/// stationary and only on screen for about a second, a still frame is
/// indistinguishable from a live backdrop.
/// </summary>
public partial class HudWindow : Window
{
    // Extended window style bits that make the HUD ignore mouse input and never
    // steal focus from whatever the user is actually working in.
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private const int EdgeGap = 24;          // gap from the screen edge, in DIPs
    private const double CornerRadius = 16;  // must match the Card in XAML

    /// <summary>
    /// Extra screen captured around the HUD, in DIPs. A blur samples beyond its
    /// source edges, so without this bleed the card would have dark rims.
    /// </summary>
    private const double Bleed = 32;

    private readonly DispatcherTimer _dismissTimer;

    public HudPosition Position { get; set; } = HudPosition.TopRight;
    public HudSize Size { get; set; } = HudSize.Medium;

    /// <summary>How long the HUD stays fully visible before fading out.</summary>
    public TimeSpan HoldTime { get; set; } = TimeSpan.FromMilliseconds(900);

    public HudWindow()
    {
        InitializeComponent();
        _dismissTimer = new DispatcherTimer { Interval = HoldTime };
        _dismissTimer.Tick += (_, _) =>
        {
            _dismissTimer.Stop();
            FadeOut();
        };

        SourceInitialized += (_, _) => ApplyClickThroughStyles();
    }

    private void ApplyClickThroughStyles()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);
    }

    /// <summary>Update the HUD for a dhikr press and flash it into view.</summary>
    /// <param name="label">Dhikr text (may be Arabic).</param>
    /// <param name="count">Current count for this dhikr.</param>
    /// <param name="accent">Accent color for the label.</param>
    public void Flash(string label, int count, Color accent)
    {
        LabelText.Text = label;
        LabelText.Foreground = new SolidColorBrush(accent);
        CountText.Text = count.ToString();
        ApplyScale();

        // Only re-snapshot when coming back from invisible. Re-capturing while
        // already on screen would photograph our own card and feed it back.
        bool needsBackdrop = !IsVisible || Opacity < 0.05;

        if (!IsVisible)
            Show(); // still at Opacity 0 — nothing is drawn yet

        // Everything below needs a measured size, which only exists after layout.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            Reposition();
            UpdateClip();
            if (needsBackdrop) CaptureBackdrop();

            FadeIn();
            _dismissTimer.Stop();
            _dismissTimer.Interval = HoldTime;
            _dismissTimer.Start();
        }));
    }

    /// <summary>Snapshot the screen behind the HUD and use it as the frosted backdrop.</summary>
    private void CaptureBackdrop()
    {
        var dpi = VisualTreeHelper.GetDpi(this);

        int x = (int)Math.Round((Left - Bleed) * dpi.DpiScaleX);
        int y = (int)Math.Round((Top - Bleed) * dpi.DpiScaleY);
        int w = (int)Math.Round((ActualWidth + Bleed * 2) * dpi.DpiScaleX);
        int h = (int)Math.Round((ActualHeight + Bleed * 2) * dpi.DpiScaleY);

        // The Rectangle's negative margin (set in XAML to -Bleed) already pulls
        // the oversized snapshot past the clip so its blurred edges stay hidden.
        BackdropBrush.ImageSource = ScreenCapture.Capture(x, y, w, h);
    }

    /// <summary>A Border won't clip children to its CornerRadius; do it manually.</summary>
    private void UpdateClip()
    {
        if (CardRoot.ActualWidth <= 0 || CardRoot.ActualHeight <= 0) return;

        CardRoot.Clip = new RectangleGeometry(
            new Rect(0, 0, CardRoot.ActualWidth, CardRoot.ActualHeight),
            CornerRadius - 1, CornerRadius - 1); // -1 keeps it inside the 1px border
    }

    private void ApplyScale()
    {
        double labelSize, countSize;
        switch (Size)
        {
            case HudSize.Small: labelSize = 16; countSize = 48; break;
            case HudSize.Large: labelSize = 30; countSize = 104; break;
            default: labelSize = 22; countSize = 72; break; // Medium
        }
        LabelText.FontSize = labelSize;
        CountText.FontSize = countSize;
    }

    private void Reposition()
    {
        var wa = SystemParameters.WorkArea; // primary screen, excludes taskbar
        double w = ActualWidth;
        double h = ActualHeight;

        double left = Position switch
        {
            HudPosition.TopLeft or HudPosition.MiddleLeft or HudPosition.BottomLeft
                => wa.Left + EdgeGap,
            HudPosition.TopCenter or HudPosition.Center or HudPosition.BottomCenter
                => wa.Left + (wa.Width - w) / 2,
            _ => wa.Right - w - EdgeGap, // right column
        };

        double top = Position switch
        {
            HudPosition.TopLeft or HudPosition.TopCenter or HudPosition.TopRight
                => wa.Top + EdgeGap,
            HudPosition.MiddleLeft or HudPosition.Center or HudPosition.MiddleRight
                => wa.Top + (wa.Height - h) / 2,
            _ => wa.Bottom - h - EdgeGap, // bottom row
        };

        Left = left;
        Top = top;
    }

    private void FadeIn()
    {
        BeginAnimation(OpacityProperty, null);
        BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(90)));
    }

    private void FadeOut()
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(320)));
    }
}
