"""Fetch Teams inline images and convert them to data-URI strings.

Teams inline images are hosted on ``smba.trafficmanager.net`` and require a
Bot Framework bearer token.  This module mirrors the Go backend's
``getImageDataURI`` helper.
"""

from __future__ import annotations

import base64
import logging
import os
from urllib.parse import urlparse

import httpx
from azure.core.credentials_async import AsyncTokenCredential
from azure.identity.aio import (
    AzureCliCredential,
    ChainedTokenCredential,
    ManagedIdentityCredential,
)

logger = logging.getLogger(__name__)

_BOTFRAMEWORK_SCOPE = "https://api.botframework.com/.default"
_ALLOWED_HOSTNAME = "smba.trafficmanager.net"

_bot_credential: AsyncTokenCredential | None = None


def _get_bot_credential() -> AsyncTokenCredential:
    """Return an async credential for the Bot Framework identity."""
    global _bot_credential
    if _bot_credential is None:
        bot_client_id = os.environ.get("BOT_CLIENT_ID", "")
        if bot_client_id:
            _bot_credential = ChainedTokenCredential(
                ManagedIdentityCredential(client_id=bot_client_id),
                AzureCliCredential(),
            )
        else:
            _bot_credential = AzureCliCredential()
            logger.warning(
                "BOT_CLIENT_ID not set — falling back to CLI credential for bot token"
            )
    return _bot_credential


async def get_image_data_uri(url: str) -> str:
    """Download a Teams image and return a ``data:image/...;base64,...`` URI.

    Only HTTPS URLs on ``smba.trafficmanager.net`` are allowed to prevent SSRF.

    Raises:
        ValueError: If the URL is not allowed.
        httpx.HTTPStatusError: If the download fails.
    """
    parsed = urlparse(url)
    if parsed.scheme != "https":
        raise ValueError("Only HTTPS URLs are allowed")
    if parsed.hostname != _ALLOWED_HOSTNAME:
        raise ValueError(
            f"Only {_ALLOWED_HOSTNAME} hostname is allowed, got {parsed.hostname}"
        )

    cred = _get_bot_credential()
    bot_tenant_id = os.environ.get("BOT_TENANT_ID", "")
    token = await cred.get_token(
        _BOTFRAMEWORK_SCOPE,
        tenant_id=bot_tenant_id or None,
    )

    async with httpx.AsyncClient(timeout=30.0) as client:
        resp = await client.get(
            url,
            headers={"Authorization": f"Bearer {token.token}"},
        )
        resp.raise_for_status()

    content_type = resp.headers.get("content-type", "image/png")
    # Normalise common content types
    if content_type not in ("image/png", "image/jpeg", "image/gif"):
        content_type = "image/png"

    b64 = base64.b64encode(resp.content).decode("ascii")
    return f"data:{content_type};base64,{b64}"
