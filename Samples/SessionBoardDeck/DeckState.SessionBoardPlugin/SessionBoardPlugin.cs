using System.Text.Json;
using DeckState.Core;
using DeckState.StreamDeck;

namespace DeckState.Samples.SessionBoardDeck;

public static class SessionBoardPlugin
{
    public const string SessionAction = "com.snowprint.deckstate.session-board.session";
    private const int Columns = 4;

    public static async Task RunAsync(StreamDeckConnection connection, CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var endpoint = new Uri(Environment.GetEnvironmentVariable("DECKSTATE_BATTLEBOX_URL") ?? "http://127.0.0.1:8787/api/sessions");

        // Cloudflare Access service token (machine-to-machine). When both are set,
        // send them on every request so a CF Access-protected endpoint authorizes the poll.
        var cfClientId = Environment.GetEnvironmentVariable("DECKSTATE_BATTLEBOX_CF_CLIENT_ID");
        var cfClientSecret = Environment.GetEnvironmentVariable("DECKSTATE_BATTLEBOX_CF_CLIENT_SECRET");
        if (!string.IsNullOrEmpty(cfClientId) && !string.IsNullOrEmpty(cfClientSecret))
        {
            http.DefaultRequestHeaders.Add("CF-Access-Client-Id", cfClientId);
            http.DefaultRequestHeaders.Add("CF-Access-Client-Secret", cfClientSecret);
        }

        var client = new BattleboxSessionBoardClient(http, endpoint);
        var surface = new StreamDeckSurface(connection);
        var context = new SessionBoardContext();
        await using var deck = new SessionBoardRuntime(context);
        var keyByContext = new Dictionary<string, string>(StringComparer.Ordinal);

        var refresh = RefreshLoop(deck, client, cancellationToken);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var message = JsonDocument.Parse(await connection.ReceiveAsync(cancellationToken));
                var root = message.RootElement;
                if (root.GetProperty("action").GetString() != SessionAction || !root.TryGetProperty("context", out var contextElement)) continue;
                var streamDeckContext = contextElement.GetString()!;

                switch (root.GetProperty("event").GetString())
                {
                    case "willAppear":
                        var keyId = $"session:{streamDeckContext}";
                        if (!keyByContext.ContainsKey(streamDeckContext))
                        {
                            var coordinates = root.GetProperty("payload").GetProperty("coordinates");
                            var slot = coordinates.GetProperty("row").GetInt32() * Columns + coordinates.GetProperty("column").GetInt32();
                            keyByContext.Add(streamDeckContext, keyId);
                            await deck.RegisterAsync(SessionBoardKey.Create(keyId, slot, context, surface), cancellationToken);
                        }
                        surface.AddContext(keyId, streamDeckContext);
                        await deck.SetVisibleAsync(keyId, true, cancellationToken);
                        break;
                    case "willDisappear":
                        if (keyByContext.Remove(streamDeckContext, out var disappeared))
                        {
                            surface.RemoveContext(disappeared, streamDeckContext);
                            await deck.UnregisterAsync(disappeared, cancellationToken);
                        }
                        break;
                    case "keyDown":
                        if (keyByContext.TryGetValue(streamDeckContext, out var pressed))
                            await deck.DispatchAsync(new DeckCommand(pressed, "press"), cancellationToken);
                        break;
                }
            }
        }
        finally { await refresh; }
    }

    private static async Task RefreshLoop(SessionBoardRuntime deck, BattleboxSessionBoardClient client, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await deck.RefreshAsync(client, cancellationToken); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                deck.Context.SetSnapshot(new SessionBoardSnapshot(DateTimeOffset.UtcNow, [], "Battlebox unavailable."));
                await deck.TickAsync(cancellationToken);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
