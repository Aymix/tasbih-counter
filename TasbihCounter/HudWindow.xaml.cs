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
/// and fades away. Reused for every press (one window, re-shown).
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

    private const int EdgeGap = 24; // gap from the screen edge, in DIPs

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

        if (!IsVisible)
            Show();

        // Recompute position after content/scale changes size.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Reposition));

        FadeIn();
        _dismissTimer.Stop();
        _dismissTimer.Interval = HoldTime;
        _dismissTimer.Start();
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
        var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(90));
        BeginAnimation(OpacityProperty, anim);
    }

    private void FadeOut()
    {
        var anim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(320));
        BeginAnimation(OpacityProperty, anim);
    }
}
