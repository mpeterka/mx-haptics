# Building MX Haptics

## Requirements

- Windows 10/11 with Logi Options+ installed. `PluginApi.dll` is referenced from `C:\Program Files\Logi\LogiPluginService`
  and isn't redistributed in this repository.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`winget install Microsoft.DotNet.SDK.10`;
  it includes the Windows Desktop Runtime for the settings window).
- An MX Master 4 to feel the result. Nothing else can tell you whether a vibration happened.

## Build and run

```powershell
dotnet build src/MxHapticsPlugin.csproj
```

The build:

1. assembles the plugin package in `build/` (`metadata/`, `events/`, `win/`),
2. compiles the settings window into `build/settings/` (skip with `-p:SkipSettingsApp=true`),
3. writes `%LOCALAPPDATA%\Logi\LogiPluginService\Plugins\MxHaptics.link` pointing at `build/`, so Logi Plugin Service
   loads the plugin straight from the working tree, and asks it to reload the plugin (skip both with `-p:SkipPluginLink=true`).

Check that the plugin loaded in `%LOCALAPPDATA%\Logi\LogiPluginService\Logs\plugin_logs\MxHaptics.log`: it should contain
`Plugin 'MxHaptics' … loaded` and `Event source added … with 12 events`. Errors also go to `_errors.log` in the same folder.
If the plugin doesn't show up, use *Restart Logi Plugin Service* in the Options+ settings.

Don't combine a development install with an installed `.lplug4`: the plugin would be registered twice. Remove one of them first.

**Remove the development install:** delete `MxHaptics.link`, restart Logi Plugin Service and optionally delete
`%LOCALAPPDATA%\MxHaptics` (settings and vibration counts).

## Releasing

Releases are built by hand. GitHub Actions can't build the plugin because it needs `PluginApi.dll` from an installed Logi Options+.

Bump `version` in [`LoupedeckPackage.yaml`](src/package/metadata/LoupedeckPackage.yaml), commit, then:

```powershell
dotnet tool install --global LogiPluginTool   # once
Remove-Item build -Recurse -Force             # so no .pdb from a dev build ends up in the package
dotnet build src/MxHapticsPlugin.csproj -c Release -p:SkipPluginLink=true   # Release has no .pdb (DebugType=none in the csproj)
logiplugintool pack ./build ./MxHaptics_<version>.lplug4
logiplugintool verify ./MxHaptics_<version>.lplug4
gh release create v<version> ./MxHaptics_<version>.lplug4 --title "MX Haptics <version>" --notes-file notes.md
dotnet build src/MxHapticsPlugin.csproj       # back to the development build
```

The `.pdb` files are left out on purpose: they contain local build paths.

## Icon

The high-resolution source is [`design/icon-source.png`](design/icon-source.png). The plugin uses its downscaled copy
`src/package/metadata/Icon256x256.png` (256 × 256 px, bicubic). The settings window exe and the Start menu shortcut use
`design/icon.ico`, generated from the PNG by:

```powershell
pwsh tools/Convert-IconToIco.ps1
```

## Screenshots

The window screenshots in `docs/screenshots/` are generated without clicking. The window renders the default settings in full,
saves nothing, doesn't talk to the plugin and closes:

```powershell
build\settings\MxHapticsSettings.exe --screenshot docs\screenshots\settings-dark.png --theme dark
build\settings\MxHapticsSettings.exe --screenshot docs\screenshots\settings-light.png --theme light
```

Regenerate both after every change to the window. `options-plus-haptics.png` is a screenshot of Logi Options+ and is updated by hand.
