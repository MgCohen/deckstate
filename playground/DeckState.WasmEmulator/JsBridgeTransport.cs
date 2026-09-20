using System.Threading.Channels;
using DeckState.StreamDeck;

namespace DeckState.WasmEmulator;

/// <summary>
/// The emulator's swap-in for the hardware WebSocket. It carries the exact same Stream Deck
/// protocol JSON frames, in-process: the UI enqueues inbound frames (willAppear / keyDown) with
/// <see cref="Deliver"/>, and the plugin's outbound frames (setImage / setTitle / registerPlugin)
/// surface through <see cref="Frame"/>. Same serialization, same plugin loop — only the pipe
/// changes.
/// </summary>
public sealed class JsBridgeTransport : IStreamDeckTransport
{
    private readonly Channel<string> _inbound = Channel.CreateUnbounded<string>();

    /// <summary>Outbound frames from the plugin (already serialized JSON).</summary>
    public event Action<string>? Frame;

    /// <summary>Feed an inbound frame (serialized JSON) to the plugin loop.</summary>
    public void Deliver(string json) => _inbound.Writer.TryWrite(json);

    public ValueTask SendAsync(string json, CancellationToken cancellationToken)
    {
        Frame?.Invoke(json);
        return ValueTask.CompletedTask;
    }

    public async ValueTask<string> ReceiveAsync(CancellationToken cancellationToken) =>
        await _inbound.Reader.ReadAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        _inbound.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
