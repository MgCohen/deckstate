using System.Threading.Channels;

namespace DeckState.Core;

public class DeckRuntime<TContext> : IAsyncDisposable where TContext : IDeckContext
{
    private readonly Dictionary<string, IDeckKey> _keys = new(StringComparer.Ordinal);
    private readonly Channel<WorkItem> _queue = Channel.CreateUnbounded<WorkItem>();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _pump;
    private readonly Task _heartbeat;

    public DeckRuntime(TContext context, TimeSpan? tickInterval = null)
    {
        Context = context;
        _pump = PumpAsync();
        _heartbeat = HeartbeatAsync(tickInterval ?? TimeSpan.FromMilliseconds(33), _stop.Token);
    }

    public TContext Context { get; }

    public void Register(Key<TContext> key)
    {
        if (!_keys.TryAdd(key.KeyId, key)) throw new InvalidOperationException($"Duplicate key '{key.KeyId}'.");
    }

    public ValueTask RegisterAsync(Key<TContext> key, CancellationToken cancellationToken = default) =>
        EnqueueAsync(_ =>
        {
            Register(key);
            return ValueTask.CompletedTask;
        }, cancellationToken);

    public ValueTask UnregisterAsync(string keyId, CancellationToken cancellationToken = default) =>
        EnqueueAsync(_ =>
        {
            _keys.Remove(keyId);
            return ValueTask.CompletedTask;
        }, cancellationToken);

    public ValueTask DispatchAsync(DeckCommand command, CancellationToken cancellationToken = default) =>
        EnqueueAsync(async ct =>
        {
            await Find(command.KeyId).HandleAsync(command, ct);
            await OnDispatchedAsync(command, ct);
        }, cancellationToken);

    public ValueTask SetVisibleAsync(string keyId, bool visible, CancellationToken cancellationToken = default) =>
        EnqueueAsync(ct => Find(keyId).SetVisibleAsync(visible, ct), cancellationToken);

    public ValueTask TickAsync(CancellationToken cancellationToken = default) =>
        EnqueueAsync(TickRegisteredKeysAsync, cancellationToken);

    protected virtual ValueTask OnDispatchedAsync(DeckCommand command, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    protected async ValueTask TickRegisteredKeysAsync(CancellationToken cancellationToken)
    {
        foreach (var key in _keys.Values)
            await key.TickAsync(cancellationToken);
    }

    private async Task HeartbeatAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        try { while (await timer.WaitForNextTickAsync(cancellationToken)) await TickAsync(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private ValueTask EnqueueAsync(Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_queue.Writer.TryWrite(new WorkItem(work, done))) throw new InvalidOperationException("Runtime has stopped.");
        return new ValueTask(done.Task.WaitAsync(cancellationToken));
    }

    private async Task PumpAsync()
    {
        await foreach (var item in _queue.Reader.ReadAllAsync(_stop.Token))
        {
            try { await item.Work(_stop.Token); item.Done.SetResult(); }
            catch (Exception ex) { item.Done.SetException(ex); }
        }
    }

    private IDeckKey Find(string keyId) => _keys.GetValueOrDefault(keyId) ?? throw new KeyNotFoundException(keyId);

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel(); _queue.Writer.TryComplete();
        try { await Task.WhenAll(_pump, _heartbeat); } catch (OperationCanceledException) { }
        _stop.Dispose();
    }
    private sealed record WorkItem(Func<CancellationToken, ValueTask> Work, TaskCompletionSource Done);
}
