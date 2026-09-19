using DeckState.Core;

namespace DeckState.Samples.SessionBoardDeck;

public sealed class SessionBoardContext : IDeckContext
{
    private SessionBoardSnapshot _snapshot = SessionBoardSnapshot.Loading;

    public SessionBoardSnapshot Snapshot => Volatile.Read(ref _snapshot);
    public long Revision { get; private set; }

    public void SetSnapshot(SessionBoardSnapshot snapshot)
    {
        Volatile.Write(ref _snapshot, snapshot);
        Revision++;
    }

    public BoardSession? SessionAt(int slot) => Snapshot.Sessions.ElementAtOrDefault(slot);
}
