using System.Collections.Concurrent;
using System.Text.Json;
using DeckState.Core;

namespace DeckState.StreamDeck;

/// <summary>Maps a logical framework key to every visible Stream Deck action context.</summary>
public sealed class StreamDeckSurface(StreamDeckConnection connection) : IKeySurface
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _contexts = new();

    public void AddContext(string keyId, string context) =>
        _contexts.GetOrAdd(keyId, _ => new()).TryAdd(context, 0);

    public void RemoveContext(string keyId, string context)
    {
        if (_contexts.TryGetValue(keyId, out var contexts))
            contexts.TryRemove(context, out _);
    }

    public bool HasVisibleContexts(string keyId) =>
        _contexts.TryGetValue(keyId, out var contexts) && !contexts.IsEmpty;

    public async ValueTask DrawAsync(string keyId, KeyVisual visual, CancellationToken cancellationToken)
    {
        if (!_contexts.TryGetValue(keyId, out var contexts)) return;
        var image = visual.Svg is null ? null : $"data:image/svg+xml,{Uri.EscapeDataString(visual.Svg)}";

        foreach (var context in contexts.Keys)
        {
            if (image is not null)
                await connection.SendAsync(new { @event = "setImage", context, payload = new { image } }, cancellationToken);
            if (visual.Title is not null)
                await connection.SendAsync(new { @event = "setTitle", context, payload = new { title = visual.Title } }, cancellationToken);
            if (visual.Alert)
                await connection.SendAsync(new { @event = "showAlert", context }, cancellationToken);
        }
    }
}
