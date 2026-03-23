"""OAuth2 Authorization Code + PKCE для кабинета ↔ www.qwertystock.com (см. docs/auth/)."""

from __future__ import annotations

import base64
import hashlib
import secrets
from urllib.parse import urlencode

import httpx

CLIENT_ID = "qwertystock-local-cabinet"
AUTHORIZATION_URL = "https://www.qwertystock.com/oauth/authorize"
TOKEN_URL = "https://www.qwertystock.com/oauth/token"
USERINFO_URL = "https://www.qwertystock.com/oauth/userinfo"
SCOPES = "openid profile email"


def new_oauth_state() -> str:
    return secrets.token_urlsafe(48)


def generate_pkce_pair() -> tuple[str, str]:
    """code_verifier и code_challenge (S256)."""
    verifier = secrets.token_urlsafe(48)
    digest = hashlib.sha256(verifier.encode("ascii")).digest()
    challenge = base64.urlsafe_b64encode(digest).rstrip(b"=").decode("ascii")
    return verifier, challenge


def build_authorize_url(*, redirect_uri: str, state: str, code_challenge: str) -> str:
    q = urlencode(
        {
            "response_type": "code",
            "client_id": CLIENT_ID,
            "redirect_uri": redirect_uri,
            "scope": SCOPES,
            "state": state,
            "code_challenge": code_challenge,
            "code_challenge_method": "S256",
        }
    )
    return f"{AUTHORIZATION_URL}?{q}"


async def exchange_code_for_tokens(
    *,
    code: str,
    redirect_uri: str,
    code_verifier: str,
) -> dict:
    async with httpx.AsyncClient() as client:
        r = await client.post(
            TOKEN_URL,
            data={
                "grant_type": "authorization_code",
                "code": code,
                "redirect_uri": redirect_uri,
                "client_id": CLIENT_ID,
                "code_verifier": code_verifier,
            },
            headers={"Content-Type": "application/x-www-form-urlencoded"},
            timeout=30.0,
        )
        if r.status_code != 200:
            detail = r.text
            try:
                err = r.json()
                detail = err.get("error_description") or err.get("error") or detail
            except Exception:
                pass
            raise RuntimeError(detail or f"token HTTP {r.status_code}")
        return r.json()


async def fetch_userinfo(access_token: str) -> dict:
    async with httpx.AsyncClient() as client:
        r = await client.get(
            USERINFO_URL,
            headers={"Authorization": f"Bearer {access_token}"},
            timeout=30.0,
        )
        if r.status_code != 200:
            raise RuntimeError(r.text or f"userinfo HTTP {r.status_code}")
        return r.json()


def userinfo_to_public(user: dict) -> dict:
    return {
        "sub": user.get("sub"),
        "name": user.get("name"),
        "email": user.get("email"),
    }
