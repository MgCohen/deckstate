namespace DeckState.StreamDeck;

public static class StreamDeckPluginHost
{
    public static async Task RunAsync(
        StreamDeckPluginArguments arguments,
        Func<StreamDeckConnection, CancellationToken, Task> run,
        CancellationToken cancellationToken)
    {
        await using var connection = await StreamDeckConnection.ConnectAsync(arguments.Port, cancellationToken);
        await connection.SendAsync(new { @event = arguments.RegisterEvent, uuid = arguments.PluginUuid }, cancellationToken);
        await run(connection, cancellationToken);
    }
}
