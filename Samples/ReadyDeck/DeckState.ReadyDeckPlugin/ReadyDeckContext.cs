using DeckState.Core;

namespace DeckState.Samples.ReadyDeck;

public sealed class ReadyDeckContext : IDeckContext
{
    private readonly HashSet<string> _workKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _readyKeys = new(StringComparer.Ordinal);

    public int TotalButtonCount => _workKeys.Count + 1;
    public int ReadyCount => _readyKeys.Count + 1;
    public bool Completed { get; private set; }

    public void AddWorkKey(string keyId) => _workKeys.Add(keyId);

    public void RemoveWorkKey(string keyId)
    {
        _workKeys.Remove(keyId);
        _readyKeys.Remove(keyId);
    }

    public bool IsReady(string keyId) => _readyKeys.Contains(keyId);

    public void SetReady(string keyId, bool ready)
    {
        if (Completed) return;
        if (ready) _readyKeys.Add(keyId); else _readyKeys.Remove(keyId);
    }

    public void Complete() => Completed = true;

    public void Reset()
    {
        _readyKeys.Clear();
        Completed = false;
    }
}
