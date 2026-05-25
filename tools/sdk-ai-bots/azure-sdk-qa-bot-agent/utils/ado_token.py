"""Shared ADO bearer-token resolver with JIT refresh.

Centralises the KV-first → managed-identity-fallback logic used by both
``tools.ado_mcp_tools`` (MCPStdioTool env injection) and
``tools.pipeline_tools`` (direct REST calls).

The cached token is reused until ``exp - _TOKEN_REFRESH_BUFFER_SECS``
(mirroring the GitHub MCP token-manager pattern).  When the JWT has no
parseable ``exp``, falls back to a short fixed TTL so we still pick up
rotations.
"""

from __future__ import annotations

import asyncio
import base64
import binascii
import json
import logging
import time

from utils.azure_credential import get_credential
from utils.azure_keyvault import get_secret

logger = logging.getLogger(__name__)

# Key Vault secret holding the ADO bearer token.
TOKEN_SECRET_NAME = "ado-token"
# AAD resource ID for Azure DevOps.
ADO_RESOURCE_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798/.default"
# Refresh the token this many seconds before its JWT ``exp`` claim.
_TOKEN_REFRESH_BUFFER_SECS = 5 * 60
# Fallback cache TTL when the token has no parseable ``exp`` claim.
_TOKEN_FALLBACK_TTL_SECS = 5 * 60

# Cached (token, refresh_after_monotonic).
_token_cache: tuple[str, float] | None = None
_token_cache_lock = asyncio.Lock()


def _jwt_exp_seconds(token: str) -> int | None:
    """Return the ``exp`` claim (Unix seconds) from a JWT, or ``None``."""
    parts = token.split(".")
    if len(parts) < 2:
        return None
    payload_b64 = parts[1]
    padding = "=" * (-len(payload_b64) % 4)
    try:
        payload_bytes = base64.urlsafe_b64decode(payload_b64 + padding)
        payload = json.loads(payload_bytes)
    except (binascii.Error, ValueError, UnicodeDecodeError):
        return None
    exp = payload.get("exp")
    return int(exp) if isinstance(exp, (int, float)) else None


async def resolve_token() -> str:
    """Return an ADO AAD bearer token, refreshed JIT.

    Resolution order:
      1. Key Vault secret ``ado-token`` (populated by an out-of-band
         job, used while the Foundry agent identity is not an ADO org
         member).
      2. The agent identity itself via :func:`get_credential` — taken
         when the KV secret is absent or empty.  Switch to this path by
         simply deleting the secret once ADO accepts the agent identity.
    """
    global _token_cache
    now = time.monotonic()
    if _token_cache is not None and _token_cache[1] > now:
        return _token_cache[0]

    async with _token_cache_lock:
        # Double-checked: another coroutine may have just refreshed.
        if _token_cache is not None and _token_cache[1] > now:
            return _token_cache[0]

        value = await get_secret(TOKEN_SECRET_NAME)
        token = (value or "").strip()
        if token:
            pass
        else:
            logger.info(
                "KV secret '%s' is empty; minting ADO token via agent identity",
                TOKEN_SECRET_NAME,
            )
            credential = get_credential()
            access_token = await credential.get_token(ADO_RESOURCE_SCOPE)
            token = access_token.token

        # Compute the refresh deadline from the JWT exp claim.
        exp_unix = _jwt_exp_seconds(token)
        if exp_unix is not None:
            seconds_until_exp = exp_unix - int(time.time())
            ttl = max(seconds_until_exp - _TOKEN_REFRESH_BUFFER_SECS, 0)
            logger.debug(
                "ADO token loaded, exp in %ds, refresh in %ds",
                seconds_until_exp,
                ttl,
            )
        else:
            ttl = _TOKEN_FALLBACK_TTL_SECS
            logger.debug(
                "ADO token loaded; no parseable exp, using %ds TTL",
                ttl,
            )

        _token_cache = (token, now + ttl)
        return token
