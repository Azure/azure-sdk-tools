# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from __future__ import annotations

import asyncio
from typing import Optional, Set

import aiohttp
import jwt
from fastapi import Depends, HTTPException
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from jwt import ExpiredSignatureError, InvalidTokenError, PyJWKClient
from src._settings import SettingsManager

settings = SettingsManager()
TENANT_ID = settings.get("TENANT_ID")
APP_ID = settings.get("APP_ID")

_SECURITY = HTTPBearer(auto_error=True)
_JWK_CLIENT = None
_JWK_URI = None

_JWK_CLIENT_LOCK = asyncio.Lock()

_CANONICAL_AUD = f"api://{APP_ID}"
_ISSUER = f"https://login.microsoftonline.com/{TENANT_ID}/v2.0"
_REQUIRED_SCOPES_DEFAULT: Set[str] = {"user_impersonation"}


async def _get_jwk_client():
    """
    Get (and cache) the PyJWKClient for the issuer's JWKS URI.
    Originally fetched from the OpenID Connect discovery document.
    """
    # pylint: disable=global-statement
    global _JWK_CLIENT, _JWK_URI
    if _JWK_CLIENT is not None:
        return _JWK_CLIENT
    async with _JWK_CLIENT_LOCK:
        if _JWK_CLIENT is not None:
            return _JWK_CLIENT
        # Fetch OpenID config asynchronously
        config_url = f"{_ISSUER}/.well-known/openid-configuration"
        async with aiohttp.ClientSession() as session:
            async with session.get(config_url, timeout=5) as resp:
                resp.raise_for_status()
                data = await resp.json()
        jwks_uri = data["jwks_uri"]
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
    cred: HTTPAuthorizationCredentials = Depends(_SECURITY),
    required_scopes: Optional[Set[str]] = None,
) -> dict:
    """
    Validate JWT (issuer, signature, audience) and enforce delegated scopes.
    :param cred: HTTP auth credentials (injected)
    :param required_scopes: additional scopes to require (optional)
    :return: decoded token claims
    :rtype: dict
    :raises HTTPException: 401 for invalid token, 403 for insufficient scope
    """
    token = cred.credentials

    # 1) Resolve signing key (handles rollover via kid)
    jwk_client = await _get_jwk_client()
    try:
        signing_key = jwk_client.get_signing_key_from_jwt(token)
    except Exception as e1:
        # One retry without cache in rare rollover race
        jwk_client = PyJWKClient(_JWK_URI, cache_keys=False)
        try:
            signing_key = jwk_client.get_signing_key_from_jwt(token)
        except Exception as e2:
            raise e2 from e1

    # 2) Decode without audience check, so we can force canonical form
    try:
        claims = jwt.decode(
            token,
            signing_key.key,
            algorithms=["RS256"],
            options={"verify_aud": False, "verify_iss": True},
            issuer=_ISSUER,
        )
    except ExpiredSignatureError as exc:
        raise HTTPException(status_code=401, detail="Token expired") from exc
    except InvalidTokenError as exc:
        raise HTTPException(status_code=401, detail=f"Invalid token: {str(exc)}") from exc

    # Force audience to canonical form if needed
    aud = claims.get("aud")
    if aud == APP_ID:
        aud = _CANONICAL_AUD
        claims["aud"] = aud

    # Now strictly check canonical audience
    if aud != _CANONICAL_AUD:
        raise HTTPException(status_code=401, detail="Invalid audience")

    # 4) Delegated scope enforcement (only for user tokens with 'scp')
    scopes_to_enforce = required_scopes if required_scopes is not None else _REQUIRED_SCOPES_DEFAULT
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
    """
    Require specific delegated scopes in addition to basic auth.

    :param scopes: scopes to require
    :type scopes: str
    """
    required = set(scopes)

    async def _dep(claims: dict = Depends(require_auth)):
        if required:
            token_scopes = _safe_get_scopes(claims)
            if not required.issubset(token_scopes):
                raise HTTPException(status_code=403, detail="Insufficient scope")
        return claims
