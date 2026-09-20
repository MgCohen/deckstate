using System.Text.Json;
using DeckState.Core;
using DeckState.StreamDeck;

namespace DeckState.Samples.SessionBoardDeck;

public static partial class SessionBoardPlugin
{
    public const string SessionAction = "com.snowprint.deckstate.session-board.session";

    /// <summary>
    /// The real SessionBoard loop over injected seams. Hardware and the emulator share this exact
    /// code — only the <see cref="ISessionBoardSource"/> (where sessions come from), the
    /// <see cref="ISessionOpener"/> (how a key press opens a URL) and the deck's column count differ.
    /// </summary>
    public static async Task RunAsync(
        StreamDeckConnection connection,
        ISessionBoardSource source,
        ISessionOpener opener,
        int columns,
        CancellationToken cancellationToken)
    {
        var surface = new StreamDeckSurface(connection);
        var context = new SessionBoardContext();
        await using var deck = new SessionBoardRuntime(context);
        var keyByContext = new Dictionary<string, string>(StringComparer.Ordinal);

        var refresh = RefreshLoop(deck, source, cancellationToken);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var message = JsonDocument.Parse(await connection.ReceiveAsync(cancellationToken));
                var root = message.RootElement;
                if (!root.TryGetProperty("action", out var action) || action.GetString() != SessionAction || !root.TryGetProperty("context", out var contextElement)) continue;
                var streamDeckContext = contextElement.GetString()!;

                switch (root.GetProperty("event").GetString())
                {
                    case "willAppear":
                        var keyId = $"session:{streamDeckContext}";
                        if (!keyByContext.ContainsKey(streamDeckContext))
                        {
                            var coordinates = root.GetProperty("payload").GetProperty("coordinates");
                            var slot = coordinates.GetProperty("row").GetInt32() * columns + coordinates.GetProperty("column").GetInt32();
                            keyByContext.Add(streamDeckContext, keyId);
                            await deck.RegisterAsync(SessionBoardKey.Create(keyId, slot, context, surface, opener), cancellationToken);
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
                        {
                            try { await deck.DispatchAsync(new DeckCommand(pressed, "press"), cancellationToken); }
                            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { /* shutting down */ }
                            catch (Exception ex) { DeckLog.Write("error", $"press dispatch failed: {ex}"); }
                        }
                        break;
                }
            }
        }
        finally { await refresh; }
    }

    private static async Task RefreshLoop(SessionBoardRuntime deck, ISessionBoardSource source, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await deck.RefreshAsync(source, cancellationToken); }
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
