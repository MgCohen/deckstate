using DeckState.Core;
using DeckState.StreamDeck;

namespace DeckState.Samples.SessionBoardDeck;

public static class SessionBoardKey
{
    public static Key<SessionBoardContext> Create(string keyId, int slot, SessionBoardContext context, IKeySurface surface, ISessionOpener opener) =>
        new(keyId, context, surface, command => command.Name == "press" ? new KeyEvent("press") : null, new Display(slot, opener));

    private sealed class Display(int slot, ISessionOpener opener) : KeyState<SessionBoardContext>
    {
        private long _revision = -1;

        public override bool IsValid(SessionBoardContext context) => true;

        public override ValueTask<StateReaction> OnTickAsync(SessionBoardContext context, CancellationToken cancellationToken)
        {
            if (_revision == context.Revision) return ValueTask.FromResult(StateReaction.None);
            _revision = context.Revision;
            return ValueTask.FromResult(StateReaction.Render);
        }

        public override async ValueTask<StateReaction> OnEventAsync(KeyEvent @event, SessionBoardContext context, CancellationToken cancellationToken)
        {
            var url = context.SessionAt(slot)?.OpenUrl;
            if (!string.IsNullOrWhiteSpace(url)) await opener.OpenAsync(url, cancellationToken);
            return StateReaction.None;
        }

        public override KeyVisual Draw(SessionBoardContext context)
        {
            var session = context.SessionAt(slot);
            if (session is null)
            {
                var message = context.Snapshot.Error ?? "No session";
                return new KeyVisual(null, KeySvg.Draw(key => key.Fill("#20252e").Text("SESSION", 72, 58, 15).Text(Short(message, 18), 72, 90, 12, "#b7c0ce")));
            }

            var (color, mark, label) = session.NeedsYou
                ? ("#a93226", "!", "NEEDS YOU")
                : session.Bucket.ToLowerInvariant() switch
                {
                    "working" => ("#176b87", "●", "WORKING"),
                    "review" => ("#7d6608", "✓", "REVIEW"),
                    "done" => ("#455a64", "✓", "DONE"),
                    _ => ("#5b2c6f", "!", "BLOCKED"),
                };

            return new KeyVisual(null, KeySvg.Draw(key => key.Fill(color)
                .Circle(72, 30, 18, "#22FFFFFF")
                .Text(mark, 72, 38, 24)
                .Text(Short(session.Title, 15), 72, 75, 14)
                .Text(label, 72, 106, 12)
                .Text(Short(session.Provider, 12), 72, 128, 10, "#dfe6e9")));
        }

        private static string Short(string value, int max) => value.Length <= max ? value : $"{value[..(max - 1)]}…";
    }
}
