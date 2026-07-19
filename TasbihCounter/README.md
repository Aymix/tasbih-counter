# Tasbih Counter — Hotkey → HUD proof-of-concept

A tiny background Windows app that proves the core loop of the tasbih counter:

**global keypress → in-memory count → floating HUD that fades out.**

No main window, no tray icon yet — that comes next. This PoC exists to confirm
the hard parts work: a system-wide hotkey (fires while any app is focused) and a
borderless, topmost, click-through HUD.

## Hotkeys

One bare modifier **tap** per dhikr (press and release alone — combos like
`Ctrl+C` / `Alt+Tab` don't count):

| Keys         | Action                                              |
|--------------|-----------------------------------------------------|
| Tap `Alt`    | Count سُبْحَانَ اللّٰه (SubhanAllah)                   |
| Tap `Ctrl`   | Count اللّٰهُ أَكْبَر (Allahu akbar)                    |
| `Ctrl+Alt+P` | Pause / resume counting (also in the tray menu)     |
| `Ctrl+Alt+Q` | Quit the app                                        |

Counts are per-dhikr, in memory only, and reset on relaunch (per the v1 decision).

**Caveat:** a lone `Alt` tap also nudges the focused window's menu bar (standard
Windows behavior). Suppressing that cleanly is a known follow-up.

## Requirements

- Windows 10/11
- .NET 8 Desktop Runtime (already installed on this machine) to **run**
- .NET 8 SDK to **build**

## Build & run

From WSL (uses the Windows `dotnet.exe` via interop):

```bash
WINPROJ='\\wsl.localhost\Ubuntu\home\amine\tasbih\TasbihCounter\TasbihCounter.csproj'
dotnet.exe build "$WINPROJ" -c Debug
dotnet.exe run   --project "$WINPROJ"
```

Or from a Windows terminal in this folder:

```powershell
dotnet run
```

Or just double-click the built exe in Explorer:

```
\\wsl.localhost\Ubuntu\home\amine\tasbih\TasbihCounter\bin\Debug\net8.0-windows\TasbihCounter.exe
```

On launch you'll see a "Tasbih ready" flash in the top-right. Then press
`Ctrl+Alt+S` from anywhere and watch the count climb.

## What's here

| File                 | Role                                                        |
|----------------------|-------------------------------------------------------------|
| `App.xaml(.cs)`      | Headless entry point; wires hotkeys to the HUD              |
| `GlobalHotkey.cs`    | `RegisterHotKey` wrapper on a message-only window           |
| `HudWindow.xaml(.cs)`| Borderless topmost click-through HUD; 9 positions, 3 sizes  |
| `app.manifest`       | Per-monitor-v2 DPI awareness for crisp text                 |

`HudWindow` already supports all 9 screen anchors (`HudPosition`) and 3 sizes
(`HudSize`) — the PoC just hardcodes `TopRight` / `Medium` in `App.xaml.cs`.

## Next steps (not in this PoC)

- Multiple adhkar, one hotkey each (preset list + custom)
- System tray icon (show/hide, quit, open settings)
- Settings window: pick adhkar, colors, hotkeys, HUD position & size
- Persist config to JSON (counts stay in-memory per v1)
- Sensible default hotkeys that don't collide with system shortcuts
