"""
GitHub webhook: verify HMAC, git pull, restart qwertystock-way.service.
Secret: env QWERTYSTOCK_WEBHOOK_SECRET (same value as in GitHub webhook settings).
"""
from __future__ import annotations

import hashlib
import hmac
import json
import logging
import os
import subprocess
import threading

log = logging.getLogger(__name__)

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SERVICE_NAME = os.environ.get("QWERTYSTOCK_SYSTEMD_UNIT", "qwertystock-way.service")
TARGET_BRANCH = os.environ.get("QWERTYSTOCK_GIT_BRANCH", "main")


def _secret() -> str:
    return (os.environ.get("QWERTYSTOCK_WEBHOOK_SECRET") or "").strip()


def _verify_github_signature(body: bytes, signature_header: str | None) -> bool:
    secret = _secret()
    if not secret:
        log.error("QWERTYSTOCK_WEBHOOK_SECRET is not set")
        return False
    if not signature_header or not signature_header.startswith("sha256="):
        return False
    mac = hmac.new(secret.encode("utf-8"), body, hashlib.sha256).hexdigest()
    expected = "sha256=" + mac
    return hmac.compare_digest(expected, signature_header)


def _git_pull() -> tuple[bool, str]:
    r = subprocess.run(
        ["git", "-C", REPO_ROOT, "pull", "--ff-only", "origin", TARGET_BRANCH],
        capture_output=True,
        text=True,
        timeout=180,
    )
    out = (r.stdout or "") + (r.stderr or "")
    return r.returncode == 0, out.strip() or f"exit {r.returncode}"


def _restart_service() -> None:
    cmd = ["/usr/bin/sudo", "/bin/systemctl", "restart", SERVICE_NAME]
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
    if r.returncode != 0:
        log.error("systemctl restart failed: %s %s", r.stdout, r.stderr)
    else:
        log.info("systemctl restart %s ok", SERVICE_NAME)


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
    """
    Returns HTTP status and body for POST /api/git/webhook.
    headers keys may be mixed case; normalize for lookup.
    """
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
    if ref != f"refs/heads/{TARGET_BRANCH}":
        return 200, json.dumps({"ok": True, "ignored_ref": ref}).encode()

    _schedule_pull_and_restart()
    return 202, json.dumps({"ok": True, "action": "pull_and_restart_scheduled"}).encode()
