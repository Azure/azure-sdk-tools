# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from __future__ import annotations

import asyncio
from typing import Iterable, Optional, Set

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
_REQUIRED_APP_ROLES_DEFAULT: Set[str] = {"Reader"}

# TODO: If we want to allow-list specific client IDs
ALLOWED_CALLER_APP_IDS: Set[str] = {
    # "<APIView-client-id-guid>",  # fill this in to pin the caller
}


def _safe_get_app_roles(claims: dict) -> Set[str]:
    """
    Return application roles from 'roles' as a set.
    In v2 tokens, 'roles' is an array; be defensive if it's a string.
    """
    roles = claims.get("roles", [])
    if isinstance(roles, str):
        roles = roles.split()
    return set(roles)


def _caller_app_id(claims: dict) -> Optional[str]:
    """
    For application tokens, AAD includes 'appid' (client app id).
    For some flows (OBO), 'azp' may be present—fall back appropriately.
    """
    return claims.get("azp") or claims.get("appid")


def _normalize_audience(aud_value):
    # aud can be string or list of strings
    if isinstance(aud_value, list):
        return set(aud_value)
    return {aud_value} if aud_value else set()


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


async def _require_auth(
    cred: HTTPAuthorizationCredentials = Depends(_SECURITY),
) -> dict:
    """
    Validate JWT (issuer, signature, audience).
    :param cred: HTTP auth credentials (injected)
    :return: decoded token claims
    :rtype: dict
    :raises HTTPException: 401 for invalid token
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

    # 3) Audience enforcement (supports both APP_ID and api://APP_ID)
    aud_values = _normalize_audience(claims.get("aud"))

    # Accept either raw client id or api:// form, but normalize to canonical
    acceptable = {APP_ID, _CANONICAL_AUD}
    if not aud_values.intersection(acceptable):
        raise HTTPException(status_code=401, detail="Invalid audience")

    # Canonicalize for downstream logic/telemetry
    claims["aud"] = _CANONICAL_AUD

    return claims


def require_app_roles(*roles: str):
    """
    Require specific application roles (from 'roles') for app-only tokens
    issued to managed identities / service principals.
    """
    required = set(roles) if roles else _REQUIRED_APP_ROLES_DEFAULT

    async def _dep(claims: dict = Depends(_require_auth)):
        token_roles = _safe_get_app_roles(claims)
        if not token_roles:
            # No 'roles' => not an app token (or no roles assigned)
            raise HTTPException(status_code=403, detail="Application roles required")
        if required and not required.issubset(token_roles):
            raise HTTPException(status_code=403, detail="Missing required app role")
        return claims

    return _dep


def require_caller_app_ids(*allowed_appids: str):
    """
    Restrict access to specific calling applications by client id (GUID).
    """
    allowed = set(allowed_appids) if allowed_appids else ALLOWED_CALLER_APP_IDS

    async def _dep(claims: dict = Depends(_require_auth)):
        appid = _caller_app_id(claims)
        if allowed and appid not in allowed:
            raise HTTPException(status_code=403, detail="Unauthorized caller application")
        return claims

    return _dep


def require_permissions(
    scopes: Iterable[str] = None,
    roles: Iterable[str] = None,
    allow_either: bool = True,
):
    """
    Require delegated scopes and/or application roles.
    If allow_either=True, accept tokens that satisfy either set.
    If False, require both (rare).
    """
    scopes = set(scopes) if scopes is not None else _REQUIRED_SCOPES_DEFAULT.copy()
    roles = set(roles) if roles is not None else _REQUIRED_APP_ROLES_DEFAULT.copy()

    async def _dep(claims: dict = Depends(_require_auth)):
        token_scopes = _safe_get_scopes(claims)
        token_roles = _safe_get_app_roles(claims)

        scope_ok = (not scopes) or (scopes.issubset(token_scopes))
        role_ok = (not roles) or (roles.issubset(token_roles))

        if allow_either:
            if not (scope_ok or role_ok):
                raise HTTPException(status_code=403, detail="Required permissions not satisfied")
        else:
            if not (scope_ok and role_ok):
                raise HTTPException(status_code=403, detail="Required permissions not satisfied")
        return claims

    return _dep


def require_scopes(*scopes: str):
    """
    Require specific delegated scopes in addition to basic auth.

    :param scopes: scopes to require
    :type scopes: str
    """
    required = set(scopes)

    async def _dep(claims: dict = Depends(_require_auth)):
        if required:
            token_scopes = _safe_get_scopes(claims)
            if not required.issubset(token_scopes):
                raise HTTPException(status_code=403, detail="Insufficient scope")
        return claims

    return _dep
