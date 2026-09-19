using DeckState.Samples.ReadyDeck;
using DeckState.StreamDeck;

await StreamDeckPluginHost.RunAsync(
    StreamDeckPluginArguments.Parse(args),
    ReadyDeckPlugin.RunAsync,
    CancellationToken.None);
