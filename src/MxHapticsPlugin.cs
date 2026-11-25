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

            this._watcher = new PointerWatcher(this.RaiseReaction);
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

        /// <summary>Příkazy z okna nastavení: <c>reload</c>, <c>test &lt;reakce&gt; &lt;síla&gt;</c>.</summary>
        private String HandleCommand(String line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1 && parts[0] == "reload")
            {
                this.ReloadSettings();
                return "ok";
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
