"""Fetch Teams inline images and uploaded image files as data-URI strings.

Bot Framework attachments use the bot identity; Graph hosted content uses
the backend identity with channel-message read permissions. SharePoint files
are downloaded through Graph with the backend identity's file-read permissions.
"""

from __future__ import annotations

import base64
import binascii
import logging
import os
import re
from urllib.parse import unquote, urlparse

import httpx
from azure.core.credentials_async import AsyncTokenCredential
from azure.identity.aio import (
    AzureCliCredential,
    ChainedTokenCredential,
    ManagedIdentityCredential,
)

from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_IMAGE_DOWNLOAD_TIMEOUT_SECS = 30.0
MAX_IMAGE_BYTES = 5 * 1024 * 1024

_BOTFRAMEWORK_SCOPE = "https://api.botframework.com/.default"
_GRAPH_SCOPE = "https://graph.microsoft.com/.default"
_GRAPH_IMAGE_PATH = re.compile(
    r"/(?:v1\.0|beta)/teams/[^/]+/channels/[^/]+/messages/[^/]+/"
    r"(?:replies/[^/]+/)?hostedContents/[^/]+/\$value"
)
_GRAPH_FILE_PATH = re.compile(
    r"/(?:v1\.0|beta)/(?:drives/[^/]+/items/[^/]+|"
    r"shares/[^/]+/driveItem)/content"
)
_SHAREPOINT_HOST = re.compile(r"[a-z0-9][a-z0-9-]*\.sharepoint\.com")
_ONEDRIVE_DOWNLOAD_HOST = re.compile(r"[a-z0-9][a-z0-9-]*\.files\.1drv\.com")
_IMAGE_TYPES = ("image/png", "image/jpeg", "image/gif", "image/webp")

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


def _validate_https_url(url: str) -> None:
    parsed = urlparse(url)
    if parsed.scheme != "https":
        raise ValueError("Only HTTPS URLs are allowed")
    if (
        parsed.username is not None
        or parsed.password is not None
        or parsed.port not in (None, 443)
    ):
        raise ValueError("Image URLs must not contain credentials or non-HTTPS ports")
    if parsed.fragment:
        raise ValueError("Image URLs must not contain fragments")
    if "\\" in url or any(ord(char) < 32 or ord(char) == 127 for char in url):
        raise ValueError("Image URLs must not contain backslashes or control characters")


def validate_teams_image_url(url: str) -> None:
    """Allow Teams images, Graph file content, and SharePoint file references."""
    _validate_https_url(url)
    parsed = urlparse(url)
    if parsed.hostname == "graph.microsoft.com":
        if (
            not (
                _GRAPH_IMAGE_PATH.fullmatch(parsed.path)
                or _GRAPH_FILE_PATH.fullmatch(parsed.path)
            )
            or parsed.query
            or any(
                unquote(part) in (".", "..") or "/" in unquote(part)
                or "\\" in unquote(part)
                for part in parsed.path.split("/")
            )
        ):
            raise ValueError("Only Graph hosted-content or file-content URLs are allowed")
    elif _SHAREPOINT_HOST.fullmatch(parsed.hostname or ""):
        if not parsed.path.strip("/"):
            raise ValueError("SharePoint image URLs must identify a file")
    elif parsed.hostname != "smba.trafficmanager.net":
        raise ValueError("Only Bot Framework, Graph, and SharePoint image hosts are allowed")


def _validate_download_url(url: str) -> None:
    """Restrict Graph's preauthenticated redirect; never send it a bearer token."""
    _validate_https_url(url)
    hostname = urlparse(url).hostname or ""
    if not (
        _SHAREPOINT_HOST.fullmatch(hostname)
        or _ONEDRIVE_DOWNLOAD_HOST.fullmatch(hostname)
    ):
        raise ValueError("Unsupported Graph file download host")


async def get_image_data_uri(url: str) -> str:
    """Validate connector-supplied bytes or download a bounded Teams image."""
    if url.startswith("data:"):
        header, separator, encoded = url.partition(",")
        if not separator or not header.endswith(";base64"):
            raise ValueError("Teams image must be a base64 data URI")
        if len(encoded) > 4 * ((MAX_IMAGE_BYTES + 2) // 3):
            raise ValueError("Teams image exceeds the 5 MiB limit")
        try:
            content = base64.b64decode(encoded, validate=True)
        except binascii.Error as exc:
            raise ValueError("Teams image contains invalid base64") from exc
        return _image_data_uri(content, header[5:-7])
    validate_teams_image_url(url)
    parsed = urlparse(url)
    if _SHAREPOINT_HOST.fullmatch(parsed.hostname or ""):
        sharing_url = str(httpx.URL(url))
        sharing_token = "u!" + base64.urlsafe_b64encode(
            sharing_url.encode("utf-8")
        ).decode("ascii").rstrip("=")
        url = f"https://graph.microsoft.com/v1.0/shares/{sharing_token}/driveItem/content"
    parsed = urlparse(url)
    is_graph_file = (
        parsed.hostname == "graph.microsoft.com"
        and bool(_GRAPH_FILE_PATH.fullmatch(parsed.path))
    )
    if parsed.hostname == "graph.microsoft.com":
        token = await get_credential().get_token(_GRAPH_SCOPE)
    else:
        bot_tenant_id = os.environ.get("BOT_TENANT_ID", "")
        token = await _get_bot_credential().get_token(
            _BOTFRAMEWORK_SCOPE,
            tenant_id=bot_tenant_id or None,
        )

    headers = {"Authorization": f"Bearer {token.token}"}
    content = bytearray()
    content_type = ""
    async with httpx.AsyncClient(
        timeout=_IMAGE_DOWNLOAD_TIMEOUT_SECS, follow_redirects=False
    ) as client:
        for attempt in range(2):
            async with client.stream("GET", url, headers=headers) as resp:
                if attempt == 0 and is_graph_file and resp.status_code == 302:
                    url = resp.headers.get("location", "")
                    _validate_download_url(url)
                    headers = {}
                    continue
                resp.raise_for_status()
                content_type = resp.headers.get("content-type", "").split(";")[0].strip().lower()
                if content_type not in (*_IMAGE_TYPES, "application/octet-stream"):
                    raise ValueError("Unsupported Teams image content type")
                if int(resp.headers.get("content-length", "0")) > MAX_IMAGE_BYTES:
                    raise ValueError("Teams image exceeds the 5 MiB limit")
                async for chunk in resp.aiter_bytes():
                    if len(content) + len(chunk) > MAX_IMAGE_BYTES:
                        raise ValueError("Teams image exceeds the 5 MiB limit")
                    content.extend(chunk)
                break
    return _image_data_uri(content, content_type)


def _image_data_uri(content: bytes | bytearray, content_type: str) -> str:
    if len(content) > MAX_IMAGE_BYTES:
        raise ValueError("Teams image exceeds the 5 MiB limit")
    if not content:
        raise ValueError("Teams image is empty")
    if content_type not in (*_IMAGE_TYPES, "application/octet-stream"):
        raise ValueError("Unsupported Teams image content type")
    if content_type == "application/octet-stream":
        content_type = next(
            (kind for kind in _IMAGE_TYPES if _matches_image_type(content, kind)), ""
        )
    if not _matches_image_type(content, content_type):
        raise ValueError("Teams image bytes do not match the declared content type")
    b64 = base64.b64encode(content).decode("ascii")
    return f"data:{content_type};base64,{b64}"


def _matches_image_type(content: bytes | bytearray, content_type: str) -> bool:
    if content_type == "image/png":
        return content.startswith(b"\x89PNG\r\n\x1a\n")
    if content_type == "image/jpeg":
        return content.startswith(b"\xff\xd8\xff")
    if content_type == "image/gif":
        return content.startswith((b"GIF87a", b"GIF89a"))
    if content_type == "image/webp":
        return content.startswith(b"RIFF") and content[8:12] == b"WEBP"
    return False
