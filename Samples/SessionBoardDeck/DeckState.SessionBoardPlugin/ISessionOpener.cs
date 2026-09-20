namespace DeckState.Samples.SessionBoardDeck;

/// <summary>
/// How a session's provider URL is opened when its key is pressed. The other host-specific seam
/// of SessionBoard: hardware launches the OS browser (<see cref="ProcessSessionOpener"/>); the
/// emulator opens a new browser tab via JS interop. The key state machine only knows this contract.
/// </summary>
public interface ISessionOpener
{
    ValueTask OpenAsync(string url, CancellationToken cancellationToken);
}
