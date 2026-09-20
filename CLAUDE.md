# deckstate

## Testing / deploying to the Stream Deck

Redeploy a plugin to the local Stream Deck — headless, no UAC (stops the plugin, clean-packages, reloads, verifies):

```powershell
powershell -File $env:USERPROFILE\.claude\bin\sd-redeploy.ps1 -Uuid <uuid> -PackageScript <repo>\Samples\<name>\scripts\package-streamdeck-windows.sh
```

- ReadyDeck: `-Uuid com.snowprint.deckstate` · `Samples\ReadyDeck\scripts\package-streamdeck-windows.sh`
- Session Board: `-Uuid com.snowprint.deckstate.session-board` · `Samples\SessionBoardDeck\scripts\package-streamdeck-windows.sh`

Never hot-swap a live `.sdPlugin` dir — always a full package. Pure build/logic check with no hardware: `dotnet run --project Samples/ReadyDeck/DeckState.ReadyDeck.ProtocolHarness`.
