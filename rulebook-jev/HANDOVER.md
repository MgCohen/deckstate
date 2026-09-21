# Rulebook Playground + Jev grading — handover

You are picking up a working prototype and taking it to completion in a **new,
standalone repo**. This document is your brief: what the thing is, where the code
is, and the one job left to make it work for everyone.

---

## 1. What it is

**Rulebook Playground** is a single-file interactive web app (a **Claude Artifact**)
for reviewing short pieces of content against a rubric. Concretely, the sample domain
is game abilities:

- **Rules** — the game's *design constraints* (e.g. "every ability must define a
  target"; "one effect per ability — don't combine damage and healing"). Rules are
  recursive (sub-rules, sub-sub-rules), foldable, numbered `1 / 1.1 / 1.1.1`, edited
  in place, managed via a `⋯` menu. No tags (removed on purpose).
- **Content** — the things being graded (abilities like *Fireball*, *Heal*,
  *Vampiric Strike*).
- **Reviewer** — a "How to grade" instruction + a list of **rubrics**, which are
  *generic* true/false review lenses (Understandable, Complete, Consistent wording,
  Follows the rules).
- **Review** (its own tab) — runs the grading loop and shows a ring score, per-content
  breakdown, per-rubric ✓/✗ with probability, and written feedback on failures. History
  of the last 30 runs is kept in `localStorage`.

### The grading loop (the whole point)
1. **Jev grades.** Every rubric, for every content item, is sent to **TypeSafe's "jev"**
   classifier as a `noul` (yes/no) question. Jev returns a **probability 0–1** per
   rubric — pass/fail, **no text**. This is the grader.
2. **Claude explains failures.** Only the rubrics jev marks as **failed** are sent to
   Claude for the written feedback ("why it fails + smallest fix").
3. **Scores are deterministic** — `passed / total × 100` per item, mean overall. Claude
   never decides pass/fail; jev never writes prose.

**Design decision already made:** jev is *required*. If jev isn't reachable, Review is
disabled with a "connect the Jev grader" banner (no silent fallback to Claude grading).

---

## 2. Where things are (files in this bundle)

```
rulebook-playground.html     The artifact. Single file: <style> + markup + JS.
                             Already rewired to the jev→Claude loop. Syntax-clean.
                             NOT yet published against a real connector (see §4).

jev-mcp/
  server.mjs                 MCP server exposing ONE tool: grade_rubrics.
                             Dependency-free, stdio transport. Holds the jev API key.
                             Validated: MCP handshake works.
  README.md                  Local setup + deploy notes.

rulebook-grader/             Standalone Node pipeline (no browser) — same grading core.
  lib/jev.mjs                Calls jev systemone; noul-per-rubric; retry/backoff.
  lib/analyze.mjs            Claude "explain failure" via the Anthropic Messages API.
  lib/pipeline.mjs           The full loop: grade → analyze fails → deterministic scores.
  run.mjs                    CLI (`node run.mjs [payload.json] [--grade-only] [--json]`).
  sample.json               The sample rules/rubrics/content.
  test-grade.mjs             Quick jev-only grading check.
```

**Proven working:** running `rulebook-grader/test-grade.mjs` against jev graded the 3
sample abilities correctly — *Vampiric Strike* failed "Follows the rules" at **p=0.04**
(it combines damage + healing, breaking the one-effect rule); everything else passed
0.72–0.95. So the jev integration and the rubric→noul mapping are correct.

**The original private artifact** (previous account, Claude-grading version, private):
`https://claude.ai/artifact/SRC5kyL7APj1ErJ2M7eifj` — you can't open it from the new
account. Publish a fresh artifact from `rulebook-playground.html` instead.

---

## 3. How to get things

### Jev / TypeSafe
- **Skill:** install TypeSafe's skill — `npx skills add typesafe-ai/skills --skill
  typesafe-ai` (or Claude Code: `claude plugin marketplace add typesafe-ai/skills`
  then `claude plugin install typesafe@typesafe-ai`). Use one method.
- **Docs (source of truth):** https://docs.typesafe.ai/ — `api.md`,
  `sdk/javascript.md`, `sdk/python.md`.
- **API contract (already distilled):**
  - `POST https://api.typesafe.ai/v1/systemone`
  - Headers: `Authorization: Bearer <KEY>`, `Content-Type: application/json`
  - Body: `{ "state": <object>, "model": "jev-latest", "questions": { "<id>": { "type":
    "noul", "instructions": "<yes/no question>", "criteria": { "true": "...", "false":
    "..." } } } }`
  - Response: `answers["<id>"].noul` is the probability 0–1. Also `choice`/`score`
    question types exist. 401/422/429/529 errors; back off on 429/529.
- **API key:** get the current `TYPESAFE_API_KEY` from the project owner — it's shared
  out-of-band and deliberately **not committed to git**. Keep it in an env var / deploy
  secret, never in the artifact or client. Prefer generating a fresh key on the new
  account. (A temporary key was used during the prototype; assume it needs replacing.)

### Claude (the feedback half)
- **In the artifact:** the browser page uses the native `sample` capability
  (`claude.use("sample")`) — runs on the viewer's own Claude plan, no key.
- **In the CLI:** `rulebook-grader/lib/analyze.mjs` uses the Anthropic Messages API —
  set `ANTHROPIC_API_KEY` (and optionally `ANTHROPIC_MODEL`).

---

## 4. What we need to do to make it work (the one real task)

**The blocker:** a published Claude Artifact runs in a locked sandbox — it can `fetch`
**nothing** except two channels: Claude (`sample`) and the viewer's **MCP connectors**
(`mcp`). So the artifact can't call `api.typesafe.ai` directly; jev has to be reached
through an MCP **connector**. A *local* (`host:`) MCP server can't be baked into the
manifest from a remote publishing session (that's where the last attempt failed). So:

**Do the remote-connector path.** Steps:

1. **Build the HTTP MCP server.** Wrap the existing `grade_rubrics` core (see
   `jev-mcp/server.mjs` and `rulebook-grader/lib/jev.mjs`) in an MCP **streamable-HTTP**
   server (the transport claude.ai remote connectors speak). The jev key lives
   server-side. Tool stays: `grade_rubrics({ rulesContext?, rubrics[], content[],
   threshold? }) -> { contentResults: [{ contentId, rubricResults: [{ rubricId, passed,
   probability }] }] }`, annotated `readOnlyHint: true`.
2. **Deploy / expose it over HTTPS.** Fastest: run it and `cloudflared tunnel` for a
   public URL. Durable: **Cloudflare Workers** (jev key as a Worker secret).
3. **Add it as a connector** in **claude.ai → Settings → Connectors**, display name
   **"Jev"** (org-level so everyone gets it).
4. **Point the artifact at it and publish.** In `rulebook-playground.html`, change the
   constant `JEV_SERVER` from `'host:jev'` to `'Jev'` (leave `JEV_TOOL =
   'grade_rubrics'`). Publish with:
   `capabilities: { "sample": {}, "mcp": { "servers": [{ "server": "Jev", "tools":
   ["grade_rubrics"] }] } }`.
   The **publishing account must have the "Jev" connector connected** so the manifest
   validates. (Declaring `mcp` makes the artifact org-internal, not publicly shareable
   — fine.)
5. **Verify.** Open the artifact as a viewer who has connected Jev → Review → jev grades
   (watch the p= values), and *Vampiric Strike / Follows the rules* should fail and get a
   Claude-written explanation.

The artifact code is already written for exactly this — `jevGrade()` does
`mcp.callTool(JEV_SERVER, JEV_TOOL, {rulesContext, rubrics, content, threshold})`, reads
`result.payload.contentResults`, collects failures, and calls `claudeFeedback()`. Only
the server name + the hosting are missing.

---

## 5. Move to a standalone repo

This currently lives in a scratch area of an unrelated repo. Give it its own home, e.g.
`rulebook-jev`:

```
rulebook-jev/
  artifact/rulebook-playground.html      # the published artifact source
  mcp/                                    # the HTTP MCP server (to build) + server.mjs (stdio, local/CI)
  grader/                                 # rulebook-grader/* (CLI + libs)
  package.json                            # "type":"module"
  .env.example                            # TYPESAFE_API_KEY, ANTHROPIC_API_KEY
  wrangler.toml                           # if Cloudflare Workers
  README.md                               # setup + deploy + connector steps
```

Notes for the new repo:
- All Node code is ESM, zero-dependency today (Node 18+ global `fetch`). The HTTP MCP
  server may add `@modelcontextprotocol/sdk` if you prefer it over hand-rolling the
  transport.
- The artifact must stay a **single self-contained HTML file** with everything inline
  (Artifact CSP: no external CSS/JS/images/fetch; Google Fonts `<link>` is the only
  external exception). No `<html>/<head>/<body>` wrapper. Must work at ~400px width.
- Keep the localStorage keys stable (`rulebook-playground-data-v3`,
  `rulebook-playground-history-v4`) unless you intend to reset users' saved data.

### First message to paste into the new session
> Set up a new repo `rulebook-jev` from the attached bundle (artifact HTML + jev-mcp +
> rulebook-grader + this handover). The goal is the jev→Claude review loop working in the
> published artifact for everyone via a "Jev" MCP connector. Start by building the
> streamable-HTTP MCP server that wraps `grade_rubrics` (jev key server-side), get it
> deployable on Cloudflare Workers with a `cloudflared tunnel` option for quick testing,
> then walk me through adding it as a claude.ai connector named "Jev" and I'll publish the
> artifact with the mcp manifest pointing at it. Read HANDOVER.md fully first.
