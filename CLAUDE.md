# deckstate

## Testing / deploying to the Stream Deck

Redeploy a plugin to the local Stream Deck — headless, no UAC (stops the plugin, clean-packages, reloads, verifies):

```powershell
powershell -File $env:USERPROFILE\.claude\bin\sd-redeploy.ps1 -Uuid <uuid> -PackageScript <repo>\Samples\<name>\scripts\package-streamdeck-windows.sh
```

- ReadyDeck: `-Uuid com.snowprint.deckstate` · `Samples\ReadyDeck\scripts\package-streamdeck-windows.sh`
- Session Board: `-Uuid com.snowprint.deckstate.session-board` · `Samples\SessionBoardDeck\scripts\package-streamdeck-windows.sh`

Never hot-swap a live `.sdPlugin` dir — always a full package.

### No-hardware self-test (run these first)

Protocol harnesses replay the exact host frame sequence over a loopback WebSocket — `deviceDidConnect` (an action-less frame) first, then `willAppear` / `keyDown` — and assert the plugin renders and acts. No device, no browser. They catch message-loop regressions (e.g. a `keyDown` that crashes on a frame without an `action` field) that the emulator and hardware can hide. Also run in CI.

```powershell
dotnet run --project Samples/ReadyDeck/DeckState.ReadyDeck.ProtocolHarness
dotnet run --project Samples/SessionBoardDeck/DeckState.SessionBoardPlugin.ProtocolHarness
```

Exit 0 = pass. Adding a new key event or protocol frame? Extend the matching harness so it is covered before touching hardware. Runtime diagnostics land in `%LOCALAPPDATA%\DeckState\logs` (every inbound/outbound frame, opener calls, errors); read that log first when a click misbehaves on-device.
