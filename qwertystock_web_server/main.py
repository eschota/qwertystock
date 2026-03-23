"""HTTP entry for the QwertyStock local web UI (bootstrapper default: port 7332)."""

from __future__ import annotations

import json
import os
import sys
from pathlib import Path

import uvicorn
from fastapi import FastAPI
from fastapi.responses import HTMLResponse, PlainTextResponse
from pydantic import BaseModel, ConfigDict, Field

DEFAULT_PORT = 7332

_APP_DIR = Path(__file__).resolve().parent
_CABINET_HTML_PATH = _APP_DIR / "static" / "cabinet.html"

# Bump or set QS_CABINET_BUILD to verify deploys without changing code.
_CABINET_BUILD_DEFAULT = "cabinet-stub-2025-03-23"

if not _CABINET_HTML_PATH.is_file():
    raise FileNotFoundError(
        f"Cabinet UI not found: {_CABINET_HTML_PATH}. "
        "Ensure qwertystock_web_server/static/cabinet.html exists (git pull / full repo sync)."
    )

_MIN_POLL = 30
_MAX_POLL = 86400
_DEFAULT_POLL = 60


def _daemon_settings_path() -> Path:
    env = os.environ.get("QS_DAEMON_SETTINGS_PATH")
    if env:
        return Path(env)
    if sys.platform == "win32":
        la = os.environ.get("LOCALAPPDATA")
        if la:
            return Path(la) / "QwertyStock" / "daemon_settings.json"
    return Path.home() / ".qwertystock" / "daemon_settings.json"


def _load_daemon_settings_raw() -> dict:
    path = _daemon_settings_path()
    if not path.is_file():
        return {"repoPollIntervalSeconds": _DEFAULT_POLL}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {"repoPollIntervalSeconds": _DEFAULT_POLL}
    if not isinstance(data, dict):
        return {"repoPollIntervalSeconds": _DEFAULT_POLL}
    n = data.get("repoPollIntervalSeconds", _DEFAULT_POLL)
    try:
        v = int(n)
    except (TypeError, ValueError):
        v = _DEFAULT_POLL
    v = max(_MIN_POLL, min(_MAX_POLL, v))
    return {"repoPollIntervalSeconds": v}


def _write_daemon_settings(data: dict) -> None:
    path = _daemon_settings_path()
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


class DaemonSettingsPayload(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    repo_poll_interval_seconds: int = Field(
        ...,
        ge=_MIN_POLL,
        le=_MAX_POLL,
        alias="repoPollIntervalSeconds",
    )


def _load_cabinet_html() -> str:
    raw = _CABINET_HTML_PATH.read_text(encoding="utf-8")
    stamp = os.environ.get("QS_CABINET_BUILD", _CABINET_BUILD_DEFAULT)
    return raw.replace("{{BUILD_STAMP}}", stamp)


app = FastAPI(title="QwertyStock Web", version="0.2.0")


@app.get("/", response_class=HTMLResponse)
async def root() -> str:
    return _load_cabinet_html()


@app.get("/health")
async def health() -> PlainTextResponse:
    """Lightweight check for scripts."""
    return PlainTextResponse("ok")


@app.get("/api/settings")
async def get_daemon_settings() -> dict:
    return _load_daemon_settings_raw()


@app.put("/api/settings")
async def put_daemon_settings(body: DaemonSettingsPayload) -> dict:
    out = {"repoPollIntervalSeconds": body.repo_poll_interval_seconds}
    _write_daemon_settings(out)
    return out


def main() -> None:
    port = int(os.environ.get("PORT", str(DEFAULT_PORT)))
    host = os.environ.get("HOST", "127.0.0.1")
    uvicorn.run(app, host=host, port=port)


if __name__ == "__main__":
    main()
