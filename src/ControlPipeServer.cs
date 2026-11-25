namespace Loupedeck.MxHapticsPlugin
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Pipes;
    using System.Threading;
    using System.Threading.Tasks;

    using MxHaptics.Shared;

    /// <summary>
    /// Lokální pojmenovaná roura pro okno nastavení. Přístupná jen pro aktuálního uživatele (CurrentUserOnly).
    /// Protokol: jeden řádek příkazu, jeden řádek odpovědi.
    /// </summary>
    internal sealed class ControlPipeServer : IDisposable
    {
        private readonly Func<String, String> _handler;
        private readonly CancellationTokenSource _cts = new();
        private Task _loop;

        public ControlPipeServer(Func<String, String> handler) => this._handler = handler;

        public void Start() => this._loop = Task.Run(() => this.RunAsync(this._cts.Token));

        public void Dispose()
        {
            this._cts.Cancel();
            try
            {
                this._loop?.Wait(1000);
            }
            catch (AggregateException)
            {
            }

            this._cts.Dispose();
        }

        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        HapticsSettings.PipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(server, leaveOpen: true);
                    using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };

                    var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    var response = line == null ? "error empty" : this._handler(line.Trim());
                    await writer.WriteLineAsync(response).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"MxHaptics pipe: {ex.Message}");
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
            }
        }
    }
}
