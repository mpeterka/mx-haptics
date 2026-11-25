namespace Loupedeck.MxHapticsPlugin
{
    using System;

    public class OpenSettingsCommand : PluginDynamicCommand
    {
        public OpenSettingsCommand()
            : base("Nastavení MX Haptics", "Otevře okno s nastavením reakcí a síly vibrací", "MX Haptics")
        {
        }

        protected override void RunCommand(String actionParameter) => ((MxHapticsPlugin)this.Plugin).OpenSettings();
    }
}
