namespace DeckState.Core;

public sealed class Key<TContext> : IDeckKey where TContext : IDeckContext
{
    private readonly IReadOnlyList<IKeyState<TContext>> _states;
    private readonly Func<DeckCommand, KeyEvent?> _mapInput;
    private readonly TContext _context;
    private readonly IKeySurface _surface;
    private IKeyState<TContext>? _current;
    private KeyVisual? _lastVisual;
    private bool _visible;

    public Key(string keyId, TContext context, IKeySurface surface, Func<DeckCommand, KeyEvent?> mapInput, params IKeyState<TContext>[] states)
    {
        if (states.Length == 0) throw new ArgumentException("A key needs at least one state.", nameof(states));
        KeyId = keyId; _context = context; _surface = surface; _mapInput = mapInput; _states = states;
    }

    public string KeyId { get; }

    public async ValueTask HandleAsync(DeckCommand command, CancellationToken cancellationToken)
    {
        var input = _mapInput(command);
        if (input is null) return;
        var reaction = _current is null ? StateReaction.None : await _current.OnEventAsync(input, _context, cancellationToken);
        await UpdateAsync(reaction.RequestRender, cancellationToken);
    }

    public async ValueTask TickAsync(CancellationToken cancellationToken)
    {
        var reaction = _current is null ? StateReaction.None : await _current.OnTickAsync(_context, cancellationToken);
        await UpdateAsync(reaction.RequestRender, cancellationToken);
    }

    public async ValueTask SetVisibleAsync(bool visible, CancellationToken cancellationToken)
    {
        _visible = visible;
        if (visible) await UpdateAsync(forceRender: true, cancellationToken);
    }

    private async ValueTask UpdateAsync(bool forceRender, CancellationToken cancellationToken)
    {
        var changed = await ReconcileStateAsync(cancellationToken);
        if (!_visible || _current is null || (!forceRender && !changed)) return;
        var visual = _current.Draw(_context);
        if (forceRender || !Equals(visual, _lastVisual))
        {
            _lastVisual = visual;
            await _surface.DrawAsync(KeyId, visual, cancellationToken);
        }
    }

    private async ValueTask<bool> ReconcileStateAsync(CancellationToken cancellationToken)
    {
        if (_current is not null && _current.IsValid(_context)) return false;
        var next = _states.FirstOrDefault(state => state.IsValid(_context))
            ?? throw new InvalidOperationException($"No valid state for key '{KeyId}'.");
        if (ReferenceEquals(next, _current)) return false;
        _current = next;
        return true;
    }
}
