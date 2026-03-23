"""
Уведомления в Telegram (Bot API sendMessage).
"""
from __future__ import annotations

import json
import logging
import urllib.error
import urllib.request
from typing import Any

log = logging.getLogger(__name__)


def send_message(cfg: dict[str, Any], text: str) -> bool:
    tg = cfg.get("telegram") or {}
    if not tg.get("notify_on_server_start", True):
        return False
    token = (tg.get("bot_token") or "").strip()
    chat_id = tg.get("notify_chat_id")
    if not token or chat_id is None or chat_id == "":
        log.warning("telegram notify skipped: missing bot_token or notify_chat_id")
        return False
    url = f"https://api.telegram.org/bot{token}/sendMessage"
    body = json.dumps(
        {
            "chat_id": chat_id,
            "text": text,
            "parse_mode": "HTML",
            "disable_web_page_preview": True,
        },
        ensure_ascii=False,
    ).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            raw = resp.read().decode("utf-8", errors="replace")
            data = json.loads(raw)
            if not data.get("ok"):
                log.error("telegram API error: %s", raw[:500])
                return False
            return True
    except urllib.error.HTTPError as e:
        log.error("telegram HTTPError: %s %s", e.code, e.read()[:500])
        return False
    except OSError as e:
        log.error("telegram request failed: %s", e)
        return False


def git_short_rev(repo_root: str) -> str:
    import subprocess

    try:
        r = subprocess.run(
            ["git", "-C", repo_root, "rev-parse", "--short", "HEAD"],
            capture_output=True,
            text=True,
            timeout=5,
        )
        if r.returncode == 0:
            return (r.stdout or "").strip() or "?"
    except OSError:
        pass
    return "?"


def build_startup_message(cfg: dict[str, Any], repo_root: str) -> str:
    rev = git_short_rev(repo_root)
    tpl = (cfg.get("telegram") or {}).get("startup_message_template")
    if tpl and isinstance(tpl, str):
        return tpl.format(revision=rev, repo_root=repo_root)
    return (
        "Qwertystock way: сервер запущен.\n"
        f"Ревизия: <code>{rev}</code>\n"
        "HTTP слушает конфиг из <code>qwertystock_way.json</code>."
    )
