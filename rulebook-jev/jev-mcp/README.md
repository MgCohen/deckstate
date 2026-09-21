# jev-mcp

A tiny MCP server that exposes TypeSafe's **jev** grader as one tool, `grade_rubrics`.
The Rulebook artifact calls it to grade rubrics (pass/fail + probability); failed
rubrics then go to Claude for written feedback.

- **Transport:** stdio, newline-delimited JSON-RPC 2.0. No dependencies (`node` only).
- **Tool:** `grade_rubrics({ rulesContext?, rubrics[], content[], threshold? })`
  → `{ contentResults: [{ contentId, rubricResults: [{ rubricId, passed, probability }] }] }`
- **Secret:** the jev API key lives here (server-side), never in the artifact or the browser.

## Local use (phase 1 — owner, Claude desktop app)

Host MCP servers (`host:jev` in the artifact) run on **your device**, through the
**Claude desktop app**, and — for now — only for the **artifact's owner**. So test the
whole loop in the desktop app.

Add to your Claude desktop config
(`~/Library/Application Support/Claude/claude_desktop_config.json` on macOS,
`%APPDATA%\\Claude\\claude_desktop_config.json` on Windows):

```json
{
  "mcpServers": {
    "jev": {
      "command": "node",
      "args": ["/ABSOLUTE/PATH/TO/jev-mcp/server.mjs"],
      "env": { "TYPESAFE_API_KEY": "apikey_…" }
    }
  }
}
```

The config key (`jev`) is the server name → the artifact addresses it as **`host:jev`**
(non-`[A-Za-z0-9_-]` chars become `_`). Restart the desktop app, open the artifact
there, and grading routes to jev.

Quick smoke test without the app:

```bash
TYPESAFE_API_KEY=apikey_… node server.mjs < test-handshake.jsonl   # protocol only
```

## Deploy (phase 2 — everyone, Cloudflare / tunnel)

The grading core (`gradeRubrics`) is transport-agnostic. To make it work for every
viewer, wrap the same core in a **remote MCP server over streamable HTTP** and add it
as an **org connector** in claude.ai (Settings → Connectors). Two easy paths:

- **Cloudflare Workers** — deploy the HTTP MCP handler; the key lives in a Worker secret.
- **Tunnel** — expose this local server over HTTPS (e.g. `cloudflared tunnel`) for a
  quick shared test before a real deploy.

Once it's a connector with a display name (say **"Jev"**), the artifact manifest swaps
`host:jev` for that display name and every viewer who has connected it can grade — no
per-viewer key, the Worker holds it.

## Notes

- `grade_rubrics` is annotated `readOnlyHint: true`, so the app won't prompt per call and
  results can be cached briefly.
- 429/529 from jev are retried with exponential backoff (2s, 4s, 8s, 16s).
- One jev call is made per content item (all rubrics batched as `noul` questions).
