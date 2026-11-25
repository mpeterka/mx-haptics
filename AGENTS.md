# mx-haptics

Plugin pro Logi Options+ (Logi Actions SDK, C#, .NET 10), který posílá hmatovou odezvu na myš MX Master 4
při přejetí hranice okna, najetí na tlačítko a nárazu na okraj plochy. Plugin běží uvnitř procesu
Logi Plugin Service, nemá vlastní okno ani ikonu v tray.

## Struktura

| Cesta | Obsah |
| --- | --- |
| `src/MxHapticsPlugin.cs` | třída `Plugin`: start/stop sledování, zapnuto/vypnuto (plugin setting `enabled`), `RaiseEvent` |
| `src/MxHapticsApplication.cs` | prázdná `ClientApplication`. Loader ji vyžaduje, bez ní plugin skončí na „Cannot load plugin“ |
| `src/PointerWatcher.cs` | detekce: vlákno oken a okrajů (10 ms), STA vlákno MSAA hit-testu tlačítek (30 ms) |
| `src/Native.cs` | P/Invoke (user32, oleacc) a prefix rozhraní `IAccessible` |
| `src/ToggleHapticsCommand.cs` | akce „Zapnout/vypnout MX Haptics“ pro tlačítko myši / Actions Ring |
| `src/package/` | obsah balíčku: `metadata/` (LoupedeckPackage.yaml, ikona), `events/` (události a mapování na vibrace) |
| `tools/Make-Icon.ps1` | generátor ikony `Icon256x256.png` (System.Drawing) |

## Sestavení a ladění

- `dotnet build src/MxHapticsPlugin.csproj`. Na tomto stroji je SDK v `C:\Program Files\dotnet`; když `dotnet` není v PATH, přidej ho.
- `PluginApi.dll` se bere z `C:\Program Files\Logi\LogiPluginService\` (nekopíruje se do výstupu, `Private=false`).
- Build skládá plugin do `build/` (`metadata/`, `events/`, `win/`), zapisuje
  `%LOCALAPPDATA%\Logi\LogiPluginService\Plugins\MxHaptics.link` a přes `loupedeck:plugin/MxHaptics/reload` plugin znovu načte.
  `-p:SkipPluginLink=true` zápis `.link` a reload vynechá.
- Ověření načtení: `%LOCALAPPDATA%\Logi\LogiPluginService\Logs\plugin_logs\MxHaptics.log` má obsahovat
  „Plugin 'MxHaptics' … loaded“ a „Event source added … with 3 events“. Chyby jsou i v `_errors.log`.
- Vibrace samotné ověřit nejde, poznat je může jen uživatel s myší v ruce. Neříkej, že haptika funguje, když jsi ji necítil.
- Nehýbej uživateli kurzorem kvůli testu.

## Pravidla

- Události jsou definované jen v `src/package/events/DefaultEventSource.yaml`. Nevolej k nim navíc `PluginEvents.AddEvent`,
  jinak se zaregistrují dvakrát (log pak hlásí 6 událostí místo 3).
- Názvy událostí (`windowBorder`, `controlHover`, `screenEdge`) musí sedět v YAML, v `eventMapping.yaml` i v konstantách `MxHapticsPlugin`.
- Povolené vzory vibrací: `sharp_state_change`, `damp_state_change`, `sharp_collision`, `damp_collision`, `subtle_collision`,
  `happy_alert`, `angry_alert`, `completed`, `square`, `wave`, `firework`, `mad`, `knock`, `jingle`, `ringing`.
- Hit-test přes MSAA může v zaseknuté aplikaci blokovat. Proto běží ve vlastním vlákně odděleném od detekce oken; tohle oddělení zachovej.
- Ikonu neupravuj ručně v PNG. Změň `tools/Make-Icon.ps1`, pusť `pwsh tools/Make-Icon.ps1` a výsledek si prohlédni.
- Repozitář je soukromý (`mpeterka/mx-haptics`), větev `main`. Do commitů, PR ani issues nepiš AI patičku.
