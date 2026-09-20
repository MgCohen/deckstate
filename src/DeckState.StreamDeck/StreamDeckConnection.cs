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

    public ValueTask SendAsync<T>(T message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        DeckLog.Write("out", Summarize(json));
        return transport.SendAsync(json, cancellationToken);
    }

    public async ValueTask<string> ReceiveAsync(CancellationToken cancellationToken)
    {
        var json = await transport.ReceiveAsync(cancellationToken);
        DeckLog.Write("in", json.Length > 600 ? json[..600] : json);
        return json;
    }

    // Event + context only — never the (potentially huge base64 image) payload.
    private static string Summarize(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var ev = root.TryGetProperty("event", out var e) ? e.GetString() : "?";
            var ctx = root.TryGetProperty("context", out var c) ? c.GetString() : "";
            return $"{ev} {ctx}".Trim();
        }
        catch { return json.Length > 120 ? json[..120] : json; }
    }

    public ValueTask DisposeAsync() => transport.DisposeAsync();
}
