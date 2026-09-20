#!/usr/bin/env python3
"""docker-hello — a tiny hello-world backend for exercising Docker in a
Claude Code web/cloud session. Standard library only, so the image builds
without any network access.

Endpoints:
  GET /          -> greeting + container hostname + timestamp
  GET /health    -> {"status": "ok"}   (used by the Docker HEALTHCHECK)
  GET /outbound  -> calls an external HTTPS site, so you can see the
                    difference between running with no / limited / full internet
"""
import json
import os
import socket
import urllib.request
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PORT = int(os.environ.get("PORT", "8080"))
OUTBOUND_URL = os.environ.get("OUTBOUND_URL", "https://example.com")


def check_outbound():
    """Try an outbound HTTPS request. Succeeds only when the container has
    full internet (e.g. launched with `drun`); fails on cert error with
    plain `docker run`, and on a connection error with `--network none`."""
    try:
        with urllib.request.urlopen(OUTBOUND_URL, timeout=6) as resp:
            return {"reachable": True, "url": OUTBOUND_URL, "status": resp.status}
    except Exception as exc:  # noqa: BLE001 - report any failure verbatim
        return {"reachable": False, "url": OUTBOUND_URL,
                "error": f"{type(exc).__name__}: {exc}"}


class Handler(BaseHTTPRequestHandler):
    def _send(self, code, body):
        payload = json.dumps(body, indent=2).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def do_GET(self):
        path = self.path.split("?", 1)[0]
        if path == "/health":
            self._send(200, {"status": "ok"})
        elif path == "/outbound":
            self._send(200, {"outbound": check_outbound()})
        elif path == "/":
            self._send(200, {
                "service": "docker-hello",
                "message": "Hello from inside Docker 🐳",
                "hostname": socket.gethostname(),
                "time": datetime.now(timezone.utc).isoformat(),
                "endpoints": ["/", "/health", "/outbound"],
            })
        else:
            self._send(404, {"error": "not found", "path": path})

    def log_message(self, fmt, *args):  # keep logs tidy and prefixed
        print("[docker-hello]", fmt % args)


if __name__ == "__main__":
    print(f"[docker-hello] listening on :{PORT}")
    ThreadingHTTPServer(("0.0.0.0", PORT), Handler).serve_forever()
