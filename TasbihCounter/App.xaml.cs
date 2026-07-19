using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace TasbihCounter;

/// <summary>
/// Entry point. Runs in the tray with no main window and counts adhkar from bare
/// modifier taps — one dhikr per key (Alt / Ctrl / Shift by default).
///
/// A "tap" is the modifier pressed and released alone; using it in a combo
/// (Ctrl+C, Alt+Tab, …) does NOT count. See <see cref="ModifierTapHook"/>.
/// Counts are per-dhikr and in memory only (reset on relaunch, per v1).
/// </summary>
public partial class App : Application
{
    private const uint VK_Q = 0x51;
    private const uint VK_P = 0x50;

    private ModifierTapHook? _tapHook;
    private GlobalHotkey? _quitHotkey;
    private GlobalHotkey? _toggleHotkey;
    private WinForms.NotifyIcon? _tray;
    private WinForms.ToolStripMenuItem? _toggleItem;
    private HudWindow? _hud;
    private SettingsWindow? _settings;

    /// <summary>
    /// Master switch for the tap shortcuts. Deliberately not persisted: the app
    /// always starts counting, so a pause set long ago can't leave someone
    /// wondering why taps do nothing.
    /// </summary>
    private bool _countingEnabled = true;

    private AppConfig _config = AppConfig.Defaults();
    private readonly Dictionary<TapKey, int> _counts = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _config = ConfigStore.Load();
        _hud = new HudWindow();
        ApplyConfigToHud();

        try
        {
            _tapHook = new ModifierTapHook();
            _tapHook.Tapped += OnDhikrTapped;

            // Always-available escape hatch. Windows 11 hides new tray icons in
            // the overflow flyout, so quitting must not depend on finding one.
            _quitHotkey = new GlobalHotkey(1,
                GlobalHotkey.Modifiers.Control | GlobalHotkey.Modifiers.Alt, VK_Q);
            _quitHotkey.Pressed += () => Shutdown();

            // Pause/resume counting without hunting for the tray icon.
            _toggleHotkey = new GlobalHotkey(2,
                GlobalHotkey.Modifiers.Control | GlobalHotkey.Modifiers.Alt, VK_P);
            _toggleHotkey.Pressed += () => SetCounting(!_countingEnabled);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Tasbih Counter — startup error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        SetupTray();

        // --demo pins the HUD on screen so its appearance can be inspected or
        // screenshotted without racing the fade-out timer.
        if (e.Args.Contains("--demo"))
        {
            // Pinned long enough to inspect, but self-terminating: the HUD is
            // click-through, so it must never outlive an obvious way to close it.
            _hud.HoldTime = TimeSpan.FromSeconds(30);
            _hud.Flash("سُبْحَانَ اللّٰه", 33, Color.FromRgb(0x66, 0xE0, 0xA3));

            var demoExit = new DispatcherTimer { Interval = TimeSpan.FromSeconds(32) };
            demoExit.Tick += (_, _) => { demoExit.Stop(); Shutdown(); };
            demoExit.Start();
            return;
        }

        _hud.Flash("Tasbih Counter is running", 0, Color.FromRgb(0x9A, 0xA0, 0xA6));
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        using var s = typeof(App).Assembly.GetManifestResourceStream("TasbihCounter.icon.ico");
        return s is not null ? new System.Drawing.Icon(s) : System.Drawing.SystemIcons.Application;
    }

    private void SetupTray()
    {
        var menu = new WinForms.ContextMenuStrip();

        // CheckOnClick flips Checked before Click fires, so it already holds the
        // new state by the time we read it.
        _toggleItem = new WinForms.ToolStripMenuItem("Counting enabled")
        {
            Checked = _countingEnabled,
            CheckOnClick = true,
        };
        _toggleItem.Click += (_, _) => SetCounting(_toggleItem.Checked);
        menu.Items.Add(_toggleItem);

        menu.Items.Add("Open Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Reset counts", null, (_, _) => ResetCounts());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Shutdown());

        _tray = new WinForms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Visible = true,
            Text = "Tasbih Counter",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowSettings();
    }

    private void ApplyConfigToHud()
    {
        if (_hud is null) return;
        _hud.Position = _config.Position;
        _hud.Size = _config.Size;
        _hud.HoldTime = TimeSpan.FromMilliseconds(_config.HoldMs);
    }

    /// <summary>Turn the tap shortcuts on or off and reflect it everywhere.</summary>
    private void SetCounting(bool enabled)
    {
        _countingEnabled = enabled;

        if (_toggleItem is not null) _toggleItem.Checked = enabled;
        if (_tray is not null)
            _tray.Text = enabled ? "Tasbih Counter" : "Tasbih Counter — paused";

        _hud?.Flash(
            enabled ? "Counting enabled" : "Counting paused",
            0,
            enabled ? Color.FromRgb(0x66, 0xE0, 0xA3) : Color.FromRgb(0x9A, 0xA0, 0xA6));
    }

    private void OnDhikrTapped(TapKey key)
    {
        if (!_countingEnabled) return;

        var dhikr = _config.Adhkar.FirstOrDefault(d => d.Key == key && d.Enabled);
        if (dhikr is null) return;

        _counts.TryGetValue(key, out int count);
        _counts[key] = ++count;

        // The hook fires on the UI thread, but marshal defensively.
        Dispatcher.Invoke(() => _hud?.Flash(dhikr.Label, count, dhikr.Color));
    }

    private void ShowSettings()
    {
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }

        _settings = new SettingsWindow(_config, ResetCounts);
        _settings.Saved += cfg =>
        {
            _config = cfg;
            ConfigStore.Save(_config);
            ApplyConfigToHud();
        };
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
        _settings.Activate();
    }

    private void ResetCounts()
    {
        _counts.Clear();
        _hud?.Flash("Counts reset", 0, Color.FromRgb(0x9A, 0xA0, 0xA6));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tapHook?.Dispose();
        _quitHotkey?.Dispose();
        _toggleHotkey?.Dispose();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        base.OnExit(e);
    }
}
