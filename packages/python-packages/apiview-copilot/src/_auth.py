# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from __future__ import annotations

import asyncio
from typing import Iterable, Optional, Set

import jwt
import requests
from fastapi import Depends, HTTPException
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from jwt import ExpiredSignatureError, InvalidTokenError, PyJWKClient
from src._settings import SettingsManager

settings = SettingsManager()
TENANT_ID = settings.get("TENANT_ID")
APP_ID = settings.get("APP_ID")
REQUIRED_SCOPES_DEFAULT = {"user_impersonation"}  # default scope for routes

ISSUER = f"https://login.microsoftonline.com/{TENANT_ID}/v2.0"

# Keep a tiny in-memory cache for OIDC discovery + JWKS
_security = HTTPBearer(auto_error=True)
_jwks_keys: Optional[list[dict]] = None
_jwks_uri: Optional[str] = None
_lock = asyncio.Lock()

# Choose ONE canonical audience for PyJWT to verify.

# Only accept canonical audience
CANONICAL_AUD = f"api://{APP_ID}"

REQUIRED_SCOPES_DEFAULT: Set[str] = {"user_impersonation"}

_security = HTTPBearer(auto_error=True)


# Dynamically fetch and cache the JWKS URI and PyJWKClient
_JWK_CLIENT = None
_JWK_URI = None
_JWK_LOCK = asyncio.Lock()


def _get_jwk_client():
    global _JWK_CLIENT, _JWK_URI
    if _JWK_CLIENT is not None:
        return _JWK_CLIENT
    # Fetch OpenID config
    config_url = f"{ISSUER}/.well-known/openid-configuration"
    resp = requests.get(config_url, timeout=5)
    resp.raise_for_status()
    jwks_uri = resp.json()["jwks_uri"]
    _JWK_URI = jwks_uri
    _JWK_CLIENT = PyJWKClient(jwks_uri)
    return _JWK_CLIENT


def _safe_get_scopes(claims: dict) -> Set[str]:
    """Return delegated scopes from 'scp' as a set; empty set if none."""
    scp = claims.get("scp")
    if not scp:
        return set()
    # 'scp' is a space-delimited string per MSFT v2 tokens
    return set(scp.split())


async def require_auth(
    cred: HTTPAuthorizationCredentials = Depends(_security),
    required_scopes: Optional[Set[str]] = None,
) -> dict:
    """Validate JWT (issuer, signature, audience) and enforce delegated scopes."""
    token = cred.credentials

    # 1) Resolve signing key (handles rollover via kid)
    jwk_client = _get_jwk_client()
    try:
        signing_key = jwk_client.get_signing_key_from_jwt(token)
    except Exception:
        # One retry without cache in rare rollover race
        jwk_client = PyJWKClient(_JWK_URI, cache_keys=False)
        signing_key = jwk_client.get_signing_key_from_jwt(token)

    # 2) Decode without audience check, so we can force canonical form
    try:
        claims = jwt.decode(
            token,
            signing_key.key,
            algorithms=["RS256"],
            options={"verify_aud": False, "verify_iss": True},
            issuer=ISSUER,
        )
    except ExpiredSignatureError:
        raise HTTPException(status_code=401, detail="Token expired")
    except InvalidTokenError as e:
        raise HTTPException(status_code=401, detail=f"Invalid token: {str(e)}")

    # Force audience to canonical form if needed
    aud = claims.get("aud")
    if aud == APP_ID:
        aud = CANONICAL_AUD
        claims["aud"] = aud

    # Now strictly check canonical audience
    if aud != CANONICAL_AUD:
        raise HTTPException(status_code=401, detail="Invalid audience")

    # 4) Delegated scope enforcement (only for user tokens with 'scp')
    scopes_to_enforce = required_scopes if required_scopes is not None else REQUIRED_SCOPES_DEFAULT
    if scopes_to_enforce:
        token_scopes = _safe_get_scopes(claims)
        if not scopes_to_enforce.issubset(token_scopes):
            # Explicit 403 = token valid but insufficient permissions
            raise HTTPException(status_code=403, detail="Insufficient scope")

    # NOTE: If you later add App Roles (application permissions),
    # check 'roles' claim here, e.g.:
    # required_roles = {"Reviewer"}
    # if required_roles and not required_roles.intersection(set(claims.get("roles", []))):
    #     raise HTTPException(status_code=403, detail="Missing required app role")

    return claims


def require_scopes(*scopes: str):
    required = set(scopes)

    async def _dep(claims: dict = Depends(require_auth)):
        if required:
            token_scopes = _safe_get_scopes(claims)
            if not required.issubset(token_scopes):
                raise HTTPException(status_code=403, detail="Insufficient scope")
        return claims
