using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DeckState.Samples.ReadyDeck;
using DeckState.StreamDeck;

var artifacts = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/streamdeck-protocol");
Directory.CreateDirectory(artifacts);

await using var server = await LoopbackStreamDeckServer.StartAsync();
using var stop = new CancellationTokenSource();
var hostTask = StreamDeckPluginHost.RunAsync(
    new StreamDeckPluginArguments(server.Port, "harness-plugin-instance", "registerPlugin"),
    ReadyDeckPlugin.RunAsync,
    stop.Token);

await server.AcceptAsync();
var registration = await server.ReceiveAsync();
using (var document = JsonDocument.Parse(registration))
{
    Assert(document.RootElement.GetProperty("event").GetString() == "registerPlugin", "Plugin did not register.");
    Assert(document.RootElement.GetProperty("uuid").GetString() == "harness-plugin-instance", "Wrong plugin UUID.");
}

const string device = "harness-device";

// The real host sends action-less frames (deviceDidConnect) before any willAppear. Replay one so
// the plugin loop is exercised against a frame with no "action"/"context" field — it must skip it.
await server.SendAsync(new { @event = "deviceDidConnect", device, deviceInfo = new { name = "Stream Deck", type = 0 } });

const string counterContext = "counter";
await server.SendActionAsync(ReadyDeckPlugin.CounterAction, "willAppear", counterContext, device);
var initial = new Dictionary<string, string> { [counterContext] = await server.ReceiveImageForContextAsync(counterContext) };

var workContexts = Enumerable.Range(1, 11).Select(index => $"work-{index:00}").ToArray();
foreach (var context in workContexts)
{
    await server.SendActionAsync(ReadyDeckPlugin.WorkAction, "willAppear", context, device);
    initial[context] = await server.ReceiveImageForContextAsync(context);
}
Assert(initial[counterContext].Contains("#284560", StringComparison.OrdinalIgnoreCase), "Counter did not render its initial state.");
Assert(initial[workContexts[0]].Contains("#34495E", StringComparison.OrdinalIgnoreCase), "Work key did not render IDLE.");

foreach (var context in workContexts[..^1])
{
    await server.SendActionAsync(ReadyDeckPlugin.WorkAction, "keyDown", context, device);
    await server.ReceiveImageForContextAsync(context);
    await server.ReceiveImageForContextAsync(counterContext);
}

await server.SendActionAsync(ReadyDeckPlugin.WorkAction, "keyDown", workContexts[^1], device);
var completedCounter = await server.ReceiveImageForContextAsync(counterContext);
var completedWork = new Dictionary<string, string>();
foreach (var context in workContexts) completedWork[context] = await server.ReceiveImageForContextAsync(context);
Assert(completedWork.Values.All(image => image.Contains("#4C3C79", StringComparison.OrdinalIgnoreCase)), "Ready deck did not promote work keys to DONE.");
Assert(completedCounter.Contains("#284560", StringComparison.OrdinalIgnoreCase), "Counter did not render ALL DONE.");

await server.SendActionAsync(ReadyDeckPlugin.CounterAction, "keyDown", counterContext, device);
var resetCounter = await server.ReceiveImageForContextAsync(counterContext);
var resetWork = new Dictionary<string, string>();
foreach (var context in workContexts) resetWork[context] = await server.ReceiveImageForContextAsync(context);
Assert(resetWork.Values.All(image => image.Contains("#34495E", StringComparison.OrdinalIgnoreCase)), "Counter did not reset work keys to IDLE.");
Assert(resetCounter.Contains("#284560", StringComparison.OrdinalIgnoreCase), "Counter did not reset to READY 1/12.");

await File.WriteAllTextAsync(Path.Combine(artifacts, "01-initial-counter.svg"), initial[counterContext]);
await File.WriteAllTextAsync(Path.Combine(artifacts, "02-completed-counter.svg"), completedCounter);
await File.WriteAllTextAsync(Path.Combine(artifacts, "03-reset-counter.svg"), resetCounter);
await WriteGridAsync("01-initial-grid.svg", "Initial", initial, artifacts);
completedWork[counterContext] = completedCounter;
await WriteGridAsync("02-completed-grid.svg", "All ready", completedWork, artifacts);
resetWork[counterContext] = resetCounter;
await WriteGridAsync("03-reset-grid.svg", "Reset", resetWork, artifacts);

stop.Cancel();
try { await hostTask; } catch (OperationCanceledException) { }
Console.WriteLine($"Protocol test passed. Rendered Stream Deck SVGs: {artifacts}");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static Task WriteGridAsync(string fileName, string title, IReadOnlyDictionary<string, string> frames, string artifacts)
{
    var ordered = frames.OrderBy(pair => pair.Key == "counter" ? "zzzz" : pair.Key).ToArray();
    var keys = ordered.Select((pair, index) =>
        $"<image x='{24 + index % 4 * 156}' y='{88 + index / 4 * 156}' width='144' height='144' href='data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Value))}'/>");
    var grid = $$"""
        <svg xmlns="http://www.w3.org/2000/svg" width="672" height="570" viewBox="0 0 672 570">
          <rect width="672" height="570" fill="#0c111b"/>
          <text x="24" y="40" fill="#eef4ff" font-family="Arial" font-size="26" font-weight="bold">Native Elgato emulator · {{title}}</text>
          <text x="24" y="64" fill="#aab5c6" font-family="Arial" font-size="14">Actual setImage frames emitted by DeckState.ReadyDeckPlugin</text>
          {{string.Join("", keys)}}
        </svg>
        """;
    return File.WriteAllTextAsync(Path.Combine(artifacts, fileName), grid);
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
        // HttpListener cannot bind port 0. Retry in the tiny interval between
        // releasing the probe socket and claiming that port for WebSockets.
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

    public Task SendActionAsync(string action, string @event, string context, string device) =>
        SendAsync(new { action, @event, context, device, payload = new { coordinates = new { column = 0, row = 0 }, settings = new { } } });

    public async Task<(string Context, string Image)> ReceiveSetImageAsync()
    {
        while (true)
        {
            var message = await ReceiveAsync();
            using var document = JsonDocument.Parse(message);
            if (document.RootElement.GetProperty("event").GetString() != "setImage") continue;
            var url = document.RootElement.GetProperty("payload").GetProperty("image").GetString()
                ?? throw new InvalidOperationException("setImage did not include image data.");
            const string prefix = "data:image/svg+xml,";
            if (!url.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Expected SVG data URL.");
            return (document.RootElement.GetProperty("context").GetString()!, Uri.UnescapeDataString(url[prefix.Length..]));
        }
    }

    public async Task<string> ReceiveImageForContextAsync(string expectedContext)
    {
        while (true)
        {
            var frame = await ReceiveSetImageAsync();
            if (frame.Context == expectedContext) return frame.Image;
        }
    }

    public async Task<List<(string Context, string Image)>> ReceiveImagesAsync(int count)
    {
        var images = new List<(string Context, string Image)>();
        while (images.Count < count) images.Add(await ReceiveSetImageAsync());
        return images;
    }

    private WebSocket Socket => _socket ?? throw new InvalidOperationException("No WebSocket connection.");

    public async ValueTask DisposeAsync()
    {
        if (_socket?.State == WebSocketState.Open)
        {
            try { await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", CancellationToken.None); }
            catch (WebSocketException) { /* host cancellation can close first */ }
        }
        _socket?.Dispose();
        _writes.Dispose();
        _listener.Close();
    }
}
