namespace Loupedeck.MxHapticsPlugin
{
    using System;
    using System.Diagnostics;
    using System.IO;

    using MxHaptics.Shared;

    public class MxHapticsPlugin : Plugin
    {
        private PointerWatcher _watcher;
        private ControlPipeServer _pipe;
        private volatile HapticsSettings _settings = new();
        private readonly Object _statsLock = new();
        private HapticsStats _stats = new();
        private readonly Object _statsSaveLock = new();
        private Boolean _statsDirty;
        private Boolean _statsPendingMerge;
        private System.Threading.Timer _statsTimer;

        public override Boolean UsesApplicationApiOnly => true;

        public override Boolean HasNoApplication => true;

        public Boolean Enabled => this._settings.Enabled;

        /// <summary>Cesta k oknu nastavení: build/settings/MxHapticsSettings.exe vedle build/win/.</summary>
        internal String SettingsAppPath =>
            Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(typeof(MxHapticsPlugin).Assembly.Location) ?? ".",
                "..", "settings", "MxHapticsSettings.exe"));

        public override void Load()
        {
            this._settings = HapticsSettings.Load();
            lock (this._statsLock)
            {
                // nepřečtenou historii nesmíme přepsat: počítáme do prázdné a sloučíme, až se soubor podaří přečíst
                this._statsPendingMerge = !HapticsStats.TryLoad(quarantineCorrupt: true, out var stats);
                this._stats = stats ?? new HapticsStats();
            }

            this._statsTimer = new System.Threading.Timer(_ => this.SaveStats(), null, 60_000, 60_000);

            this._watcher = new PointerWatcher(this.RaiseReaction, () => this._settings.TaskbarButtons);
            this._watcher.Start();

            this._pipe = new ControlPipeServer(this.HandleCommand);
            this._pipe.Start();
        }

        public override void Unload()
        {
            this._pipe?.Dispose();
            this._pipe = null;
            this._watcher?.Dispose();
            this._watcher = null;
            this._statsTimer?.Dispose();
            this._statsTimer = null;
            this.SaveStats();
        }

        /// <summary>Změna hlavního vypínače (okno nastavení, akce Toggle); akce podle ní obnoví popisek.</summary>
        public event Action EnabledChanged;

        /// <summary>Načte nastavení znovu (příkaz <c>reload</c> z okna). Nečitelný soubor nechá platit dosavadní stav.</summary>
        public void ReloadSettings()
        {
            if (HapticsSettings.TryLoad(out var settings))
            {
                this.ApplySettings(settings);
            }
        }

        public void ToggleEnabled()
        {
            // základ ze souboru (okno mohlo mezitím něco změnit); když nejde přečíst, z paměti
            var settings = HapticsSettings.TryLoad(out var loaded) ? loaded : this._settings;
            settings.Enabled = !settings.Enabled;
            this.ApplySettings(settings);
            try
            {
                settings.Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // přepnutí platí aspoň do restartu pluginu
                Trace.WriteLine($"MxHaptics: nastavení nejde uložit: {ex.Message}");
            }
        }

        private void ApplySettings(HapticsSettings settings)
        {
            var enabledChanged = settings.Enabled != this._settings.Enabled;
            this._settings = settings;
            if (enabledChanged)
            {
                this.EnabledChanged?.Invoke();
            }
        }

        public void OpenSettings()
        {
            var path = this.SettingsAppPath;
            if (!File.Exists(path))
            {
                Trace.WriteLine($"MxHaptics: okno nastavení nenalezeno: {path}");
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private void RaiseReaction(String reaction)
        {
            var settings = this._settings;
            if (!settings.Enabled)
            {
                return;
            }

            var reactionSettings = settings.For(reaction);
            if (reactionSettings.Enabled)
            {
                this.RaiseEvent(Reactions.EventName(reaction, reactionSettings.Strength));
                lock (this._statsLock)
                {
                    this._stats.Add(reaction, DateTime.Now);
                    this._statsDirty = true;
                }
            }
        }

        private void SaveStats()
        {
            // timer a Unload se můžou potkat: zápis jen jeden naráz
            lock (this._statsSaveLock)
            {
                if (this._statsPendingMerge)
                {
                    if (!HapticsStats.TryLoad(quarantineCorrupt: true, out var onDisk))
                    {
                        return; // soubor pořád nejde přečíst: nepřepisovat, zkusit při dalším uložení
                    }

                    lock (this._statsLock)
                    {
                        this._stats.MergeFrom(onDisk);
                        this._statsPendingMerge = false;
                    }
                }

                String json;
                lock (this._statsLock)
                {
                    if (!this._statsDirty)
                    {
                        return;
                    }

                    json = this._stats.ToJson();
                    this._statsDirty = false;
                }

                try
                {
                    HapticsStats.FromJson(json).Save();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Trace.WriteLine($"MxHaptics: statistiku nejde uložit: {ex.Message}");
                    lock (this._statsLock)
                    {
                        this._statsDirty = true;
                    }
                }
            }
        }

        private void RaiseEvent(String eventName)
        {
            try
            {
                this.PluginEvents.RaiseEvent(eventName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MxHaptics: RaiseEvent {eventName} selhal: {ex}");
            }
        }

        /// <summary>Příkazy z okna nastavení: <c>reload</c>, <c>stats</c>, <c>test &lt;reakce&gt; &lt;síla&gt;</c>.</summary>
        private String HandleCommand(String line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1 && parts[0] == "reload")
            {
                this.ReloadSettings();
                return "ok";
            }

            if (parts.Length == 1 && parts[0] == "stats")
            {
                lock (this._statsLock)
                {
                    return this._stats.ToJson();
                }
            }

            if (parts.Length == 3 && parts[0] == "test"
                && Array.IndexOf(Reactions.All, parts[1]) >= 0
                && Enum.TryParse<Strength>(parts[2], ignoreCase: true, out var strength)
                && Enum.IsDefined(strength))
            {
                // test vibruje i při vypnuté reakci, aby šlo sílu vyzkoušet před zapnutím
                this.RaiseEvent(Reactions.EventName(parts[1], strength));
                return "ok";
            }

            return "error unknown command";
        }
    }
}
