namespace MxHaptics.Settings
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Threading;

    using MxHaptics.Shared;

    /// <summary>Posílá příkazy pluginu přes lokální pojmenovanou rouru.</summary>
    internal static class PluginClient
    {
        private const Int32 ConnectTimeoutMs = 500;

        // volá se i z UI vlákna: zaseknutý plugin nesmí okno zamrazit
        private const Int32 ReplyTimeoutMs = 1500;

        /// <returns>Odpověď pluginu, nebo null, když plugin neběží nebo neodpověděl včas.</returns>
        public static String Send(String command)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", HapticsSettings.PipeName, PipeDirection.InOut,
                    PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
                client.Connect(ConnectTimeoutMs);
                using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(client, leaveOpen: true);
                using var timeout = new CancellationTokenSource(ReplyTimeoutMs);
                writer.WriteLineAsync(command.AsMemory(), timeout.Token).GetAwaiter().GetResult();
                return reader.ReadLineAsync(timeout.Token).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                return null;
            }
        }
    }
}
