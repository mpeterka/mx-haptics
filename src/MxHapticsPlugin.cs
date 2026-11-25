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
        private Boolean _statsDirty;
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
            this.ReloadSettings();
            lock (this._statsLock)
            {
                this._stats = HapticsStats.Load();
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

        public void ReloadSettings() => this._settings = HapticsSettings.Load();

        public void ToggleEnabled()
        {
            var settings = HapticsSettings.Load();
            settings.Enabled = !settings.Enabled;
            settings.Save();
            this._settings = settings;
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
                && Enum.TryParse<Strength>(parts[2], ignoreCase: true, out var strength))
            {
                // test vibruje i při vypnuté reakci, aby šlo sílu vyzkoušet před zapnutím
                this.RaiseEvent(Reactions.EventName(parts[1], strength));
                return "ok";
            }

            return "error unknown command";
        }
    }
}
