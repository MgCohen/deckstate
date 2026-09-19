namespace DeckState.Core;

public sealed record DeckCommand(string KeyId, string Name, object? Payload = null);
