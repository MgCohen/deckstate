using DeckState.Samples.SessionBoardDeck;
using DeckState.StreamDeck;

await StreamDeckPluginHost.RunAsync(
    StreamDeckPluginArguments.Parse(args),
    SessionBoardPlugin.RunAsync,
    CancellationToken.None);
