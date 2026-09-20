using DeckState.Samples.SessionBoardDeck;

namespace DeckState.WasmEmulator.Emulator;

/// <summary>
/// The emulator's swap-in for the Battlebox HTTP client — the SessionBoard analogue of
/// <see cref="JsBridgeTransport"/>. It returns a fixed, realistic set of sample sessions so the
/// real board runtime, key state machine and SVG rendering run unchanged inside the browser.
/// </summary>
public sealed class MockSessionBoardSource : ISessionBoardSource
{
    public static readonly IReadOnlyList<BoardSession> Sessions =
    [
        new("api-refactor", "Claude", "Refactor auth module", "working", false, "Extracting the token service.", "https://example.com/sessions/api-refactor"),
        new("flaky-test", "Cursor", "Fix flaky payment test", "review", false, "Waiting on a second review.", "https://example.com/sessions/flaky-test"),
        new("db-migration", "Claude", "Migrate orders table", "review", true, "Needs your approval to run.", "https://example.com/sessions/db-migration"),
        new("docs-pass", "Codex", "Update API docs", "done", false, "Merged to main.", "https://example.com/sessions/docs-pass"),
        new("outage", "Claude", "Investigate 500s", "blocked", false, "Blocked on infra access.", "https://example.com/sessions/outage"),
        new("telemetry", "Cursor", "Add request telemetry", "working", false, "Wiring the exporter.", "https://example.com/sessions/telemetry"),
    ];

    public static int SampleCount => Sessions.Count;

    public Task<SessionBoardSnapshot> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SessionBoardSnapshot(DateTimeOffset.UtcNow, Sessions));
}
