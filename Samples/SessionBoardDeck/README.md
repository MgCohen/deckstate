# DeckState Session Board sample

A native Stream Deck plugin that renders the Battlebox `session-board` snapshot. Add **Session Slot** to each key in a 4×3 grid. The physical position selects the corresponding normalized session, ordered by `needsYou`, bucket, and update time.

It polls `http://127.0.0.1:8787/api/sessions` every five seconds. Set `DECKSTATE_BATTLEBOX_URL` when Battlebox is hosted elsewhere. A key opens the session's `openUrl` on press.

This deliberately does not collect Claude data itself: Battlebox and `session-board` remain the collection and normalization authority. Quota/reset is intentionally a later, separate source because it is not in the current board contract.

```bash
bash scripts/package-streamdeck-windows.sh
```

Install the resulting `artifacts/com.snowprint.deckstate.session-board.sdPlugin` directory on the Windows machine that runs Stream Deck, then place **Session Slot** on keys in a 4×3 grid.
