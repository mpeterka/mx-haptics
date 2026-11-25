# MX Haptics

<img src="src/package/metadata/Icon256x256.png" alt="Ikona MX Haptics" width="96" align="right">

Plugin pro Logi Options+ (Logi Actions SDK), který posílá hmatovou odezvu na myš **MX Master 4**:

| Událost | Kdy | Výchozí vzor |
| --- | --- | --- |
| `windowBorder` | kurzor přejede z jednoho okna do jiného | `subtle_collision` |
| `controlHover` | najetí na tlačítko, odkaz, záložku, položku menu (MSAA) | `sharp_state_change` |
| `screenEdge` | náraz na vnější okraj plochy (vnitřní hrany monitorů se nepočítají) | `damp_collision` |

Jen Windows. Plugin běží uvnitř Logi Plugin Service, nemá vlastní okno ani ikonu v tray.

## Nastavení

- **Zapnout/vypnout celý plugin:** Options+ → nastavení myši → *Enable haptic for app plugins* → MX Haptics.
- **Síla vibrací:** globální nastavení haptiky myši v Options+.
- **Dočasné vypnutí:** akce *Zapnout/vypnout MX Haptics* (skupina MX Haptics), kterou jde přiřadit na tlačítko myši
  nebo do Actions Ringu. Stav si plugin pamatuje.
- **Typ vibrace:** v [`src/package/events/extra/eventMapping.yaml`](src/package/events/extra/eventMapping.yaml), pak znovu sestavit.
  Vzory: `sharp_state_change`, `damp_state_change`, `sharp_collision`, `damp_collision`, `subtle_collision`,
  `happy_alert`, `angry_alert`, `completed`, `square`, `wave`, `firework`, `mad`, `knock`, `jingle`, `ringing`.

## Sestavení

Potřeba [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`winget install Microsoft.DotNet.SDK.10`)
a nainstalované Logi Options+. `PluginApi.dll` se bere z `C:\Program Files\Logi\LogiPluginService`.

```powershell
dotnet build src/MxHapticsPlugin.csproj
```

Build složí plugin do `build/` (`metadata/`, `events/`, `win/`) a zapíše
`%LOCALAPPDATA%\Logi\LogiPluginService\Plugins\MxHaptics.link`, takže ho Logi Plugin Service načte přímo odtud.
Pokud se plugin neobjeví, dej v nastavení Options+ *Restart Logi Plugin Service*.
Logy: `%LOCALAPPDATA%\Logi\LogiPluginService\Logs\plugin_logs\MxHaptics.log`.

**Odinstalace:** smazat `MxHaptics.link` a restartovat Logi Plugin Service.

## Jak to funguje

- Vlákno oken a okrajů každých 10 ms čte polohu kurzoru. Hranici okna hlásí, když se změní kořenové okno pod kurzorem
  a vydrží aspoň dva vzorky (filtruje probliknutí tooltipů). Okraj plochy hlásí, když sousední pixel neleží na žádném monitoru.
- Vlákno prvků každých 30 ms (jen při pohybu a bez stisknutého tlačítka) zjistí přes MSAA (`AccessibleObjectFromPoint`)
  prvek pod kurzorem a projde pár úrovní rodičů. Vibruje při vstupu na nový prvek s rolí tlačítko, odkaz, záložka, položka menu,
  zaškrtávátko, přepínač nebo rozbalovací seznam.
- Aplikace, které neposkytují accessibility (některé hry, část Electron aplikací), na tlačítkách vibrovat nebudou.

## Ikona

Zdroj ve vysokém rozlišení je [`design/icon-source.png`](design/icon-source.png). Plugin používá jeho zmenšeninu `src/package/metadata/Icon256x256.png` (256 × 256 px).

## Licence

[MIT](LICENSE)
