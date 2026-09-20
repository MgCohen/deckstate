namespace DeckState.Samples.SessionBoardDeck;

/// <summary>
/// Where the board's sessions come from. This is the SessionBoard equivalent of
/// <c>IStreamDeckTransport</c>: a single swappable seam. Hardware polls the real Battlebox HTTP
/// endpoint (<see cref="BattleboxSessionBoardClient"/>); the emulator supplies an in-process mock.
/// Everything above this line — the runtime, the key state machine, the SVG rendering — is
/// identical on every path.
/// </summary>
public interface ISessionBoardSource
{
    Task<SessionBoardSnapshot> GetAsync(CancellationToken cancellationToken);
}
