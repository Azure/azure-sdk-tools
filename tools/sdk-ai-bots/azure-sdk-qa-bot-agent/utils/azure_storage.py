"""Azure Blob Storage client helpers.

Provides async blob download and upload using the Azure Storage SDK,
authenticated via the shared credential from ``utils.azure_credential``.
"""

from __future__ import annotations

import logging

from azure.storage.blob.aio import BlobServiceClient

from config.app_config import get as cfg
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_blob_service_client: BlobServiceClient | None = None


def _get_blob_service_client() -> BlobServiceClient:
    """Return a reusable async BlobServiceClient (singleton)."""
    global _blob_service_client
    if _blob_service_client is None:
        base_url = cfg("STORAGE_BASE_URL", "")
        if not base_url:
            raise RuntimeError("STORAGE_BASE_URL not configured in App Configuration")
        _blob_service_client = BlobServiceClient(
            account_url=base_url, credential=get_credential()
        )
    return _blob_service_client


async def download_blob(container: str, blob_name: str) -> bytes | None:
    """Download a blob and return its bytes, or *None* if not found / empty."""
    client = _get_blob_service_client()
    blob_client = client.get_blob_client(container=container, blob=blob_name)
    try:
        stream = await blob_client.download_blob()
        data = await stream.readall()
        return data if data else None
    except Exception:
        logger.warning(
            "Failed to download blob %s/%s", container, blob_name, exc_info=True
        )
        return None


async def upload_blob(container: str, blob_name: str, data: bytes) -> None:
    """Upload (overwrite) a blob with the given bytes."""
    client = _get_blob_service_client()
    blob_client = client.get_blob_client(container=container, blob=blob_name)
    await blob_client.upload_blob(data, overwrite=True)


async def close_storage_client() -> None:
    """Close the shared BlobServiceClient on shutdown."""
    global _blob_service_client
    if _blob_service_client is not None:
        await _blob_service_client.close()
        _blob_service_client = None
