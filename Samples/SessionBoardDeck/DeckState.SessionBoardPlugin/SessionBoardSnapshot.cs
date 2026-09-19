namespace DeckState.Samples.SessionBoardDeck;

public sealed record SessionBoardSnapshot(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<BoardSession> Sessions,
    string? Error = null)
{
    public static readonly SessionBoardSnapshot Loading = new(DateTimeOffset.MinValue, [], "Connecting to Battlebox…");
}

public sealed record BoardSession(
    string Key,
    string Provider,
    string Title,
    string Bucket,
    bool NeedsYou,
    string Summary,
    string? OpenUrl);
