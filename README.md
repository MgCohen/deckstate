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

## Emulator (playground)

`playground/DeckState.WasmEmulator` is a browser emulator: the real sample plugin loops compiled to WebAssembly, driven over an in-process transport instead of the Elgato WebSocket. Same `willAppear`/`keyDown`/`setImage` frames, same state machine and SVG rendering — only the wire changes.

```bash
dotnet run --project playground/DeckState.WasmEmulator
```

Pick a **project** (ReadyDeck / SessionBoard) and a **device** (Mini / Standard / XL) from the toolbar. **Configure** turns on the playground: click a key to cycle it through that project's actions and back to empty. SessionBoard runs against an in-process mock source (no live Battlebox needed).

### Publishing it as a shareable Artifact

The emulator can be published as a static Claude Artifact (an in-browser, shareable page). This is on demand, not automated — the `publish-emulator` skill (`.claude/skills/publish-emulator`) documents the flow, and the packaging is done by:

```bash
bash playground/DeckState.WasmEmulator/scripts/publish-emulator.sh
```

That builds Release, stages the static files (dropping compressed duplicates, rebasing the reserved `_framework/` path), and writes a `files.json` the Artifact tool consumes.

### Browser E2E tests

`playground/DeckState.WasmEmulator.E2E` is a Playwright (.NET) smoke suite that boots the emulator's real dev server, drives it in a headless browser, and asserts on the SVG the plugin loops render back (ReadyDeck toggle + counter reset, device reshaping, SessionBoard rendering its mock sessions).

```bash
dotnet test playground/DeckState.WasmEmulator.E2E
```

The fixture launches the pre-installed Chromium via `ExecutablePath` (no `playwright install` needed) and records a `.webm` video and a Playwright trace per test into `artifacts/e2e/` (gitignored). Open a trace with `dotnet tool run playwright show-trace artifacts/e2e/traces/<name>.zip`.
