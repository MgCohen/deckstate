using System.Text.Json;

namespace DeckState.StreamDeck;

/// <summary>
/// Serializes framework messages to Stream Deck protocol JSON and reads raw inbound frames back.
/// The wire itself is an <see cref="IStreamDeckTransport"/> — the one thing that differs between
/// hardware and the emulator. Serialization, and everything above it, is shared by every path.
/// </summary>
public sealed class StreamDeckConnection(IStreamDeckTransport transport) : IAsyncDisposable
{
    /// <summary>Convenience for the hardware path: connect over the real host WebSocket.</summary>
    public static async Task<StreamDeckConnection> ConnectAsync(int port, CancellationToken cancellationToken) =>
        new(await WebSocketTransport.ConnectAsync(port, cancellationToken));

    public ValueTask SendAsync<T>(T message, CancellationToken cancellationToken) =>
        transport.SendAsync(JsonSerializer.Serialize(message), cancellationToken);

    public ValueTask<string> ReceiveAsync(CancellationToken cancellationToken) =>
        transport.ReceiveAsync(cancellationToken);

    public ValueTask DisposeAsync() => transport.DisposeAsync();
}
