namespace Loupedeck.MxHapticsPlugin
{
    using System;

    public class MxHapticsPlugin : Plugin
    {
        internal const String WindowBorder = "windowBorder";
        internal const String ControlHover = "controlHover";
        internal const String ScreenEdge = "screenEdge";

        private const String EnabledSetting = "enabled";

        private PointerWatcher _watcher;

        public override Boolean UsesApplicationApiOnly => true;

        public override Boolean HasNoApplication => true;

        public Boolean Enabled { get; private set; } = true;

        public override void Load()
        {
            if (this.TryGetPluginSetting(EnabledSetting, out var value))
            {
                this.Enabled = value != "0";
            }

            this._watcher = new PointerWatcher(this.Raise);
            this._watcher.Start();
        }

        public override void Unload()
        {
            this._watcher?.Dispose();
            this._watcher = null;
        }

        public void ToggleEnabled()
        {
            this.Enabled = !this.Enabled;
            this.SetPluginSetting(EnabledSetting, this.Enabled ? "1" : "0", false);
        }

        private void Raise(String eventName)
        {
            if (!this.Enabled)
            {
                return;
            }

            try
            {
                this.PluginEvents.RaiseEvent(eventName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"MxHaptics: RaiseEvent {eventName} selhal: {ex}");
            }
        }
    }
}
