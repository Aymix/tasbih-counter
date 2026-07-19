# Tasbih Counter (Windows)

**Date**: 2026-07-18

## The idea
A lightweight Windows tally counter for adhkar. Each dhikr (subḥān Allāh, Allāhu akbar, …) is bound to its own global hotkey. Pressing that key anywhere — even while another app is focused — increments that dhikr's count and briefly shows a small floating HUD in a corner of the screen. It works like a multi-button physical clicker you never have to look for.

## Problem and user
- **User**: primarily the author (personal tool), reciting short adhkar during the day while using the PC.
- **Problem**: counting adhkar with a phone app, physical clicker, or in your head means breaking focus or picking up another device. A keyboard-native counter keeps hands on the keyboard and eyes on whatever you're doing.
- **Today's alternatives**: phone tasbih apps, physical clickers, sticky notes — all require a context switch away from the screen.

## Main use case
1. Launch the app (lives in the system tray, no main window in the way).
2. Configured adhkar each have a dedicated global hotkey.
3. Press a dhikr's hotkey → its count goes up by 1 → a small HUD pops up showing that dhikr's label + big count, then fades.
4. Press a different dhikr's hotkey → that one increments and its HUD shows instead.
5. Open Settings to change adhkar, colors, hotkeys, HUD position, and size.

## v1 scope
**In:**
- **One global hotkey per dhikr** (works system-wide, while any app is focused).
- **Preset adhkar list** to pick from (common adhkar with Arabic + transliteration), plus custom entries.
- **Per-dhikr color** and text label.
- **Floating HUD** showing only the dhikr just pressed (single large count), then fades out.
- **HUD position**: 9 screen anchors — top-left, top-middle, top-right, middle-left, center, middle-right, bottom-left, bottom-middle, bottom-right.
- **HUD size**: small / medium / large.
- **Settings window** for all of the above.
- **System tray** presence (show/hide, quit, open settings).
- Arabic script rendering in the HUD and settings.

**Out (v1):**
- No targets / no auto-advance / no target cue (pure count-up).
- No count persistence across launches — counts reset to 0 each launch (the dhikr list, colors, hotkeys, position, and size *do* persist).
- No stats, history, streaks, or daily logs.
- No cloud sync, accounts, or mobile version.
- No "all dhikr stacked" HUD view (only the last-pressed dhikr is shown).

## Technical shape
- **Platform**: Windows.
- **Stack**: C# / .NET, WPF UI.
- **Global hotkeys**: Win32 low-level keyboard hook (`WH_KEYBOARD_LL`) or `RegisterHotKey`, so keys fire regardless of focused app.
- **HUD**: topmost, borderless, transparent, click-through WPF window positioned to the chosen screen anchor; fade in/out animation and auto-dismiss timer.
- **Persistence**: local config file (JSON) for the dhikr list, colors, hotkeys, HUD position, and size. Counts held in memory only.
- **Distribution**: single small `.exe` (self-contained build).

## Constraints
- **Timeline**: not stated — treat as a small personal build, ship a working v1 quickly.
- **Budget**: zero-cost; no hosting or services needed (fully local).
- **Team**: solo.

## Success signal
You can sit at your PC, recite adhkar, and count them entirely from the keyboard without ever switching apps or looking away from what you're doing.

## Open questions / risks
- **Hotkey choice conflict**: bare `Alt` and `Ctrl` are modifier keys the OS uses constantly, so using them alone as counter keys will clash with normal typing/shortcuts. Likely need dedicated non-modifier keys (e.g. `F13–F24`, NumPad keys, or `Ctrl+Alt+<key>` combos) or make the binding fully user-configurable with conflict detection. **This is the main design decision to resolve before building.**
- **Multiple presets on one key**: confirm whether two adhkar could ever share a key (probably disallow — one key, one dhikr).
- **HUD fade timing**: how long the HUD stays before fading, and whether rapid repeated presses keep it alive / restart the timer.
- **Arabic rendering**: pick a font that renders adhkar cleanly at large HUD sizes (e.g. an Amiri/Scheherazade-style font, or a good system fallback).
- **Preset list contents**: which adhkar ship by default (subḥān Allāh, al-ḥamdu lillāh, Allāhu akbar, lā ilāha illā Allāh, astaghfiru-llāh, …).
