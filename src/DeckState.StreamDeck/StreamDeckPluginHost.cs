namespace DeckState.StreamDeck;

public static class StreamDeckPluginHost
{
    /// <summary>Hardware path: connect the real host WebSocket, register, run the plugin loop.</summary>
    public static async Task RunAsync(
        StreamDeckPluginArguments arguments,
        Func<StreamDeckConnection, CancellationToken, Task> run,
        CancellationToken cancellationToken)
    {
        await using var transport = await WebSocketTransport.ConnectAsync(arguments.Port, cancellationToken);
        await RunAsync(transport, arguments, run, cancellationToken);
    }

    /// <summary>
    /// Any path: register and run the plugin loop over the supplied transport. The emulator passes
    /// its in-process bridge here so it runs the exact same plugin loop as hardware. The caller owns
    /// the transport's lifetime.
    /// </summary>
    public static async Task RunAsync(
        IStreamDeckTransport transport,
        StreamDeckPluginArguments arguments,
        Func<StreamDeckConnection, CancellationToken, Task> run,
        CancellationToken cancellationToken)
    {
        var connection = new StreamDeckConnection(transport);
        await connection.SendAsync(new { @event = arguments.RegisterEvent, uuid = arguments.PluginUuid }, cancellationToken);
        await run(connection, cancellationToken);
    }
}
