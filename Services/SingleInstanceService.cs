using System.IO;
using System.IO.Pipes;

namespace AppLauncher.Services
{
    /// <summary>
    /// Ensures only one instance runs at a time (spec §16). A second launch signals the
    /// first instance over a named pipe to show the overlay, then exits immediately.
    /// </summary>
    public class SingleInstanceService : IDisposable
    {
        private const string MutexName = "Local\\AppLauncher_SingleInstance_Mutex_9F3E2C";
        private const string PipeName = "AppLauncher_ShowOverlay_Pipe_9F3E2C";

        private Mutex? _mutex;
        private CancellationTokenSource? _listenerCts;

        public event Action? ShowRequested;

        /// <summary>Returns true if this is the first (primary) instance.</summary>
        public bool TryAcquire()
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            return createdNew;
        }

        /// <summary>Primary instance only: listens for "show overlay" signals from later launches.</summary>
        public void StartListening()
        {
            _listenerCts = new CancellationTokenSource();
            _ = ListenLoopAsync(_listenerCts.Token);
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(server);
                    string? message = await reader.ReadLineAsync();
                    if (message == "SHOW") ShowRequested?.Invoke();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Helpers.Logger.LogError(ex, "SingleInstanceService.ListenLoop");
                    try { await Task.Delay(250, token); } catch { break; }
                }
            }
        }

        /// <summary>Secondary instance only: signals the primary instance, then this instance exits.</summary>
        public static void SignalPrimaryInstance()
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(500);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine("SHOW");
            }
            catch
            {
                // Primary instance unreachable - nothing more a second instance can do
            }
        }

        public void Dispose()
        {
            _listenerCts?.Cancel();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }
}
