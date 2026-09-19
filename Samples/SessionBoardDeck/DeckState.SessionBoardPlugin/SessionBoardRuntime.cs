using DeckState.Core;

namespace DeckState.Samples.SessionBoardDeck;

public sealed class SessionBoardRuntime(SessionBoardContext context) : DeckRuntime<SessionBoardContext>(context)
{
    public async Task RefreshAsync(BattleboxSessionBoardClient client, CancellationToken cancellationToken)
    {
        Context.SetSnapshot(await client.GetAsync(cancellationToken));
        await TickAsync(cancellationToken);
    }
}
