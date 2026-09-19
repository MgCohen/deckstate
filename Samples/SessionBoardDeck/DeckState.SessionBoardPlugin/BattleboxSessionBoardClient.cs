using System.Net.Http.Json;
using System.Text.Json;

namespace DeckState.Samples.SessionBoardDeck;

public sealed class BattleboxSessionBoardClient(HttpClient http, Uri endpoint)
{
    public async Task<SessionBoardSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(endpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new SessionBoardSnapshot(DateTimeOffset.UtcNow, [], $"Battlebox: {(int)response.StatusCode}");

        var envelope = await response.Content.ReadFromJsonAsync<BattleboxEnvelope>(cancellationToken: cancellationToken);
        if (envelope is null || !envelope.Enabled)
            return new SessionBoardSnapshot(DateTimeOffset.UtcNow, [], "Battlebox sessions are disabled.");

        if (envelope.Board is null)
            return new SessionBoardSnapshot(DateTimeOffset.UtcNow, [], envelope.Error ?? "Waiting for Battlebox.");

        return new SessionBoardSnapshot(
            envelope.Board.GeneratedAt,
            envelope.Board.Sessions.Select(session => new BoardSession(
                session.Key,
                session.Provider,
                session.Title,
                session.Bucket,
                session.NeedsYou,
                session.Summary,
                session.OpenUrl)).ToArray(),
            envelope.Ok ? null : envelope.Error);
    }

    private sealed record BattleboxEnvelope(bool Enabled, bool Ok, string? Error, BoardDocument? Board);
    private sealed record BoardDocument(DateTimeOffset GeneratedAt, List<SessionDocument> Sessions);
    private sealed record SessionDocument(string Key, string Provider, string Title, string Bucket, bool NeedsYou, string Summary, string? OpenUrl);
}
