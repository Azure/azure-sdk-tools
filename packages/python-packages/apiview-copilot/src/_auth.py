# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from __future__ import annotations

import asyncio
from typing import Iterable, Optional

import httpx
import jwt
from fastapi import Depends, HTTPException
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from jwt import ExpiredSignatureError, InvalidTokenError, PyJWKClient

# TODO: Get these from appConfig
TENANT_ID = "72f988bf-86f1-41af-91ab-2d7cd011db47"  # Microsoft tenant you showed in the token
APP_ID = "66bad3f5-7326-4160-b644-987e6da42d1c"  # your API application (client) ID
REQUIRED_SCOPES_DEFAULT = {"user_impersonation"}  # default scope for routes

ISSUER = f"https://login.microsoftonline.com/{TENANT_ID}/v2.0"
ACCEPTED_AUDIENCES = {APP_ID, f"api://{APP_ID}"}  # accept GUID or api://GUID

# Keep a tiny in-memory cache for OIDC discovery + JWKS
_security = HTTPBearer(auto_error=True)
_jwks_keys: Optional[list[dict]] = None
_jwks_uri: Optional[str] = None
_lock = asyncio.Lock()


async def _ensure_jwks_loaded() -> list[dict]:
    global _jwks_keys, _jwks_uri
    if _jwks_keys is not None:
        return _jwks_keys

    async with _lock:
        if _jwks_keys is not None:
            return _jwks_keys
        async with httpx.AsyncClient(timeout=5) as client:
            oidc = (await client.get(f"{ISSUER}/.well-known/openid-configuration")).json()
            _jwks_uri = oidc["jwks_uri"]
            _jwks_keys = (await client.get(_jwks_uri)).json()["keys"]
    return _jwks_keys


def _check_audience(aud_claim) -> bool:
    if isinstance(aud_claim, str):
        return aud_claim in ACCEPTED_AUDIENCES
    if isinstance(aud_claim, Iterable):
        return any(a in ACCEPTED_AUDIENCES for a in aud_claim)
    return False


def _check_scopes(claims: dict, required_scopes: set[str]) -> bool:
    token_scopes = set((claims.get("scp") or "").split())
    return required_scopes.issubset(token_scopes)


async def require_auth(
    cred: HTTPAuthorizationCredentials = Depends(_security),
    required_scopes: Optional[set[str]] = None,
) -> dict:
    """
    Validates a JWT access token from Microsoft Entra ID and (optionally) enforces scopes.
    Returns claims dict on success or raises 401/403.
    """
    # Use PyJWT's PyJWKClient to fetch and cache keys
    jwks_url = f"{ISSUER}/discovery/v2.0/keys"
    jwk_client = PyJWKClient(jwks_url)
    try:
        signing_key = jwk_client.get_signing_key_from_jwt(cred.credentials)
    except Exception:
        # Try to refresh JWKS if key not found (key rollover)
        jwk_client = PyJWKClient(jwks_url, cache_keys=False)
        try:
            signing_key = jwk_client.get_signing_key_from_jwt(cred.credentials)
        except Exception:
            raise HTTPException(status_code=401, detail="Unknown signing key (kid)")

    try:
        claims = jwt.decode(
            cred.credentials,
            signing_key.key,
            algorithms=["RS256"],
            audience=list(ACCEPTED_AUDIENCES),
            issuer=ISSUER,
            options={"verify_aud": True},
        )
    except ExpiredSignatureError:
        raise HTTPException(status_code=401, detail="Token expired")
    except InvalidTokenError:
        raise HTTPException(status_code=401, detail="Invalid token")

    # Secondary audience check (defensive)
    if not _check_audience(claims.get("aud")):
        raise HTTPException(status_code=401, detail="Invalid audience")

    # Scopes (only for delegated user tokens)
    scopes_to_enforce = required_scopes if required_scopes is not None else REQUIRED_SCOPES_DEFAULT
    if scopes_to_enforce and not _check_scopes(claims, scopes_to_enforce):
        raise HTTPException(status_code=403, detail="Insufficient scope")

    # You can also add role checks here if you later adopt app roles (claims["roles"])
    return claims


# Helpers to create route-specific dependencies
def require_scopes(*scopes: str):
    required = set(scopes)

    async def _dep(claims: dict = Depends(require_auth)):
        # require_auth already checked default scopes; re-check if custom were given
        if required and not _check_scopes(claims, required):
            raise HTTPException(status_code=403, detail="Insufficient scope")
        return claims

    return _dep
