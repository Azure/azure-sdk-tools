# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from __future__ import annotations

import asyncio
import logging
from typing import Optional, Set, Union

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


# Uvicorn logger for debug output
_logger = logging.getLogger("uvicorn")


def require_roles(*roles: str):
    """
    Require that the token contains at least one of the specified roles.
    No implicit inclusion: all required roles must be present for endpoints requiring both.
    Logs relevant JWT info to Uvicorn for debugging.
    """
    required = set(roles)

    async def _dep(claims: dict = Depends(_require_auth)):
        token_roles = _safe_get_app_roles(claims)
        aud = claims.get("aud")
        appid = _caller_app_id(claims)
        subject = claims.get("sub")
        # Log relevant info for debugging
        _logger.info(
            "AUTH DEBUG: sub=%s, aud=%s, appid=%s, roles=%s, required=%s", subject, aud, appid, token_roles, required
        )
        if not required.intersection(token_roles):
            _logger.warning(
                "AUTH DENY: sub=%s, aud=%s, appid=%s, roles=%s, required=%s", subject, aud, appid, token_roles, required
            )
            raise HTTPException(status_code=403, detail="Missing required role(s): %s" % ", ".join(required))
        return claims

    return _dep


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


def _normalize_audience(aud_value: Union[str, list[str], None]) -> Set[str]:
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
    if not aud_values.intersection({APP_ID, _CANONICAL_AUD}):
        raise HTTPException(status_code=401, detail="Invalid token audience")

    # Canonicalize for downstream logic/telemetry
    claims["aud"] = _CANONICAL_AUD

    return claims
