namespace DeckState.StreamDeck;

/// <summary>
/// The single swappable seam between the framework and the outside world. It carries raw
/// Stream Deck protocol frames (JSON text) in both directions. Real hardware uses a WebSocket
/// (<see cref="WebSocketTransport"/>); the in-process emulator uses a bridge. Everything above
/// this line — JSON serialization, the plugin message loop, the state machine and SVG
/// rendering — is identical on every path.
/// </summary>
public interface IStreamDeckTransport : IAsyncDisposable
{
    /// <summary>Framework -> host. A serialized protocol frame (e.g. setImage, registerPlugin).</summary>
    ValueTask SendAsync(string json, CancellationToken cancellationToken);

    /// <summary>Host -> framework. The next inbound protocol frame (e.g. willAppear, keyDown).</summary>
    ValueTask<string> ReceiveAsync(CancellationToken cancellationToken);
}
