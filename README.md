<div align="center">

<img src="TasbihCounter/icon-preview.png" width="120" alt="Tasbih Counter icon">

# Tasbih Counter

**A keyboard-native tasbih (dhikr) counter for Windows.**

Tap a modifier key anywhere — even while another app is focused — and a small HUD
flashes your count, then fades away.

<br>

<img src="docs/screenshots/hud.png" width="200" alt="HUD showing Subhan Allah at 33">
&nbsp;&nbsp;
<img src="docs/screenshots/hud-alt.png" width="200" alt="HUD showing Allahu akbar at 7">

<sub>The HUD: frosted glass with a naqsh overlay, color-coded per dhikr.</sub>

</div>

---

## Why

Counting adhkar with a phone app or a physical clicker means breaking focus and
picking up another device. This keeps your hands on the keyboard and your eyes on
whatever you're doing.

## How it works

Each dhikr is bound to a bare modifier key. A **tap** — pressing and releasing the
key alone — counts one.

| Key | Dhikr | Default color |
|-----|-------|---------------|
| Tap `Alt` | سُبْحَانَ اللّٰه (SubhanAllah) | green |
| Tap `Ctrl` | اَلْحَمْدُ لِلّٰه (Alhamdulillah) | blue |
| Tap `Shift` | اللّٰهُ أَكْبَر (Allahu akbar) | gold |

Using a modifier in a **combo** — `Ctrl+C`, `Alt+Tab`, `Ctrl+Shift+Esc` — never
counts, so normal typing is untouched.

### Other shortcuts

| Keys | Action |
|------|--------|
| `Ctrl+Alt+P` | Pause / resume counting (also in the tray menu) |
| `Ctrl+Alt+Q` | Quit |

Pausing is handy during heavy typing, when a stray solo tap could miscount. It
always starts **enabled** on launch — the pause is not persisted, so it can
never silently swallow your counts after a restart.

## Features

- **Global** — counts while any app is focused (low-level keyboard hook)
- **Floating HUD** — borderless, always-on-top, click-through; fades out
- **9 screen positions** — any corner, edge-center, or center
- **3 sizes** — small / medium / large
- **Per-dhikr label and color**, editable in settings
- **Pause / resume** — `Ctrl+Alt+P` or the tray menu, to stop accidental counts
- **Tray icon** — pause, open settings, reset counts, quit
- **Settings persist** to `%AppData%\TasbihCounter\config.json`

Counts are in memory only and reset on relaunch — each launch is a fresh session.

## Settings

<img src="docs/screenshots/settings.png" width="420" alt="Settings window">

Reachable from the tray icon. Edit each dhikr's text and color, pick one of nine
HUD positions, choose a size, and set how long the HUD stays on screen.

## Requirements

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) to run
- .NET 8 SDK to build

## Build

```bash
dotnet build TasbihCounter/TasbihCounter.csproj -c Debug
```

Publish a standalone single-file exe:

```bash
dotnet publish TasbihCounter/TasbihCounter.csproj -c Release -r win-x64 \
  --self-contained false -p:PublishSingleFile=true -o out
```

Then run `out/TasbihCounter.exe`. It starts in the tray with no main window.

## Project layout

| File | Role |
|------|------|
| `App.xaml.cs` | Entry point; tray icon, config, wiring |
| `ModifierTapHook.cs` | `WH_KEYBOARD_LL` hook; lone-tap detection |
| `HudWindow.xaml(.cs)` | The floating count HUD |
| `SettingsWindow.xaml(.cs)` | Settings UI |
| `Config.cs` | JSON config model, load/save |
| `make_icon.py` | Regenerates `icon.ico` (Pillow) |
| `make_naqsh.py` | Regenerates the naqsh tile, PNG + SVG (Pillow) |
| `ScreenshotMode.cs` | `--shot` renders the UI to PNGs for these docs |

### Regenerating the screenshots

```bash
TasbihCounter.exe --shot docs/screenshots
```

The app renders its own UI rather than being captured externally: the HUD is a
layered, DWM-composited window that screen capture cannot see — `BitBlt` and
`CopyFromScreen` omit it, and `PrintWindow` returns a black frame.
`RenderTargetBitmap` draws the live visual tree instead.

## Known quirks

- A lone `Alt` tap also nudges the focused window's menu bar — standard Windows
  behavior for a solo Alt. Suppressing it cleanly is a planned follow-up.
- If you habitually tap a modifier by itself, it will count.

## Roadmap

- Suppress the Alt menu-bar side effect
- Optional per-dhikr targets (e.g. 33) with a cue
- Preset adhkar picker with more built-in options
- Optional run-at-login
