namespace Loupedeck.MxHapticsPlugin
{
    using System;

    // Plugin nemá vlastní aplikaci (HasNoApplication), loader ale třídu ClientApplication očekává.
    public class MxHapticsApplication : ClientApplication
    {
        protected override String GetProcessName() => "";

        protected override String GetBundleName() => "";

        public override ClientApplicationStatus GetApplicationStatus() => ClientApplicationStatus.Unknown;
    }
}
