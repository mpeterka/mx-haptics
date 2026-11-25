# MX Haptics

Plugin pro Logi Options+ (Logi Actions SDK), který posílá hmatovou odezvu na myš MX Master 4:

| Událost | Kdy | Výchozí vzor |
| --- | --- | --- |
| `windowBorder` | kurzor přejede z jednoho okna do jiného | `subtle_collision` |
| `controlHover` | najetí na tlačítko, odkaz, záložku, položku menu (MSAA) | `sharp_state_change` |
| `screenEdge` | náraz na vnější okraj plochy (vnitřní hrany monitorů se nepočítají) | `damp_collision` |

Vzory se mění v `src/package/events/extra/eventMapping.yaml` (seznam: `sharp_state_change`, `damp_state_change`,
`sharp_collision`, `damp_collision`, `subtle_collision`, `happy_alert`, `angry_alert`, `completed`, `square`, `wave`, `firework`, `mad`, `knock`, `jingle`, `ringing`).

Akce **Zapnout/vypnout MX Haptics** (skupina MX Haptics) jde přiřadit na tlačítko myši nebo Actions Ring.

## Sestavení

Potřeba .NET 10 SDK a nainstalované Logi Options+ (PluginApi.dll se bere z `C:\Program Files\Logi\LogiPluginService`).

```powershell
dotnet build src/MxHapticsPlugin.csproj
```

Build složí plugin do `build/` (`metadata/`, `events/`, `win/`) a zapíše
`%LOCALAPPDATA%\Logi\LogiPluginService\Plugins\MxHaptics.link`, takže ho Logi Plugin Service načte přímo odtud.
Pokud se plugin neobjeví, v nastavení Options+ dej „Restart Logi Plugin Service“. Logy: `%LOCALAPPDATA%\Logi\LogiPluginService\Logs`.

Odinstalace: smazat `MxHaptics.link` a restartovat Logi Plugin Service.
