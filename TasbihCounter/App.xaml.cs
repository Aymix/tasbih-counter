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
    private ModifierTapHook? _tapHook;
    private WinForms.NotifyIcon? _tray;
    private HudWindow? _hud;
    private SettingsWindow? _settings;

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
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Tasbih Counter — startup error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        SetupTray();
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

    private void OnDhikrTapped(TapKey key)
    {
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
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        base.OnExit(e);
    }
}
