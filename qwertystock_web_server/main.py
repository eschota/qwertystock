"""Minimal HTTP entrypoint for the QwertyStock local web UI (bootstrapper default: port 7332)."""

from __future__ import annotations

import os

import uvicorn
from fastapi import FastAPI
from fastapi.responses import HTMLResponse

DEFAULT_PORT = 7332

app = FastAPI(title="QwertyStock Web", version="0.1.0")


@app.get("/", response_class=HTMLResponse)
async def root() -> str:
    return """<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>QwertyStock</title>
  <style>
    body { font-family: system-ui, sans-serif; margin: 2rem; }
    code { background: #f4f4f4; padding: 0.2em 0.4em; border-radius: 4px; }
  </style>
</head>
<body>
  <h1>QwertyStock</h1>
  <p>Local server is running.</p>
</body>
</html>"""


def main() -> None:
    port = int(os.environ.get("PORT", str(DEFAULT_PORT)))
    host = os.environ.get("HOST", "127.0.0.1")
    uvicorn.run(app, host=host, port=port)


if __name__ == "__main__":
    main()
