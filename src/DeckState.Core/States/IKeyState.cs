namespace DeckState.Core;

public interface IKeyState<TContext> where TContext : IDeckContext
{
    bool IsValid(TContext context);
    ValueTask<StateReaction> OnEventAsync(KeyEvent @event, TContext context, CancellationToken cancellationToken);
    ValueTask<StateReaction> OnTickAsync(TContext context, CancellationToken cancellationToken);
    KeyVisual Draw(TContext context);
}
