using System.Diagnostics;
using System.IO.Pipes;

namespace KeyboardLauncher;

// On-demand counters only: no typed text, key history, or application titles.
internal sealed class InputStatusChannel : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private static string PipeName => $"KeyboardLauncher.InputStatus.{Environment.UserName}.{Process.GetCurrentProcess().SessionId}";

    internal InputStatusChannel(Func<string> snapshot)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.Out, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(stop.Token);
                    try
                    {
                        using var writer = new StreamWriter(server) { AutoFlush = true };
                        await writer.WriteLineAsync(snapshot().AsMemory(), stop.Token);
                    }
                    catch (IOException) { /* A diagnostic client may have disconnected. */ }
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    internal static async Task<string> ReadAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.In,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(timeout.Token);
        using var reader = new StreamReader(client);
        return await reader.ReadLineAsync(timeout.Token) ?? throw new IOException("No input status received.");
    }

    public void Dispose() => stop.Cancel();
}
