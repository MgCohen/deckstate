using DeckState.Core;

namespace DeckState.Samples.ReadyDeck;

public sealed class ReadyDeckRuntime(ReadyDeckContext context) : DeckRuntime<ReadyDeckContext>(context)
{
    protected override async ValueTask OnDispatchedAsync(DeckCommand command, CancellationToken cancellationToken)
    {
        if (!Context.Completed && Context.ReadyCount == Context.TotalButtonCount)
            Context.Complete();
        await TickRegisteredKeysAsync(cancellationToken);
    }
}
