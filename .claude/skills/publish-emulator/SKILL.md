---
name: publish-emulator
description: >
  Publish or update the DeckState WASM emulator as a Claude Artifact (the shareable,
  in-browser Stream Deck emulator). Use when the user asks to publish, update, re-publish,
  or "push" the emulator/simulator artifact, or after changing the emulator, a sample
  plugin (ReadyDeck / SessionBoard), or the Core/StreamDeck stack and wanting the live
  artifact refreshed. On demand only — this is never automated.
---

# Publish the DeckState emulator artifact

The emulator (`playground/DeckState.WasmEmulator`) is a Blazor WebAssembly build of the
**real** plugin loops. An Artifact is a static web page, so publishing is: build → stage the
static files → hand them to the Artifact tool. A script does the deterministic part; you do
the upload.

## Canonical artifact

Update this same artifact in place so the link is stable:

    https://claude.ai/artifact/XV7tNoLmTCiHdYRSz9Cuis

If that URL is missing or you don't own it (a fresh clone, a different account), publish
without `url` to create a new one, then record the new URL here in this file.

## Steps

1. **Run the packaging script** (publishes Release, stages, rebases `_framework` → `framework`,
   writes `files.json`):

   ```bash
   bash playground/DeckState.WasmEmulator/scripts/publish-emulator.sh
   ```

   It prints the stage dir (`playground/DeckState.WasmEmulator/artifact-stage`), the page
   (`index.html`), and the file map (`files.json`).

2. **Read `files.json`** from the stage dir. Its contents are exactly the `files` argument
   for the Artifact tool (published paths → `{from, contentType}`, with `.wasm` typed as
   `application/wasm`).

3. **Publish with the Artifact tool** (`action: "publish"`):
   - `url` = the canonical artifact URL above (omit only when creating a new one).
   - `file_path` = `<stage>/index.html`
   - `root` = `<stage>`
   - `files` = the parsed contents of `files.json`
   - On a first-ever publish also set `title` ("DeckState Emulator") and `icon` ("grid").
     On updates, omit `icon` so it's preserved.

4. **Open it** for the user (`action: "open"`) and give them the link.

## Why the `_framework` → `framework` rename

The Artifact service reserves paths starting with `_`. Blazor ships its runtime under
`_framework/` but loads it via **relative** imports (`import("./dotnet.js")`), so only the
folder name and the single `<script>` tag in `index.html` need patching — the script handles
both. Do not change the source `wwwroot/index.html` (it must keep `_framework/` for local
`dotnet run`); the rename happens only in the staged copy.

## Notes

- The artifact is a **static snapshot** — it does not auto-update. Re-run these steps whenever
  the user wants the live emulator refreshed.
- Limits are comfortable: ~45 files, ~6.4 MB total (well under the 255-file / 64 MB caps).
- The artifact is private to its owner until shared via the page's Share menu.
