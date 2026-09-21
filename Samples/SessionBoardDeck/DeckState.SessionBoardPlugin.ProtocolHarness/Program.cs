using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DeckState.Samples.SessionBoardDeck;
using DeckState.StreamDeck;

// Headless regression harness for SessionBoard — no hardware, no browser.
// It replays the EXACT frame sequence the Stream Deck host sends on a real device,
// including the action-less frames (deviceDidConnect, applicationDidLaunch) that the
// plugin's message loop must skip. The keyDown crash (GetProperty("action") on a frame
// with no "action") reproduced here deterministically; this asserts it stays fixed.

const int columns = 4;
const string device = "harness-device";

// Hard watchdog: whatever wedges (a crashed message loop can leave a send parked on a half-closed
// socket, before any per-step timeout), the harness always exits non-zero with a legible reason.
_ = Task.Run(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(30));
    Console.Error.WriteLine("Harness watchdog fired: the plugin never completed the protocol exchange (likely a message-loop crash).");
    Environment.Exit(1);
});

await using var server = await LoopbackStreamDeckServer.StartAsync();
using var stop = new CancellationTokenSource();

var source = new FakeSource();
var opener = new RecordingOpener();
var hostTask = StreamDeckPluginHost.RunAsync(
    new StreamDeckPluginArguments(server.Port, "harness-plugin-instance", "registerPlugin"),
    (connection, ct) => SessionBoardPlugin.RunAsync(connection, source, opener, columns, ct),
    stop.Token);

await server.AcceptAsync();
using (var registration = JsonDocument.Parse(await server.ReceiveAsync()))
    Assert(registration.RootElement.GetProperty("event").GetString() == "registerPlugin", "Plugin did not register.");

// 1) Action-less frames FIRST — the real host sends these before any willAppear.
//    The old loop threw on GetProperty("action") here and never processed a key again.
await server.SendAsync(new { @event = "deviceDidConnect", device, deviceInfo = new { name = "Stream Deck", type = 0 } });
await server.SendAsync(new { @event = "applicationDidLaunch", payload = new { application = "chrome" } });

// 2) Two session tiles appear (slots 0 and 1).
const string ctx0 = "session-0";
const string ctx1 = "session-1";
await server.SendActionAsync(SessionBoardPlugin.SessionAction, "willAppear", ctx0, device, 0, 0);
await server.SendActionAsync(SessionBoardPlugin.SessionAction, "willAppear", ctx1, device, 0, 1);

// Wait for the source to be polled at least once (snapshot populated) and both tiles to render.
// A dead message loop (the GetProperty("action") crash) never renders — so a timeout here is the
// regression tripping, reported as a clear failure rather than a silent hang.
await WithTimeout(source.Polled.Task, "source first poll");
await WithTimeout(server.ReceiveImageForContextAsync(ctx0), "render tile 0 (message loop alive after deviceDidConnect?)");
await WithTimeout(server.ReceiveImageForContextAsync(ctx1), "render tile 1");

// 3) Press slot 0 — must dispatch through the SAME loop that survived the action-less frames.
await server.SendActionAsync(SessionBoardPlugin.SessionAction, "keyDown", ctx0, device, 0, 0);

var opened = await WithTimeout(opener.Opened.Task, "keyDown open URL");
Assert(opened == FakeSource.Session0Url, $"keyDown opened '{opened}', expected '{FakeSource.Session0Url}'.");

// Shut down. The plugin loop parks in a WebSocket receive that will not observe the token, so
// bound the wait rather than block forever; a lingering background task cannot fail the test.
stop.Cancel();
await Task.WhenAny(hostTask, Task.Delay(TimeSpan.FromSeconds(5)));
Console.WriteLine("SessionBoard protocol test passed: survived deviceDidConnect + applicationDidLaunch, keyDown opened the session URL.");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task<T> WithTimeout<T>(Task<T> task, string what)
{
    if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))) != task)
        throw new InvalidOperationException($"Timed out waiting for: {what}. The plugin message loop likely crashed on a protocol frame.");
    return await task;
}

sealed class FakeSource : ISessionBoardSource
{
    public const string Session0Url = "https://battlebox.example/session/alpha";
    public readonly TaskCompletionSource<bool> Polled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<SessionBoardSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        Polled.TrySetResult(true);
        return Task.FromResult(new SessionBoardSnapshot(DateTimeOffset.UtcNow,
        [
            new BoardSession("alpha", "chrome", "Alpha", "working", true, "needs you", Session0Url),
            new BoardSession("beta", "chrome", "Beta", "review", false, "in review", "https://battlebox.example/session/beta"),
        ]));
    }
}

sealed class RecordingOpener : ISessionOpener
{
    public readonly TaskCompletionSource<string> Opened = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ValueTask OpenAsync(string url, CancellationToken cancellationToken)
    {
        Opened.TrySetResult(url);
        return ValueTask.CompletedTask;
    }
}

sealed class LoopbackStreamDeckServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private WebSocket? _socket;
    private readonly SemaphoreSlim _writes = new(1, 1);

    private LoopbackStreamDeckServer(HttpListener listener, int port) => (_listener, Port) = (listener, port);
    public int Port { get; }

    public static Task<LoopbackStreamDeckServer> StartAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
                return Task.FromResult(new LoopbackStreamDeckServer(listener, port));
            }
            catch (HttpListenerException) when (attempt < 9)
            {
                listener.Close();
            }
        }
        throw new InvalidOperationException("Could not allocate a loopback port for the protocol harness.");
    }

    public async Task AcceptAsync()
    {
        var context = await _listener.GetContextAsync();
        if (!context.Request.IsWebSocketRequest) throw new InvalidOperationException("Plugin did not request a WebSocket.");
        _socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
    }

    public async Task SendAsync<T>(T message)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await _writes.WaitAsync();
        try { await Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
        finally { _writes.Release(); }
    }

    public async Task<string> ReceiveAsync()
    {
        var buffer = new byte[8192];
        using var output = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await Socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("Plugin closed before response.");
            output.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    public Task SendActionAsync(string action, string @event, string context, string device, int row, int column) =>
        SendAsync(new { action, @event, context, device, payload = new { coordinates = new { column, row }, settings = new { } } });

    public async Task<string> ReceiveImageForContextAsync(string expectedContext)
    {
        while (true)
        {
            var message = await ReceiveAsync();
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            if (root.GetProperty("event").GetString() != "setImage") continue;
            if (root.GetProperty("context").GetString() != expectedContext) continue;
            return root.GetProperty("payload").GetProperty("image").GetString() ?? "";
        }
    }

    private WebSocket Socket => _socket ?? throw new InvalidOperationException("No WebSocket connection.");

    public async ValueTask DisposeAsync()
    {
        if (_socket?.State == WebSocketState.Open)
        {
            try { await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", CancellationToken.None); }
            catch (WebSocketException) { }
        }
        _socket?.Dispose();
        _writes.Dispose();
        _listener.Close();
    }
}
