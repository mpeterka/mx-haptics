# Contributing to MX Haptics

Bug reports, ideas and pull requests are welcome. For anything bigger than a small fix, open an issue first so we can agree
on the approach.

- Build and run the plugin as described in [BUILDING.md](BUILDING.md).
- Send pull requests against `main`. Keep them focused on one change.
- UI texts, event names, plugin actions and documentation are in English.
- Describe in the pull request how you tested the change. Vibrations can only be checked by hand with an MX Master 4,
  so say what you felt, not only that it builds.

## Project layout

| Path | Contents |
| --- | --- |
| `shared/HapticsSettings.cs` | settings model (`%LOCALAPPDATA%\MxHaptics\settings.json`), reaction and event names; used by the plugin and the window |
| `shared/HapticsStats.cs` | vibration counts in total and per day (`%LOCALAPPDATA%\MxHaptics\stats.json`); written only by the plugin |
| `src/MxHapticsPlugin.cs` | plugin entry point: starts detection, loads settings, raises events, counts vibrations, handles pipe commands |
| `src/PointerWatcher.cs` | detection threads (window borders, screen edge, clicks; element hit-testing) |
| `src/TaskbarButtons.cs` | Windows 11 taskbar buttons through UI Automation (COM interop) |
| `src/Native.cs` | P/Invoke (user32, oleacc) and the MSAA `IAccessible` interface |
| `src/ControlPipeServer.cs` | named pipe `MxHaptics` for the settings window |
| `src/*Command.cs` | Options+ actions *MX Haptics settings* and *Toggle MX Haptics* |
| `src/package/` | package contents: `metadata/` (manifest, icon), `events/` (events and their mapping to vibration patterns) |
| `settings/` | settings window (WinForms); `LogiControls.cs` holds the Options+-style theme and controls |
| `tools/` | Start menu shortcut and icon conversion scripts |

## How it works

- The window thread reads the pointer position every 10 ms. It reports a window border when the root window under the pointer
  changes and stays for at least two samples (filters out flickering tooltips). It reports a screen edge when the neighbouring
  pixel isn't on any monitor.
- The element thread runs every 30 ms (only while the pointer moves and no button is held). It asks MSAA
  (`AccessibleObjectFromPoint`) for the element under the pointer and walks a few parents up. It vibrates when the pointer enters
  a new element with the role of a button, link, tab, menu item, check box, radio button or combo box.
- To MSAA the Windows 11 taskbar is one block without buttons, and UI Automation `ElementFromPoint` doesn't see inside it either.
  While the pointer is over the taskbar (`Shell_TrayWnd`, or `Shell_SecondaryTrayWnd` on other monitors) the plugin loads the
  rectangles of its buttons with a UI Automation `FindAll`, caches them for a second and hit-tests the pointer itself.
- Click: the window thread reads the left and right button state every 10 ms. On a press it vibrates if the element thread last
  found a clickable element under the pointer.
- The SDK can't set a vibration strength. Each reaction therefore has three events (`<reaction>_weak`, `_medium`, `_strong`)
  mapped to the patterns `subtle_collision`, `damp_collision` and `sharp_collision`, and the plugin raises the one picked
  in the window (*Subtle*, *Damp*, *Sharp*).
- The settings window talks to the plugin over the named pipe `MxHaptics` (current user only): `reload` after saving,
  `test <reaction> <weak|medium|strong>` for *Try it* and `stats` for the counts. One line per command, one line per reply.
- The plugin keeps the counts in memory and saves them every minute and on exit. When the plugin isn't running,
  the window reads `stats.json`.

## Rules for changes

- **Events** are defined only in `src/package/events/DefaultEventSource.yaml`. Don't also call `PluginEvents.AddEvent`,
  or every event gets registered twice (the log then reports twice as many events).
- **Adding a reaction:** add it to `Reactions` and `HapticsSettings`, add all three variants to `DefaultEventSource.yaml`
  and `extra/eventMapping.yaml`, add its section to the settings window and detection to `PointerWatcher`.
- **Allowed vibration patterns** in `eventMapping.yaml`: `sharp_state_change`, `damp_state_change`, `sharp_collision`,
  `damp_collision`, `subtle_collision`, `happy_alert`, `angry_alert`, `completed`, `square`, `wave`, `firework`, `mad`,
  `knock`, `jingle`, `ringing`.
- **Keep detection threads apart.** MSAA and UI Automation calls can block when an app (or Explorer) hangs, so they run only
  on the element thread. The window thread must stay cheap: window borders, screen edges and clicks must not wait for a hung app.
- **Settings compatibility:** `settings.json` keeps the values `weak` / `medium` / `strong` and new properties need defaults,
  so older files keep loading.
- **Settings window:** take colors only from `Theme.Current`, not `SystemColors`. Custom controls draw themselves;
  `LinkButton` derives from `Control` because `Button` ignores custom painting in WinForms dark mode. After changing the window,
  regenerate the screenshots (see [BUILDING.md](BUILDING.md#screenshots)).
- **Icon:** the icon is supplied by the author. Don't redraw or modify it.
- Don't move the user's pointer in tests and don't trigger vibrations they didn't ask for.
