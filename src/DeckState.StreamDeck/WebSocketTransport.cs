using System.Net.WebSockets;
using System.Text;

namespace DeckState.StreamDeck;

/// <summary>
/// The real transport used on hardware: an Elgato Stream Deck host WebSocket. This is the only
/// piece the emulator swaps out.
/// </summary>
public sealed class WebSocketTransport : IStreamDeckTransport
{
    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public static async Task<WebSocketTransport> ConnectAsync(int port, CancellationToken cancellationToken)
    {
        var transport = new WebSocketTransport();
        await transport._socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"), cancellationToken);
        return transport;
    }

    public async ValueTask SendAsync(string json, CancellationToken cancellationToken)
    {
        if (_socket.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(json);
        await _writeLock.WaitAsync(cancellationToken);
        try { await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken); }
        catch (WebSocketException) when (_socket.State != WebSocketState.Open) { }
        finally { _writeLock.Release(); }
    }

    public async ValueTask<string> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<byte>(new byte[8 * 1024]);
        using var output = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await _socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("Stream Deck closed the WebSocket.");
            output.Write(buffer.Array!, buffer.Offset, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket.State == WebSocketState.Open)
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None);
        _socket.Dispose();
        _writeLock.Dispose();
    }
}
