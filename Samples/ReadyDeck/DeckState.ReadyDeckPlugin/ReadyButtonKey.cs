using DeckState.Core;
using DeckState.StreamDeck;

namespace DeckState.Samples.ReadyDeck;

public static class ReadyButtonKey
{
    public static Key<ReadyDeckContext> Create(string keyId, ReadyDeckContext context, IKeySurface surface) =>
        new(keyId, context, surface, command => command.Name == "press" ? new KeyEvent("press") : null,
            new Idle(keyId), new Ready(keyId), new Done());

    private sealed class Idle(string keyId) : KeyState<ReadyDeckContext>
    {
        public override bool IsValid(ReadyDeckContext context) => !context.Completed && !context.IsReady(keyId);

        public override ValueTask<StateReaction> OnEventAsync(KeyEvent @event, ReadyDeckContext context, CancellationToken cancellationToken)
        {
            context.SetReady(keyId, true);
            return ValueTask.FromResult(StateReaction.None);
        }

        public override KeyVisual Draw(ReadyDeckContext context) => Visual("IDLE", "#34495e", "○");
    }

    private sealed class Ready(string keyId) : KeyState<ReadyDeckContext>
    {
        public override bool IsValid(ReadyDeckContext context) => !context.Completed && context.IsReady(keyId);

        public override ValueTask<StateReaction> OnEventAsync(KeyEvent @event, ReadyDeckContext context, CancellationToken cancellationToken)
        {
            context.SetReady(keyId, false);
            return ValueTask.FromResult(StateReaction.None);
        }

        public override KeyVisual Draw(ReadyDeckContext context) => Visual("READY", "#146c43", "✓");
    }

    private sealed class Done : KeyState<ReadyDeckContext>
    {
        public override bool IsValid(ReadyDeckContext context) => context.Completed;
        public override KeyVisual Draw(ReadyDeckContext context) => Visual("DONE", "#4c3c79", "✓");
    }

    private static KeyVisual Visual(string label, string color, string mark) => new(null,
        KeySvg.Draw(key => key.Fill(color).Circle(72, 48, 24, "#22FFFFFF").Text(mark, 72, 58, 30).Text(label, 72, 100, 18)));
}
