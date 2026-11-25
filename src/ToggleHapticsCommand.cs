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

        protected override void RunCommand(String actionParameter)
        {
            this.HapticsPlugin.ToggleEnabled();
            this.ActionImageChanged();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.HapticsPlugin.Enabled ? "Haptics: on" : "Haptics: off";
    }
}
