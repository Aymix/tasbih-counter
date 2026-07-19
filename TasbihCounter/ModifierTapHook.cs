using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TasbihCounter;

/// <summary>Which bare modifier was tapped.</summary>
public enum TapKey { Alt, Ctrl, Shift }

/// <summary>
/// A system-wide low-level keyboard hook (WH_KEYBOARD_LL) that detects a *lone
/// tap* of Alt, Ctrl, or Shift — the modifier pressed and released with no other
/// key in between. Combos (Ctrl+C, Alt+Tab, Ctrl+Shift+…) never fire a tap, so
/// normal typing keeps working.
///
/// Why a hook instead of RegisterHotKey: Windows won't register a bare modifier
/// as a hotkey. To count a solo tap we must watch the raw key stream.
///
/// Note: taps are NOT swallowed, so a lone Alt tap will also nudge the focused
/// window's menu bar (standard Windows behavior). Suppressing that cleanly is a
/// known follow-up.
/// </summary>
public sealed class ModifierTapHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4; // Left Alt
    private const int VK_RMENU = 0xA5; // Right Alt

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    // Keep the delegate alive for the hook's lifetime (else it gets GC'd).
    private readonly HookProc _proc;
    private IntPtr _hook;

    // Per-modifier tap-detection state, indexed by (int)TapKey.
    private readonly bool[] _down = new bool[3];
    private readonly bool[] _consumed = new bool[3];

    /// <summary>Raised on a lone modifier tap. Fires on the hook thread.</summary>
    public event Action<TapKey>? Tapped;

    public ModifierTapHook()
    {
        _proc = HookCallback;
        using var module = Process.GetCurrentProcess().MainModule!;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Failed to install keyboard hook (Win32 error {Marshal.GetLastWin32Error()}).");
    }

    private static TapKey? Classify(uint vk) => vk switch
    {
        VK_LMENU or VK_RMENU => TapKey.Alt,
        VK_LCONTROL or VK_RCONTROL => TapKey.Ctrl,
        VK_LSHIFT or VK_RSHIFT => TapKey.Shift,
        _ => null,
    };

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            uint vk = ((KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT))!).vkCode;

            if (msg is WM_KEYDOWN or WM_SYSKEYDOWN)
                OnKeyDown(vk);
            else if (msg is WM_KEYUP or WM_SYSKEYUP)
                OnKeyUp(vk);
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void OnKeyDown(uint vk)
    {
        var k = Classify(vk);
        if (k is TapKey mod)
        {
            int mi = (int)mod;
            // Any other modifier already held? Then this is a modifier combo:
            // consume the held ones AND this one (no tap for either).
            bool anyOtherDown = false;
            for (int i = 0; i < 3; i++)
                if (i != mi && _down[i]) { _consumed[i] = true; anyOtherDown = true; }

            if (!_down[mi]) { _down[mi] = true; _consumed[mi] = anyOtherDown; }
        }
        else
        {
            // A non-modifier key => every held modifier was used as a modifier.
            for (int i = 0; i < 3; i++)
                if (_down[i]) _consumed[i] = true;
        }
    }

    private void OnKeyUp(uint vk)
    {
        if (Classify(vk) is not TapKey mod) return;
        int mi = (int)mod;
        if (!_down[mi]) return;

        _down[mi] = false;
        if (!_consumed[mi]) Tapped?.Invoke(mod);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
