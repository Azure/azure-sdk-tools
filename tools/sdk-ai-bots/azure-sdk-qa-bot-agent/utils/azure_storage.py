"""Azure Blob Storage client helpers.

Provides async blob download and upload using the Azure Storage SDK,
authenticated via the shared credential from ``utils.azure_credential``.
"""

from __future__ import annotations

import logging
from dataclasses import dataclass
from typing import Callable, Literal, overload

from azure.core import MatchConditions
from azure.core.exceptions import ResourceNotFoundError
from azure.storage.blob.aio import BlobServiceClient
from config.app_config import get as cfg
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_blob_service_client: BlobServiceClient | None = None


@dataclass(frozen=True)
class BlobContent:
    data: bytes
    etag: str


def create_blob_service_client(
    settings: Callable[[str, str], str | None] = cfg,
) -> BlobServiceClient:
    """Create a Blob client from an explicit configuration scope."""
    base_url = settings("STORAGE_BASE_URL", "")
    if not base_url:
        raise RuntimeError("STORAGE_BASE_URL not configured in App Configuration")
    return BlobServiceClient(account_url=base_url, credential=get_credential())


def _get_blob_service_client() -> BlobServiceClient:
    """Return a reusable async BlobServiceClient (singleton)."""
    global _blob_service_client
    if _blob_service_client is None:
        _blob_service_client = create_blob_service_client()
    return _blob_service_client


@overload
async def download_blob(
    container: str,
    blob_name: str,
    *,
    include_metadata: Literal[False] = False,
    client: BlobServiceClient | None = None,
) -> bytes | None: ...


@overload
async def download_blob(
    container: str,
    blob_name: str,
    *,
    include_metadata: Literal[True],
    client: BlobServiceClient | None = None,
) -> BlobContent | None: ...


async def download_blob(
    container: str,
    blob_name: str,
    *,
    include_metadata: bool = False,
    client: BlobServiceClient | None = None,
) -> bytes | BlobContent | None:
    """Download a blob, optionally including its ETag for conditional updates."""
    client = client or _get_blob_service_client()
    blob_client = client.get_blob_client(container=container, blob=blob_name)
    try:
        stream = await blob_client.download_blob()
        data = await stream.readall()
        if include_metadata:
            return BlobContent(data=data, etag=stream.properties.etag)
        return data if data else None
    except ResourceNotFoundError:
        logger.info("Blob not found: %s/%s", container, blob_name)
        return None


async def upload_blob(
    container: str,
    blob_name: str,
    data: bytes,
    *,
    etag: str | None = None,
    client: BlobServiceClient | None = None,
) -> None:
    """Upload a blob, optionally only when its ETag is unchanged."""
    client = client or _get_blob_service_client()
    blob_client = client.get_blob_client(container=container, blob=blob_name)
    if etag is not None:
        await blob_client.upload_blob(
            data,
            overwrite=True,
            etag=etag,
            match_condition=MatchConditions.IfNotModified,
        )
    else:
        await blob_client.upload_blob(data, overwrite=True)


async def close_storage_client() -> None:
    """Close the shared BlobServiceClient on shutdown."""
    global _blob_service_client
    if _blob_service_client is not None:
        await _blob_service_client.close()
        _blob_service_client = None
