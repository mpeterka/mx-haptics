namespace Loupedeck.MxHapticsPlugin
{
    using System;

    public class OpenSettingsCommand : PluginDynamicCommand
    {
        public OpenSettingsCommand()
            : base("MX Haptics settings", "Opens the window with reaction and intensity settings", "MX Haptics")
        {
        }

        protected override void RunCommand(String actionParameter) => ((MxHapticsPlugin)this.Plugin).OpenSettings();
    }
}
