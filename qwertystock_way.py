#!/usr/bin/env python3
"""
Единая входная точка: HTTP-сервер (health + GitHub webhook).
"""
from __future__ import annotations

import logging
import os
import socketserver
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT = os.path.dirname(os.path.abspath(__file__))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)

from module.api_git_webhook import handle_git_webhook  # noqa: E402

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
log = logging.getLogger("qwertystock_way")

PORT = int(os.environ.get("QWERTYSTOCK_PORT", "8765"))
BIND = os.environ.get("QWERTYSTOCK_BIND", "0.0.0.0")


class AppHandler(BaseHTTPRequestHandler):
    def log_message(self, fmt: str, *args) -> None:
        log.info("%s - %s", self.address_string(), fmt % args)

    def _send(self, code: int, body: bytes, content_type: str = "text/plain; charset=utf-8") -> None:
        self.send_response(code)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:
        path = self.path.split("?", 1)[0]
        if path in ("/health", "/health/", "/api/git/health", "/api/git/health/"):
            self._send(200, b"ok\n")
            return
        self._send(404, b"not found\n")

    def do_POST(self) -> None:
        path = self.path.split("?", 1)[0]
        if path != "/api/git/webhook" and path != "/api/git/webhook/":
            self._send(404, b"not found\n")
            return
        length = int(self.headers.get("Content-Length", "0") or 0)
        body = self.rfile.read(length) if length else b""
        hdrs = {k: v for k, v in self.headers.items()}
        code, out = handle_git_webhook(body, hdrs)
        ctype = "application/json" if out.startswith(b"{") else "text/plain; charset=utf-8"
        self._send(code, out, ctype)


def main() -> None:
    server = ThreadingHTTPServer((BIND, PORT), AppHandler)
    log.info("qwertystock_way listening on %s:%s", BIND, PORT)
    server.serve_forever()


if __name__ == "__main__":
    main()
