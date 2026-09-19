using System.Text.Json;
using DeckState.Core;
using DeckState.StreamDeck;

namespace DeckState.Samples.ReadyDeck;

public static class ReadyDeckPlugin
{
    public const string WorkAction = "com.snowprint.deckstate.ready-work";
    public const string CounterAction = "com.snowprint.deckstate.ready-counter";

    private const string CounterKey = "ready-counter";

    public static async Task RunAsync(StreamDeckConnection connection, CancellationToken cancellationToken)
    {
        var surface = new StreamDeckSurface(connection);
        var context = new ReadyDeckContext();
        await using var deck = new ReadyDeckRuntime(context);
        var keysByContext = new Dictionary<string, string>(StringComparer.Ordinal);
        var counterRegistered = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            using var message = JsonDocument.Parse(await connection.ReceiveAsync(cancellationToken));
            var root = message.RootElement;
            if (!root.TryGetProperty("action", out var action) || !root.TryGetProperty("context", out var contextElement)) continue;

            var actionId = action.GetString();
            var streamDeckContext = contextElement.GetString()!;
            if (actionId != WorkAction && actionId != CounterAction) continue;

            switch (root.GetProperty("event").GetString())
            {
                case "willAppear":
                    var keyId = actionId == CounterAction ? CounterKey : $"work:{streamDeckContext}";
                    if (!keysByContext.ContainsKey(streamDeckContext))
                    {
                        keysByContext.Add(streamDeckContext, keyId);
                        if (actionId == CounterAction)
                        {
                            if (!counterRegistered)
                            {
                                await deck.RegisterAsync(ReadyCountSpinnerKey.Create(CounterKey, context, surface), cancellationToken);
                                counterRegistered = true;
                            }
                        }
                        else
                        {
                            context.AddWorkKey(keyId);
                            await deck.RegisterAsync(ReadyButtonKey.Create(keyId, context, surface), cancellationToken);
                        }
                    }
                    surface.AddContext(keyId, streamDeckContext);
                    await deck.SetVisibleAsync(keyId, true, cancellationToken);
                    break;
                case "willDisappear":
                    if (keysByContext.TryGetValue(streamDeckContext, out var disappearedKey))
                    {
                        surface.RemoveContext(disappearedKey, streamDeckContext);
                        await deck.SetVisibleAsync(disappearedKey, surface.HasVisibleContexts(disappearedKey), cancellationToken);
                        if (!surface.HasVisibleContexts(disappearedKey))
                        {
                            keysByContext.Remove(streamDeckContext);
                            if (disappearedKey == CounterKey) counterRegistered = false;
                            else context.RemoveWorkKey(disappearedKey);
                            await deck.UnregisterAsync(disappearedKey, cancellationToken);
                        }
                    }
                    break;
                case "keyDown":
                    if (keysByContext.TryGetValue(streamDeckContext, out var pressedKey))
                        await deck.DispatchAsync(new DeckCommand(pressedKey, "press"), cancellationToken);
                    break;
            }
        }
    }
}
