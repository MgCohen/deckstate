using DeckState.Core;

namespace DeckState.Samples.SessionBoardDeck;

public sealed class SessionBoardRuntime(SessionBoardContext context) : DeckRuntime<SessionBoardContext>(context)
{
    public async Task RefreshAsync(ISessionBoardSource source, CancellationToken cancellationToken)
    {
        Context.SetSnapshot(await source.GetAsync(cancellationToken));
        await TickAsync(cancellationToken);
    }
}
