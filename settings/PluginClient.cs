namespace MxHaptics.Settings
{
    using System;
    using System.IO;
    using System.IO.Pipes;

    using MxHaptics.Shared;

    /// <summary>Posílá příkazy pluginu přes lokální pojmenovanou rouru.</summary>
    internal static class PluginClient
    {
        /// <returns>Odpověď pluginu, nebo null, když plugin neběží.</returns>
        public static String Send(String command)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", HapticsSettings.PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
                client.Connect(500);
                using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(client, leaveOpen: true);
                writer.WriteLine(command);
                return reader.ReadLine();
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
