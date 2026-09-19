namespace DeckState.Core;

internal interface IDeckKey
{
    string KeyId { get; }
    ValueTask HandleAsync(DeckCommand command, CancellationToken cancellationToken);
    ValueTask TickAsync(CancellationToken cancellationToken);
    ValueTask SetVisibleAsync(bool visible, CancellationToken cancellationToken);
}
