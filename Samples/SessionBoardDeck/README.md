# DeckState Session Board sample

A native Stream Deck plugin that renders the Battlebox `session-board` snapshot. Add **Session Slot** to each key in a 4×3 grid. The physical position selects the corresponding normalized session, ordered by `needsYou`, bucket, and update time.

It polls `http://127.0.0.1:8787/api/sessions` every five seconds. Set `DECKSTATE_BATTLEBOX_URL` when Battlebox is hosted elsewhere. A key opens the session's `openUrl` on press.

When Battlebox is behind Cloudflare Access, authenticate with a Cloudflare Access [service token](https://developers.cloudflare.com/cloudflare-one/access-controls/service-credentials/service-tokens/). Create the token and a matching Service Auth policy on the Access application, then set `DECKSTATE_BATTLEBOX_CF_CLIENT_ID` and `DECKSTATE_BATTLEBOX_CF_CLIENT_SECRET`. When both are present the plugin sends them as `CF-Access-Client-Id` / `CF-Access-Client-Secret` on every request.

This deliberately does not collect Claude data itself: Battlebox and `session-board` remain the collection and normalization authority. Quota/reset is intentionally a later, separate source because it is not in the current board contract.

```bash
bash scripts/package-streamdeck-windows.sh
```

Install the resulting `artifacts/com.snowprint.deckstate.session-board.sdPlugin` directory on the Windows machine that runs Stream Deck, then place **Session Slot** on keys in a 4×3 grid.

## Host seams

The plugin loop (`SessionBoardPlugin.RunAsync`) takes two injected seams so the same runtime, key state machine, and rendering run unchanged on any host — the same idea as `IStreamDeckTransport`:

- `ISessionBoardSource` — where sessions come from. Hardware uses `BattleboxSessionBoardClient` (the HTTP poll above); the WASM emulator swaps in an in-process mock. The hardware entrypoint that wires the real source lives in `SessionBoardPlugin.Hardware.cs`.
- `ISessionOpener` — how a key press opens a session URL. Hardware uses `ProcessSessionOpener` (the OS browser); the emulator opens a new tab.

See `playground/DeckState.WasmEmulator` for the emulator wiring.
