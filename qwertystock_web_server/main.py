"""HTTP entry for the QwertyStock local web UI (bootstrapper default: port 7332)."""

from __future__ import annotations

import html
import json
import os
import secrets
import sys
import time
from pathlib import Path

import uvicorn
from fastapi import FastAPI, Request
from fastapi.responses import HTMLResponse, PlainTextResponse, RedirectResponse
from pydantic import BaseModel, ConfigDict, Field
from starlette.middleware.sessions import SessionMiddleware

import oauth

DEFAULT_PORT = 7332

_APP_DIR = Path(__file__).resolve().parent
_REPO_ROOT = _APP_DIR.parent
_CABINET_HTML_PATH = _APP_DIR / "static" / "cabinet.html"

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


def _python_version_display() -> str:
    v = sys.version_info
    return f"{v.major}.{v.minor}.{v.micro}"


def _git_revision_display() -> str:
    v = os.environ.get("QS_GIT_REVISION", "").strip()
    return v if v else "—"


def _product_version() -> str:
    """Один номер с инсталлером: env от лаунчера или файл VERSION в корне репозитория."""
    env = os.environ.get("QS_PRODUCT_VERSION", "").strip()
    if env:
        return env
    p = _REPO_ROOT / "VERSION"
    if p.is_file():
        for line in p.read_text(encoding="utf-8").splitlines():
            t = line.strip()
            if t and not t.startswith("#"):
                return t
    raise FileNotFoundError(
        f"Не найдена версия продукта: задайте QS_PRODUCT_VERSION или файл {p}"
    )


def _cabinet_ui_kind() -> str:
    """Диагностика: свежий инсталлер не подменяет этот файл — он только из git-репозитория на диске."""
    try:
        raw = _CABINET_HTML_PATH.read_text(encoding="utf-8")
    except OSError:
        return "missing"
    low = raw.lower()
    if 'id="auth-bar"' in raw and "/auth/login" in raw:
        return "oauth-v2"
    if "заглушка" in low or "cabinet-stub" in low or "ui stub" in low:
        return "stub-legacy"
    return "unknown"


def _version_payload() -> dict:
    git = os.environ.get("QS_GIT_REVISION", "").strip()
    return {
        "productVersion": _product_version(),
        "python": _python_version_display(),
        "gitRevision": git or None,
        "cabinetUi": _cabinet_ui_kind(),
    }


def _session_secret() -> str:
    env = os.environ.get("QS_SESSION_SECRET", "").strip()
    if env:
        return env
    path = Path.home() / ".qwertystock" / "session_secret"
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.is_file():
        return path.read_text(encoding="utf-8").strip()
    secret = secrets.token_hex(32)
    path.write_text(secret + "\n", encoding="utf-8")
    return secret


def _auth_error_page(message: str) -> str:
    esc = html.escape(message, quote=True)
    return f"""<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Ошибка входа — QwertyStock</title>
  <style>
    body {{ font-family: system-ui, sans-serif; max-width: 520px; margin: 2rem auto; padding: 0 1rem;
      color: #1a1a1a; }}
    a {{ color: #f27a23; }}
  </style>
</head>
<body>
  <h1>Ошибка входа</h1>
  <p>{esc}</p>
  <p><a href="/">На главную кабинета</a></p>
</body>
</html>"""


def _load_cabinet_html() -> str:
    raw = _CABINET_HTML_PATH.read_text(encoding="utf-8")
    return (
        raw.replace("{{PRODUCT_VERSION}}", _product_version())
        .replace("{{PYTHON_VERSION}}", _python_version_display())
        .replace("{{GIT_REVISION}}", _git_revision_display())
    )


app = FastAPI(title="QwertyStock Web", version=_product_version())

app.add_middleware(
    SessionMiddleware,
    secret_key=_session_secret(),
    max_age=14 * 24 * 3600,
    same_site="lax",
    https_only=False,
)


@app.get("/auth/login")
async def auth_login(request: Request) -> RedirectResponse:
    state = oauth.new_oauth_state()
    verifier, challenge = oauth.generate_pkce_pair()
    redirect_uri = f"{request.url.scheme}://{request.url.netloc}/auth/callback"
    request.session["oauth_state"] = state
    request.session["code_verifier"] = verifier
    request.session["oauth_redirect_uri"] = redirect_uri
    url = oauth.build_authorize_url(
        redirect_uri=redirect_uri, state=state, code_challenge=challenge
    )
    return RedirectResponse(url=url, status_code=302)


@app.get("/auth/callback")
async def auth_callback(
    request: Request,
    code: str | None = None,
    state: str | None = None,
    error: str | None = None,
    error_description: str | None = None,
) -> HTMLResponse | RedirectResponse:
    if error:
        msg = error_description or error
        return HTMLResponse(_auth_error_page(msg), status_code=400)

    saved = request.session.get("oauth_state")
    verifier = request.session.get("code_verifier")
    redirect_uri = request.session.get("oauth_redirect_uri")
    if not saved or not verifier or not redirect_uri or state != saved:
        return HTMLResponse(
            _auth_error_page(
                "Сессия входа устарела или state не совпадает. Нажмите «Войти» в кабинете снова."
            ),
            status_code=400,
        )
    if not code:
        return HTMLResponse(_auth_error_page("Параметр code не получен."), status_code=400)

    try:
        tokens = await oauth.exchange_code_for_tokens(
            code=code, redirect_uri=redirect_uri, code_verifier=verifier
        )
    except RuntimeError as exc:
        return HTMLResponse(_auth_error_page(str(exc)), status_code=400)

    access = tokens.get("access_token")
    if not access:
        return HTMLResponse(
            _auth_error_page("Ответ сервера токенов не содержит access_token."),
            status_code=400,
        )

    try:
        raw = await oauth.fetch_userinfo(access)
    except RuntimeError as exc:
        return HTMLResponse(_auth_error_page(str(exc)), status_code=400)

    user = oauth.userinfo_to_public(raw)

    request.session.pop("oauth_state", None)
    request.session.pop("code_verifier", None)
    request.session.pop("oauth_redirect_uri", None)

    request.session["authenticated"] = True
    request.session["user"] = user
    request.session["access_token"] = access
    rt = tokens.get("refresh_token")
    if rt:
        request.session["refresh_token"] = rt
    expires_in = tokens.get("expires_in")
    if isinstance(expires_in, int) and expires_in > 0:
        request.session["token_expires_at"] = int(time.time()) + expires_in

    return RedirectResponse(url="/", status_code=302)


@app.get("/auth/logout")
async def auth_logout(request: Request) -> RedirectResponse:
    request.session.clear()
    return RedirectResponse(url="/", status_code=302)


@app.get("/api/auth/me")
async def auth_me(request: Request) -> dict:
    if not request.session.get("authenticated"):
        return {"authenticated": False}
    user = request.session.get("user") or {}
    return {"authenticated": True, "user": user}


def _cabinet_html_response() -> HTMLResponse:
    # Чтобы после git pull не показывать старую разметку из кеша браузера на localhost.
    return HTMLResponse(
        content=_load_cabinet_html(),
        headers={
            "Cache-Control": "no-store, no-cache, must-revalidate",
            "Pragma": "no-cache",
        },
    )


@app.get("/", response_class=HTMLResponse)
async def root() -> HTMLResponse:
    return _cabinet_html_response()


@app.get("/cabinet", response_class=HTMLResponse)
async def cabinet() -> HTMLResponse:
    """Тот же HTML, что и `/` — отдельный путь, чтобы в адресной строке было видно «кабинет»."""
    return _cabinet_html_response()


@app.get("/health")
async def health() -> PlainTextResponse:
    """Lightweight check for scripts."""
    return PlainTextResponse("ok")


@app.get("/api/version")
async def get_version() -> dict:
    """Единая версия продукта, Python и опционально git (QS_GIT_REVISION)."""
    return _version_payload()


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
