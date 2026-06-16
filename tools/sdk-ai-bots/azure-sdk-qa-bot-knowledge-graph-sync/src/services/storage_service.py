"""Azure Blob Storage service for the knowledge graph sync.

Trimmed to the single operation this project needs: writing the GraphRAG
``latest.json`` manifest (see ``graphrag/publish_output.py``). Blob input
for the GraphRAG build itself goes through GraphRAG's native
``azure_blob`` storage, not this class.
"""

from __future__ import annotations

import hashlib
import logging
import os
from base64 import b64encode

from azure.identity import DefaultAzureCredential
from azure.storage.blob import BlobServiceClient, ContainerClient, ContentSettings

logger = logging.getLogger(__name__)


class BlobService:
    """Minimal Azure Blob Storage writer."""

    def __init__(self, container_name: str | None = None) -> None:
        account_name = os.environ.get("STORAGE_ACCOUNT_NAME")
        account_url = f"https://{account_name}.blob.core.windows.net"
        self._credential = DefaultAzureCredential()
        self._service = BlobServiceClient(
            account_url=account_url, credential=self._credential
        )
        default_container = os.environ.get("STORAGE_KNOWLEDGE_CONTAINER")
        self._container_name = container_name or default_container
        self._container: ContainerClient = self._service.get_container_client(
            self._container_name
        )

    @property
    def container_name(self) -> str:
        return self._container_name

    def put_blob(
        self,
        blob_path: str,
        content: str | bytes,
        metadata: dict[str, str] | None = None,
    ) -> None:
        data = content.encode("utf-8") if isinstance(content, str) else content
        content_settings = ContentSettings(
            content_md5=bytearray(b64encode(hashlib.md5(data).digest())),
        )
        self._container.upload_blob(
            name=blob_path,
            data=data,
            overwrite=True,
            metadata=metadata,
            content_settings=content_settings,
        )
