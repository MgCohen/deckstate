namespace DeckState.Core;

public abstract class KeyState<TContext> : IKeyState<TContext> where TContext : IDeckContext
{
    public abstract bool IsValid(TContext context);
    public virtual ValueTask<StateReaction> OnEventAsync(KeyEvent @event, TContext context, CancellationToken cancellationToken) => ValueTask.FromResult(StateReaction.None);
    public virtual ValueTask<StateReaction> OnTickAsync(TContext context, CancellationToken cancellationToken) => ValueTask.FromResult(StateReaction.None);
    public abstract KeyVisual Draw(TContext context);
}
