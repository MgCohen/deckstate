using DeckState.Samples.ReadyDeck;
using DeckState.Samples.SessionBoardDeck;

namespace DeckState.WasmEmulator.Emulator;

/// <summary>The projects the emulator can run. Each wires a real plugin loop to emulator seams.</summary>
public static class EmulatorProjects
{
    public static readonly EmulatorProject ReadyDeck = new()
    {
        Name = "ReadyDeck",
        Actions =
        [
            new(ReadyDeckPlugin.WorkAction, "Work", "Toggle a key between idle and ready."),
            new(ReadyDeckPlugin.CounterAction, "Counter", "Shows ready progress; press to reset the deck."),
        ],
        // ReadyDeck is fully self-contained — no host seams to inject.
        CreateRun = (_, _) => ReadyDeckPlugin.RunAsync,
        DefaultLayout = device =>
        {
            var map = new Dictionary<int, string>();
            for (var slot = 0; slot < device.KeyCount; slot++)
                map[slot] = ReadyDeckPlugin.WorkAction;
            map[device.KeyCount - 1] = ReadyDeckPlugin.CounterAction; // last key drives the counter/reset
            return map;
        },
    };

    public static readonly EmulatorProject SessionBoard = new()
    {
        Name = "SessionBoard",
        Actions =
        [
            new(SessionBoardPlugin.SessionAction, "Session", "Shows a Battlebox session; press to open it."),
        ],
        // Swap the two SessionBoard host seams for their in-process equivalents.
        CreateRun = (js, columns) => (connection, cancellationToken) =>
            SessionBoardPlugin.RunAsync(connection, new MockSessionBoardSource(), new JsSessionOpener(js), columns, cancellationToken),
        DefaultLayout = device =>
        {
            var map = new Dictionary<int, string>();
            var count = Math.Min(MockSessionBoardSource.SampleCount, device.KeyCount);
            for (var slot = 0; slot < count; slot++)
                map[slot] = SessionBoardPlugin.SessionAction;
            return map;
        },
    };

    public static readonly IReadOnlyList<EmulatorProject> All = [ReadyDeck, SessionBoard];
}
