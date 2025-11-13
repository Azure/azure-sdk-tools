# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from __future__ import annotations

import asyncio
from typing import Iterable, Optional, Set

import jwt
from fastapi import Depends, HTTPException
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from jwt import ExpiredSignatureError, InvalidTokenError, PyJWKClient

# TODO: Get these from appConfig
TENANT_ID = "72f988bf-86f1-41af-91ab-2d7cd011db47"  # Microsoft tenant you showed in the token
APP_ID = "66bad3f5-7326-4160-b644-987e6da42d1c"  # your API application (client) ID
REQUIRED_SCOPES_DEFAULT = {"user_impersonation"}  # default scope for routes

ISSUER = f"https://login.microsoftonline.com/{TENANT_ID}/v2.0"

# Keep a tiny in-memory cache for OIDC discovery + JWKS
_security = HTTPBearer(auto_error=True)
_jwks_keys: Optional[list[dict]] = None
_jwks_uri: Optional[str] = None
_lock = asyncio.Lock()

# Choose ONE canonical audience for PyJWT to verify.
CANONICAL_AUD = f"api://{APP_ID}"
# Optionally accept alternates after decode (defensive)
ACCEPTED_AUDIENCES: Set[str] = {CANONICAL_AUD, APP_ID}

REQUIRED_SCOPES_DEFAULT: Set[str] = {"user_impersonation"}

_security = HTTPBearer(auto_error=True)

# Cache the JWK client (it caches keys internally)
_JWK_CLIENT = PyJWKClient(f"{ISSUER}/discovery/v2.0/keys")


def _safe_get_scopes(claims: dict) -> Set[str]:
    """Return delegated scopes from 'scp' as a set; empty set if none."""
    scp = claims.get("scp")
    if not scp:
        return set()
    # 'scp' is a space-delimited string per MSFT v2 tokens
    return set(scp.split())


def _aud_ok(aud_claim) -> bool:
    """Secondary audience check allowing an alternate AAD format."""
    if isinstance(aud_claim, str):
        return aud_claim in ACCEPTED_AUDIENCES
    if isinstance(aud_claim, Iterable):
        return any(a in ACCEPTED_AUDIENCES for a in aud_claim)
    return False


async def require_auth(
    cred: HTTPAuthorizationCredentials = Depends(_security),
    required_scopes: Optional[Set[str]] = None,
) -> dict:
    """Validate JWT (issuer, signature, audience) and enforce delegated scopes."""
    token = cred.credentials

    # 1) Resolve signing key (handles rollover via kid)
    try:
        signing_key = _JWK_CLIENT.get_signing_key_from_jwt(token)
    except Exception:
        # One retry without cache in rare rollover race
        signing_key = PyJWKClient(f"{ISSUER}/discovery/v2.0/keys", cache_keys=False).get_signing_key_from_jwt(token)

    # 2) Decode + verify issuer and canonical audience
    try:
        claims = jwt.decode(
            token,
            signing_key.key,
            algorithms=["RS256"],
            audience=CANONICAL_AUD,  # single canonical value
            issuer=ISSUER,
            options={"verify_aud": True, "verify_iss": True},
        )
    except ExpiredSignatureError:
        raise HTTPException(status_code=401, detail="Token expired")
    except InvalidTokenError as e:
        # Fall back to a clearer message
        raise HTTPException(status_code=401, detail=f"Invalid token: {str(e)}")

    # 3) Secondary audience acceptance (accept GUID or api://GUID)
    if not _aud_ok(claims.get("aud")):
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
