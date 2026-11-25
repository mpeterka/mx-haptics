# MX Haptics

<img src="src/package/metadata/Icon256x256.png" alt="Ikona MX Haptics" width="96" align="right">

Plugin pro Logi Options+ (Logi Actions SDK), který posílá hmatovou odezvu na myš **MX Master 4**:

| Reakce | Kdy | Výchozí síla |
| --- | --- | --- |
| Hranice okna (`windowBorder`) | kurzor přejede z jednoho okna do jiného | slabá |
| Tlačítka a odkazy (`controlHover`) | najetí na tlačítko, odkaz, záložku, položku menu (MSAA) | střední |
| Okraj obrazovky (`screenEdge`) | náraz na vnější okraj plochy (vnitřní hrany monitorů se nepočítají) | střední |

Jen Windows. Plugin běží uvnitř Logi Plugin Service, nastavení má samostatné okno.

## Nastavení

**Okno nastavení** otevře akce *Nastavení MX Haptics* (skupina MX Haptics), kterou si v Options+ přiřadíš na tlačítko myši
nebo do Actions Ringu. Jde ho spustit i přímo: `build/settings/MxHapticsSettings.exe`. V okně můžeš:

- celou haptiku zapnout nebo vypnout,
- každou reakci zvlášť zapnout nebo vypnout,
- zvolit sílu reakce: slabá (`subtle_collision`), střední (`damp_collision`), silná (`sharp_collision`),
- tlačítkem *Vyzkoušet* si vibraci hned ohmatat (funguje i u vypnuté reakce).

Změny se ukládají hned do `%LOCALAPPDATA%\MxHaptics\settings.json` a plugin je načte bez restartu.

Další ovládání:

- **Celý plugin:** Options+ → nastavení myši → *Enable haptic for app plugins* → MX Haptics.
- **Celková síla vibrací myši:** nastavení haptiky myši v Options+ (platí pro všechno).
- **Rychlé vypnutí:** akce *Zapnout/vypnout MX Haptics* přepíná hlavní vypínač.

SDK neumí poslat vibraci se zvolenou silou. Proto má každá reakce tři události (`windowBorder_weak`, `_medium`, `_strong`)
s různými vzory v [`eventMapping.yaml`](src/package/events/extra/eventMapping.yaml) a plugin posílá tu zvolenou.

## Sestavení

Potřeba [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`winget install Microsoft.DotNet.SDK.10`;
obsahuje i Windows Desktop Runtime pro okno nastavení) a nainstalované Logi Options+.
`PluginApi.dll` se bere z `C:\Program Files\Logi\LogiPluginService`.

```powershell
dotnet build src/MxHapticsPlugin.csproj
```

Build složí plugin do `build/` (`metadata/`, `events/`, `win/`), přeloží okno nastavení do `build/settings/` a zapíše
`%LOCALAPPDATA%\Logi\LogiPluginService\Plugins\MxHaptics.link`, takže ho Logi Plugin Service načte přímo odtud.
Pokud se plugin neobjeví, dej v nastavení Options+ *Restart Logi Plugin Service*.
Logy: `%LOCALAPPDATA%\Logi\LogiPluginService\Logs\plugin_logs\MxHaptics.log`.

**Odinstalace:** smazat `MxHaptics.link`, restartovat Logi Plugin Service a případně smazat `%LOCALAPPDATA%\MxHaptics`.

## Jak to funguje

- Vlákno oken a okrajů každých 10 ms čte polohu kurzoru. Hranici okna hlásí, když se změní kořenové okno pod kurzorem
  a vydrží aspoň dva vzorky (filtruje probliknutí tooltipů). Okraj plochy hlásí, když sousední pixel neleží na žádném monitoru.
- Vlákno prvků každých 30 ms (jen při pohybu a bez stisknutého tlačítka) zjistí přes MSAA (`AccessibleObjectFromPoint`)
  prvek pod kurzorem a projde pár úrovní rodičů. Vibruje při vstupu na nový prvek s rolí tlačítko, odkaz, záložka, položka menu,
  zaškrtávátko, přepínač nebo rozbalovací seznam.
- Okno nastavení posílá pluginu příkazy přes lokální pojmenovanou rouru `MxHaptics` (jen pro aktuálního uživatele):
  `reload` po uložení a `test <reakce> <síla>` pro tlačítko *Vyzkoušet*.
- Aplikace, které neposkytují accessibility (některé hry, část Electron aplikací), na tlačítkách vibrovat nebudou.

## Ikona

Zdroj ve vysokém rozlišení je [`design/icon-source.png`](design/icon-source.png). Plugin používá jeho zmenšeninu
`src/package/metadata/Icon256x256.png` (256 × 256 px). Okno nastavení ji má vloženou jako prostředek.

## Licence

[MIT](LICENSE)
