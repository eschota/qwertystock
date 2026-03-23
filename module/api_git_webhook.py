"""
GitHub webhook: verify HMAC, git pull, restart qwertystock-way.service.
Секрет и ветка — из qwertystock_way.json (github_webhook / git).
"""
from __future__ import annotations

import hashlib
import hmac
import json
import logging
import os
import subprocess
import threading

from module.config import get_config

log = logging.getLogger(__name__)

_MODULE_DIR = os.path.dirname(os.path.abspath(__file__))
_DEFAULT_REPO = os.path.dirname(_MODULE_DIR)


def _repo_root() -> str:
    p = (get_config().get("git") or {}).get("repo_path") or ""
    return os.path.abspath(p) if p else _DEFAULT_REPO


def _secret() -> str:
    return ((get_config().get("github_webhook") or {}).get("secret") or "").strip()


def _branch() -> str:
    return (get_config().get("git") or {}).get("branch") or "main"


def _service_name() -> str:
    return (get_config().get("git") or {}).get("systemd_unit") or "qwertystock-way.service"


def _verify_github_signature(body: bytes, signature_header: str | None) -> bool:
    secret = _secret()
    if not secret:
        log.error("github_webhook.secret is not set in qwertystock_way.json")
        return False
    if not signature_header or not signature_header.startswith("sha256="):
        return False
    mac = hmac.new(secret.encode("utf-8"), body, hashlib.sha256).hexdigest()
    expected = "sha256=" + mac
    return hmac.compare_digest(expected, signature_header)


def _git_pull() -> tuple[bool, str]:
    repo = _repo_root()
    branch = _branch()
    r = subprocess.run(
        ["git", "-C", repo, "pull", "--ff-only", "origin", branch],
        capture_output=True,
        text=True,
        timeout=180,
    )
    out = (r.stdout or "") + (r.stderr or "")
    return r.returncode == 0, out.strip() or f"exit {r.returncode}"


def _restart_service() -> None:
    cmd = ["/usr/bin/sudo", "/bin/systemctl", "restart", _service_name()]
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
    if r.returncode != 0:
        log.error("systemctl restart failed: %s %s", r.stdout, r.stderr)
    else:
        log.info("systemctl restart %s ok", _service_name())


def _schedule_pull_and_restart() -> None:
    def job() -> None:
        ok, msg = _git_pull()
        if ok:
            log.info("git pull ok: %s", msg[:500])
            _restart_service()
        else:
            log.error("git pull failed: %s", msg[:2000])

    threading.Thread(target=job, daemon=True).start()


def handle_git_webhook(body: bytes, headers: dict[str, str]) -> tuple[int, bytes]:
    lower = {k.lower(): v for k, v in headers.items()}
    sig = lower.get("x-hub-signature-256")
    event = lower.get("x-github-event", "")

    if not _verify_github_signature(body, sig):
        return 401, b"invalid signature"

    if event == "ping":
        return 200, json.dumps({"ok": True, "msg": "pong"}).encode()

    if event != "push":
        return 200, json.dumps({"ok": True, "ignored": event}).encode()

    try:
        payload = json.loads(body.decode("utf-8"))
    except json.JSONDecodeError:
        return 400, b"bad json"

    ref = payload.get("ref") or ""
    if ref != f"refs/heads/{_branch()}":
        return 200, json.dumps({"ok": True, "ignored_ref": ref}).encode()

    _schedule_pull_and_restart()
    return 202, json.dumps({"ok": True, "action": "pull_and_restart_scheduled"}).encode()
