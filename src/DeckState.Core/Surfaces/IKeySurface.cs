namespace DeckState.Core;

public interface IKeySurface
{
    ValueTask DrawAsync(string keyId, KeyVisual visual, CancellationToken cancellationToken);
}
