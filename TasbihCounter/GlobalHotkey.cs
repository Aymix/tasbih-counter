using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace TasbihCounter;

/// <summary>
/// Registers a system-wide hotkey combination via RegisterHotKey. Used for the
/// quit shortcut, which must work even when the tray icon is hidden in Windows
/// 11's overflow flyout (where new tray icons land by default).
///
/// This is for combinations only — bare modifier taps go through
/// <see cref="ModifierTapHook"/>, since Windows won't register a lone modifier.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    [Flags]
    public enum Modifiers : uint
    {
        None = 0x0000,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
        NoRepeat = 0x4000,
    }

    private const int WM_HOTKEY = 0x0312;
    private const int HWND_MESSAGE = -3;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly int _id;
    private bool _registered;

    /// <summary>Raised on the UI thread each time the hotkey is pressed.</summary>
    public event Action? Pressed;

    public GlobalHotkey(int id, Modifiers modifiers, uint virtualKey)
    {
        _id = id;

        // A message-only window: no UI, just a target for WM_HOTKEY.
        var parameters = new HwndSourceParameters("TasbihHotkeyWindow")
        {
            ParentWindow = new IntPtr(HWND_MESSAGE),
            WindowStyle = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        _registered = RegisterHotKey(_source.Handle, _id,
            (uint)(modifiers | Modifiers.NoRepeat), virtualKey);

        if (!_registered)
            throw new InvalidOperationException(
                $"RegisterHotKey failed (Win32 error {Marshal.GetLastWin32Error()}). " +
                "The combination is probably already taken by another app.");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_source.Handle, _id);
            _registered = false;
        }
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
