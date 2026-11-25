namespace Loupedeck.MxHapticsPlugin
{
    using System;

    public class ToggleHapticsCommand : PluginDynamicCommand
    {
        public ToggleHapticsCommand()
            : base("Toggle MX Haptics", "Turns haptic feedback for pointer movement on or off", "MX Haptics")
        {
        }

        private MxHapticsPlugin HapticsPlugin => (MxHapticsPlugin)this.Plugin;

        // popisek „Haptics: on/off“ obnovit i při změně z okna nastavení, nejen po stisku této akce
        protected override Boolean OnLoad()
        {
            this.HapticsPlugin.EnabledChanged += this.ActionImageChanged;
            return base.OnLoad();
        }

        protected override Boolean OnUnload()
        {
            this.HapticsPlugin.EnabledChanged -= this.ActionImageChanged;
            return base.OnUnload();
        }

        protected override void RunCommand(String actionParameter) => this.HapticsPlugin.ToggleEnabled();

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.HapticsPlugin.Enabled ? "Haptics: on" : "Haptics: off";
    }
}
