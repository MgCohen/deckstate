namespace DeckState.Core;

public readonly record struct StateReaction(bool RequestRender = false)
{
    public static StateReaction None => new();
    public static StateReaction Render => new(true);
}
