# MX Haptics

<img src="src/package/metadata/Icon256x256.png" alt="MX Haptics icon" width="96" align="right">

A Logi Options+ plugin (Logi Actions SDK) that gives the **MX Master 4** haptic feedback across Windows:

| Reaction | When | Default pattern |
| --- | --- | --- |
| Window borders (`windowBorder`) | the pointer moves from one window to another | subtle |
| Buttons and links (`controlHover`) | the pointer enters a button, link, tab or menu item (MSAA), or a Windows 11 taskbar button (UI Automation) | damp |
| Screen edge (`screenEdge`) | the pointer hits the outer edge of the desktop (edges between monitors don't count) | damp |
| Click (`click`) | left or right click on an element that *Buttons and links* reacts to (clicks into text or on the desktop don't vibrate) | damp |

Windows only. The plugin runs inside Logi Plugin Service; its settings live in a separate window.

> Unofficial project, not affiliated with, endorsed by, or supported by Logitech. Logitech, Logi, Logi Options+,
> MX and MX Master are trademarks of Logitech; they are used here only to describe what the plugin works with.
>
> Neoficiální projekt, není spojen s Logitech, Logitechem schválen ani podporován. Logitech, Logi, Logi Options+,
> MX a MX Master jsou ochranné známky společnosti Logitech; v tomto projektu slouží jen k popisu, s čím plugin funguje.

## Installation

1. You need Logi Options+ and the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
   (`winget install Microsoft.DotNet.DesktopRuntime.10`).
2. Download `MxHaptics_<version>.lplug4` from [Releases](https://github.com/mpeterka/mx-haptics/releases) and double-click it.
   Logi Plugin Service asks for confirmation and installs the plugin to `%LOCALAPPDATA%\Logi\LogiPluginService\Plugins\MxHaptics`.
3. Optional Start menu shortcut for the settings window:
   `pwsh tools/Install-StartMenuShortcut.ps1 -Target "$env:LOCALAPPDATA\Logi\LogiPluginService\Plugins\MxHaptics\settings\MxHapticsSettings.exe"`

Don't combine the package with a development install from [Building](#building): delete `MxHaptics.link` before installing
the `.lplug4`. Releases are built by hand (see [Releasing](#releasing)); GitHub Actions can't build the plugin because it needs
`PluginApi.dll` from an installed Logi Options+.

## Settings

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/settings-dark.png">
  <img src="docs/screenshots/settings-light.png" alt="MX Haptics settings window" width="440">
</picture>

### Opening the window

1. **Start menu:** type "MX Haptics" and run *MX Haptics Settings*.
   `pwsh tools/Install-StartMenuShortcut.ps1` creates the shortcut (`-Remove` deletes it).
2. **Mouse button or Actions Ring:** in Logi Options+ open the MX Master 4, pick a button (or the Actions Ring)
   and choose **MX Haptics → MX Haptics settings** among the installed plugin actions. If MX Haptics isn't listed,
   use *Restart Logi Plugin Service* in the Options+ settings.
3. **Directly:** `build\settings\MxHapticsSettings.exe`.

Only one window runs at a time; starting it again brings the open window to the front.

### What you can set

- turn all haptics on or off (*Haptic feedback*),
- turn each reaction on or off,
- for *Buttons and links*, include or skip Windows taskbar buttons (*Include Windows taskbar buttons*),
- pick the vibration pattern of each reaction: *Subtle* (`subtle_collision`), *Damp* (`damp_collision`),
  *Sharp* (`sharp_collision`); picking one vibrates right away,
- feel a reaction with *Try it* (works even when the reaction is off).

Each reaction shows how many vibrations it sent today and in total (*Try it* doesn't count). The window refreshes the numbers every 2 s.

Changes are saved instantly to `%LOCALAPPDATA%\MxHaptics\settings.json` and the plugin picks them up without a restart.

The window follows the Windows light or dark mode and is styled after the Logi Options+ settings panel. When the plugin isn't
running, the footer says so in orange and the settings apply once it starts.

### Vibration pattern vs. Haptic intensity

*Haptic intensity* in Options+ (Subtle / Low / Medium / High) sets how strongly the mouse motor vibrates, for everything.
The SDK doesn't let a plugin choose a strength, only which vibration pattern to play. So each reaction has three events
(`windowBorder_weak`, `_medium`, `_strong`) mapped to the patterns *Subtle*, *Damp* and *Sharp* in
[`eventMapping.yaml`](src/package/events/extra/eventMapping.yaml), and the plugin raises the one you picked.
The Options+ intensity then scales all of them. The event names and the `weak` / `medium` / `strong` values in
`settings.json` are kept from earlier versions.

### Other controls

- **The whole plugin:** Options+ → MX Master 4 → *Haptic feedback* → *Enable haptic for app plugins* → MX Haptics.
  With this switch off the mouse doesn't vibrate even though the plugin runs.
- **Overall vibration strength:** *Haptic intensity* in the same panel (applies to everything, not just this plugin).
- **Quick toggle:** the *Toggle MX Haptics* action flips the master switch.

<img src="docs/screenshots/options-plus-haptics.png" alt="Logi Options+: Haptic feedback, Haptic intensity and the MX Haptics switch under Enable haptic for app plugins" width="720">

## Building

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`winget install Microsoft.DotNet.SDK.10`;
it includes the Windows Desktop Runtime for the settings window) and an installed Logi Options+.
`PluginApi.dll` is referenced from `C:\Program Files\Logi\LogiPluginService`.

```powershell
dotnet build src/MxHapticsPlugin.csproj
```

The build assembles the plugin in `build/` (`metadata/`, `events/`, `win/`), compiles the settings window into `build/settings/`
and writes `%LOCALAPPDATA%\Logi\LogiPluginService\Plugins\MxHaptics.link`, so Logi Plugin Service loads the plugin straight from there.
If the plugin doesn't show up, use *Restart Logi Plugin Service* in the Options+ settings.
Logs: `%LOCALAPPDATA%\Logi\LogiPluginService\Logs\plugin_logs\MxHaptics.log`.

**Uninstall:** delete `MxHaptics.link`, restart Logi Plugin Service and optionally delete `%LOCALAPPDATA%\MxHaptics`.

### Releasing

Bump `version` in [`LoupedeckPackage.yaml`](src/package/metadata/LoupedeckPackage.yaml), then:

```powershell
dotnet tool install --global LogiPluginTool   # once
Remove-Item build -Recurse -Force             # so no .pdb from a dev build ends up in the package
dotnet build src/MxHapticsPlugin.csproj -c Release -p:SkipPluginLink=true   # Release has no .pdb (DebugType=none in the csproj)
logiplugintool pack ./build ./MxHaptics_<version>.lplug4
logiplugintool verify ./MxHaptics_<version>.lplug4
gh release create v<version> ./MxHaptics_<version>.lplug4 --title "MX Haptics <version>" --notes "…"
```

## How it works

- The window thread reads the pointer position every 10 ms. It reports a window border when the root window under the pointer
  changes and stays for at least two samples (filters out flickering tooltips). It reports a screen edge when the neighbouring
  pixel isn't on any monitor.
- The element thread runs every 30 ms (only while the pointer moves and no button is held). It asks MSAA
  (`AccessibleObjectFromPoint`) for the element under the pointer and walks a few parents up. It vibrates when the pointer enters
  a new element with the role of a button, link, tab, menu item, check box, radio button or combo box.
- To MSAA the Windows 11 taskbar is one block without buttons. While the pointer is over the taskbar (`Shell_TrayWnd`, or
  `Shell_SecondaryTrayWnd` on other monitors) the plugin loads the rectangles of its buttons (apps, Start, notification area
  icons, clock …) through UI Automation, caches them for a second and hit-tests the pointer itself.
- Click: the window thread reads the left and right button state every 10 ms. On a press it vibrates if the element thread last
  found a clickable element under the pointer. The vibration arrives a little after the physical click (it travels through
  Logi Plugin Service to the mouse).
- The settings window talks to the plugin over a local named pipe `MxHaptics` (current user only): `reload` after saving,
  `test <reaction> <weak|medium|strong>` for *Try it* and `stats` for the vibration counts.
- The plugin keeps the counts in memory and saves them every minute (and on exit) to `%LOCALAPPDATA%\MxHaptics\stats.json`
  (in total and per day). When the plugin isn't running, the window reads that file.
- Apps that don't expose accessibility information (some games, some Electron apps) won't vibrate on their buttons.

## Icon

The high-resolution source is [`design/icon-source.png`](design/icon-source.png). The plugin uses its downscaled copy
`src/package/metadata/Icon256x256.png` (256 × 256 px). The settings window exe and the shortcut use `design/icon.ico`,
generated from the PNG by `pwsh tools/Convert-IconToIco.ps1`.

## Screenshots

The window screenshots in `docs/screenshots/` are generated without clicking. The window renders the default settings in full,
saves nothing and closes:

```powershell
build\settings\MxHapticsSettings.exe --screenshot docs\screenshots\settings-dark.png --theme dark
build\settings\MxHapticsSettings.exe --screenshot docs\screenshots\settings-light.png --theme light
```

## License

[MIT](LICENSE)
