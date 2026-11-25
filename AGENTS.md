# mx-haptics

Plugin pro Logi Options+ (Logi Actions SDK, C#, .NET 10), který posílá hmatovou odezvu na myš MX Master 4
při přejetí hranice okna, najetí na tlačítko a nárazu na okraj plochy. Plugin běží uvnitř procesu
Logi Plugin Service. Nastavení má samostatné WinForms okno (`settings/`), ikona v tray není.

## Struktura

| Cesta | Obsah |
| --- | --- |
| `shared/HapticsSettings.cs` | model nastavení (JSON v `%LOCALAPPDATA%\MxHaptics\settings.json`), názvy reakcí a událostí; sdílí ho plugin i okno |
| `src/MxHapticsPlugin.cs` | třída `Plugin`: start/stop sledování, načtení nastavení, výběr události podle síly, příkazy z roury |
| `shared/HapticsStats.cs` | počty odeslaných vibrací (celkem a po dnech) v `%LOCALAPPDATA%\MxHaptics\stats.json`; zapisuje jen plugin |
| `src/ControlPipeServer.cs` | pojmenovaná roura `MxHaptics` (`CurrentUserOnly`), příkazy `reload`, `stats` a `test <reakce> <síla>` |
| `src/MxHapticsApplication.cs` | prázdná `ClientApplication`. Loader ji vyžaduje, bez ní plugin skončí na „Cannot load plugin“ |
| `src/PointerWatcher.cs` | detekce: vlákno oken a okrajů (10 ms), STA vlákno hit-testu tlačítek (30 ms) |
| `src/TaskbarButtons.cs` | tlačítka hlavní lišty Win 11 přes UI Automation (COM, jen prefixy vtable), cache obdélníků na 1 s |
| `src/Native.cs` | P/Invoke (user32, oleacc) a prefix rozhraní `IAccessible` |
| `src/ToggleHapticsCommand.cs` | akce „Zapnout/vypnout MX Haptics“ (hlavní vypínač v JSON) |
| `src/OpenSettingsCommand.cs` | akce „Nastavení MX Haptics“, spustí `build/settings/MxHapticsSettings.exe` |
| `settings/` | okno nastavení (WinForms, `net10.0-windows`), ukládá JSON a posílá pluginu `reload`/`test` |
| `settings/LogiControls.cs` | vzhled ve stylu Logi Options+: `Theme` (barvy pro tmavý/světlý režim), `ToggleSwitch`, `LogiRadio`, `LinkButton` |
| `src/package/` | obsah balíčku: `metadata/` (LoupedeckPackage.yaml, ikona), `events/` (události a mapování na vibrace) |
| `design/icon-source.png` | zdroj ikony (1254 × 1254 px, průhledné rohy) |
| `design/icon.ico` | ikona exe a zástupce, generuje `tools/Convert-IconToIco.ps1` z `icon-source.png` |
| `tools/Install-StartMenuShortcut.ps1` | zástupce „MX Haptics – nastavení“ v nabídce Start (`-Remove` smaže) |
| `docs/screenshots/` | screenshoty okna pro README (světlý a tmavý režim) a `options-haptika.png` z Options+ (dodal uživatel) |

## Sestavení a ladění

- `dotnet build src/MxHapticsPlugin.csproj`. Na tomto stroji je SDK v `C:\Program Files\dotnet`; když `dotnet` není v PATH, přidej ho.
- `PluginApi.dll` se bere z `C:\Program Files\Logi\LogiPluginService\` (nekopíruje se do výstupu, `Private=false`).
- Build skládá plugin do `build/` (`metadata/`, `events/`, `win/`), přes `dotnet build` přeloží okno do `build/settings/`
  (`-p:SkipSettingsApp=true` to vynechá), zapisuje
  `%LOCALAPPDATA%\Logi\LogiPluginService\Plugins\MxHaptics.link` a přes `loupedeck:plugin/MxHaptics/reload` plugin znovu načte.
  `-p:SkipPluginLink=true` zápis `.link` a reload vynechá.
- Ověření načtení: `%LOCALAPPDATA%\Logi\LogiPluginService\Logs\plugin_logs\MxHaptics.log` má obsahovat
  „Plugin 'MxHaptics' … loaded“ a „Event source added … with 12 events“. Chyby jsou i v `_errors.log`.
- Vibrace samotné ověřit nejde, poznat je může jen uživatel s myší v ruce. Neříkej, že haptika funguje, když jsi ji necítil.
- Nehýbej uživateli kurzorem kvůli testu. Rouru jde ověřit příkazem `reload` (odpověď `ok`); `test` uživateli zavibruje myší.

## Pravidla

- Události jsou definované jen v `src/package/events/DefaultEventSource.yaml`. Nevolej k nim navíc `PluginEvents.AddEvent`,
  jinak se zaregistrují dvakrát (log pak hlásí dvojnásobek událostí).
- Události mají tvar `<reakce>_<síla>` (`windowBorder_weak` …, viz `Reactions.EventName`). Všech 12 kombinací musí být
  v `DefaultEventSource.yaml` i v `eventMapping.yaml`. Novou reakci přidej do `Reactions`, `HapticsSettings`, obou YAML a do řádků okna.
- Síla: `weak` = `subtle_collision`, `medium` = `damp_collision`, `strong` = `sharp_collision`. SDK nemá parametr síly, proto varianty událostí.
- Povolené vzory vibrací: `sharp_state_change`, `damp_state_change`, `sharp_collision`, `damp_collision`, `subtle_collision`,
  `happy_alert`, `angry_alert`, `completed`, `square`, `wave`, `firework`, `mad`, `knock`, `jingle`, `ringing`.
- Hit-test přes MSAA i UIA (lišta) může v zaseknuté aplikaci/Exploreru blokovat. Proto běží ve vlastním vlákně odděleném od detekce oken; tohle oddělení zachovej.
- Hlavní lišta: MSAA ani UIA `ElementFromPoint` do XAML lišty nevidí, proto `FindAll` tlačítek + vlastní hit-test. UIA vrací fyzické
  pixely, porovnávej s `GetPhysicalCursorPos`. Rozhraní v `TaskbarButtons.cs` jsou prefixy vtable z `UIAutomationClient.h`, pořadí nepřehazuj.
- Lišta jde vypnout volbou `taskbarButtons` v JSON (výchozí `true`, v okně pod řádkem „Tlačítka a odkazy“). Plugin ji čte při každém vzorku přes delegát z `MxHapticsPlugin`.
- Statistika: počítá se v `RaiseReaction` až po kontrole vypínačů (test z okna ne), pod `_statsLock`. Plugin ukládá `stats.json`
  jednou za minutu a v `Unload`; okno čte přes `stats` z roury (na pozadí, `Task.Run`), jinak ze souboru. Okno do souboru nepíše.
- Kliknutí (`click`) hlásí vlákno oken (10 ms, hrana stisku L/P tlačítka), ale jen když `_hoverKey` (píše vlákno prvků) není null.
  Vlákno oken tím dál nevolá MSAA ani UIA, jen čte volatile pole.
- Ikonu dodává uživatel. Nekresli vlastní ani ji neupravuj. `Icon256x256.png` je zmenšenina `design/icon-source.png` na 256 × 256 (bikubicky).
  Při nové ikoně nahraď obojí a pusť `pwsh tools/Convert-IconToIco.ps1`.
- Po změně vzhledu okna přegeneruj screenshoty: `build\settings\MxHapticsSettings.exe --screenshot docs\screenshots\nastaveni-<dark|light>.png --theme <dark|light>`.
- Okno nastavení napodobuje panel „Modify settings“ v Logi Options+: sekce oddělené čarou, nadpis + `ToggleSwitch` vpravo,
  síla jako `LogiRadio`, „Vyzkoušet“ jako `LinkButton` (akcent, velká písmena). Barvy ber jen z `Theme.Current`, ne `SystemColors`.
  `LinkButton` dědí z `Control`, ne z `Button`: ten si v tmavém režimu WinForms kreslí sám a vlastní `OnPaint` ignoruje.
  Tenhle režim kreslí výchozí nastavení a nic neukládá. Screenshoty okna nedělej snímáním obrazovky, uživatel s oknem může zrovna pracovat.
- Release: postup v README („Vydání verze“), balíček `.lplug4` přes `logiplugintool pack` + `verify`, nahrát přes `gh release create`.
  Release build pouštěj s `-p:SkipPluginLink=true` po smazání `build/`; Release konfigurace nevytváří `.pdb` (obsahují lokální cesty), nevracej to. GitHub Actions build neumí (chybí `PluginApi.dll`). Balíček na tomhle stroji
  neinstaluj, koliduje s vývojovým `MxHaptics.link`.
- Repozitář je veřejný (`mpeterka/mx-haptics`), větev `main`. Commity pod `martin.peterka@gmail.com` (firemní e-mail sem nepatří). Do commitů, PR ani issues nepiš AI patičku.
