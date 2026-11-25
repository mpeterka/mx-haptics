namespace Loupedeck.MxHapticsPlugin
{
    using System;

    public class ToggleHapticsCommand : PluginDynamicCommand
    {
        public ToggleHapticsCommand()
            : base("Zapnout/vypnout MX Haptics", "Přepne hmatovou odezvu při pohybu kurzoru", "MX Haptics")
        {
        }

        private MxHapticsPlugin HapticsPlugin => (MxHapticsPlugin)this.Plugin;

        protected override void RunCommand(String actionParameter)
        {
            this.HapticsPlugin.ToggleEnabled();
            this.ActionImageChanged();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.HapticsPlugin.Enabled ? "Haptika: zap" : "Haptika: vyp";
    }
}
