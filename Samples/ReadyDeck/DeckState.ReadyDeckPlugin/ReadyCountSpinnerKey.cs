using DeckState.Core;
using DeckState.StreamDeck;

namespace DeckState.Samples.ReadyDeck;

public static class ReadyCountSpinnerKey
{
    public static Key<ReadyDeckContext> Create(string keyId, ReadyDeckContext context, IKeySurface surface) =>
        new(keyId, context, surface, command => command.Name == "press" ? new KeyEvent("press") : null, new Counter());

    private sealed class Counter : KeyState<ReadyDeckContext>
    {
        private int _frame;

        public override bool IsValid(ReadyDeckContext context) => true;

        public override ValueTask<StateReaction> OnEventAsync(KeyEvent @event, ReadyDeckContext context, CancellationToken cancellationToken)
        {
            context.Reset();
            _frame = 0;
            return ValueTask.FromResult(StateReaction.Render);
        }

        public override ValueTask<StateReaction> OnTickAsync(ReadyDeckContext context, CancellationToken cancellationToken)
        {
            _frame = (_frame + 1) % 12;
            return ValueTask.FromResult(StateReaction.Render);
        }

        public override KeyVisual Draw(ReadyDeckContext context)
        {
            var label = context.Completed ? "ALL DONE" : $"READY {context.ReadyCount}/{context.TotalButtonCount}";
            return new(null, KeySvg.Draw(key => key
                .Fill("#284560")
                .Circle(72, 45, 31, "#16FFFFFF")
                .Triangle(72, 45, 15, "#FFFFFF", _frame * 30)
                .Text(label, 72, 94, 16)
                .Text("press to reset", 72, 118, 11)));
        }
    }
}
