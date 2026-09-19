# DeckState

`src/DeckState.Core` contains the generic state-machine runtime.

`src/DeckState.StreamDeck` contains the Elgato WebSocket transport and SVG drawing primitives. It has no sample-specific actions or state.

Each runnable example lives under `Samples/<name>`. `Samples/ReadyDeck` is the native Stream Deck sample and its protocol harness.

## ReadyDeck

```bash
dotnet run --project Samples/ReadyDeck/DeckState.ReadyDeck.ProtocolHarness
bash Samples/ReadyDeck/scripts/package-streamdeck-windows.sh
```

The protocol harness runs the actual C# plugin against a loopback Stream Deck protocol peer. The package script creates the `.sdPlugin` directory ready to copy to a Windows Stream Deck installation.
