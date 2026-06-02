"""Azure Blob Storage service for knowledge documents.

Handles blob upload, listing, deletion (soft-delete via metadata),
content change detection via MD5 hashing, and metadata comparison.
"""

from __future__ import annotations

import hashlib
import logging
import os
from base64 import b64encode
from pathlib import Path
from typing import Any

from azure.identity import DefaultAzureCredential
from azure.storage.blob import BlobServiceClient, ContainerClient, ContentSettings

logger = logging.getLogger(__name__)


class BlobService:
    """Azure Blob Storage operations against a single container.

    By default the container name comes from
    ``STORAGE_KNOWLEDGE_CONTAINER`` so existing callers keep working
    unchanged. Pass ``container_name`` explicitly when you need to talk
    to a different container (e.g. the GraphRAG output container).
    """

    def __init__(self, container_name: str | None = None) -> None:
        account_name = os.environ.get("STORAGE_ACCOUNT_NAME")
        resolved_container = container_name or os.environ.get("STORAGE_KNOWLEDGE_CONTAINER")
        if not account_name:
            raise RuntimeError("STORAGE_ACCOUNT_NAME environment variable is required")
        if not resolved_container:
            raise RuntimeError(
                "Container name not provided and STORAGE_KNOWLEDGE_CONTAINER is not set"
            )

        credential = DefaultAzureCredential()
        account_url = f"https://{account_name}.blob.core.windows.net"
        self._service_client = BlobServiceClient(account_url, credential=credential)
        self._container_client: ContainerClient = self._service_client.get_container_client(
            resolved_container
        )
        self._container_name = resolved_container

    @property
    def container_name(self) -> str:
        return self._container_name

    def put_blob(
        self,
        blob_path: str,
        content: str | bytes,
        metadata: dict[str, str] | None = None,
    ) -> None:
        """Upload content to blob storage."""
        blob_client = self._container_client.get_blob_client(blob_path)
        data = content.encode("utf-8") if isinstance(content, str) else content
        blob_client.upload_blob(
            data,
            overwrite=True,
            content_settings=ContentSettings(content_type=self._get_content_type(blob_path)),
            metadata=metadata,
        )
        logger.info("Uploaded %s to blob storage", blob_path)

    def list_blobs(self, prefix: str | None = None) -> dict[str, Any]:
        """List all blobs in the container, returning {name: blob_properties}."""
        blobs: dict[str, Any] = {}
        kwargs: dict[str, Any] = {"include": ["metadata"]}
        if prefix:
            kwargs["name_starts_with"] = prefix
        for blob in self._container_client.list_blobs(**kwargs):
            blobs[blob.name] = blob
        logger.info("Listed %d blobs", len(blobs))
        return blobs

    def delete_blob(self, blob_path: str) -> None:
        """Soft-delete a blob by setting IsDeleted metadata."""
        blob_client = self._container_client.get_blob_client(blob_path)
        blob_client.set_blob_metadata({"IsDeleted": "true"})
        logger.info("Soft-deleted blob %s", blob_path)

    def download_blob(self, blob_name: str) -> bytes:
        """Download blob content as bytes."""
        blob_client = self._container_client.get_blob_client(blob_name)
        return blob_client.download_blob().readall()

    def download_blobs_to_dir(self, blob_paths: list[str], target_dir: Path) -> int:
        """Download specific blobs to a local directory.

        Args:
            blob_paths: List of blob names to download.
            target_dir: Local directory to write files into (cleaned first).

        Returns:
            Number of blobs successfully downloaded.
        """
        import shutil

        if target_dir.exists():
            shutil.rmtree(target_dir)
        target_dir.mkdir(parents=True)

        count = 0
        for blob_path in blob_paths:
            try:
                data = self.download_blob(blob_path)
                local_path = target_dir / blob_path
                local_path.parent.mkdir(parents=True, exist_ok=True)
                local_path.write_bytes(data)
                count += 1
            except Exception as e:
                logger.warning("Failed to download blob %s: %s", blob_path, e)

        logger.info("Downloaded %d/%d blobs to %s", count, len(blob_paths), target_dir)
        return count

    def download_all_blobs_to_dir(
        self, target_dir: Path, source_prefixes: list[str] | None = None
    ) -> int:
        """Download all blobs (optionally filtered by prefix) to a local directory.

        Args:
            target_dir: Local directory to write files into (cleaned first).
            source_prefixes: Only download blobs starting with these prefixes.

        Returns:
            Number of blobs downloaded.
        """
        import shutil

        if target_dir.exists():
            shutil.rmtree(target_dir)
        target_dir.mkdir(parents=True)

        count = 0
        for blob in self._container_client.list_blobs():
            name: str = blob.name
            if source_prefixes:
                if not any(name.startswith(f"{p}/") for p in source_prefixes):
                    continue
            try:
                data = self._container_client.get_blob_client(name).download_blob().readall()
                local_path = target_dir / name
                local_path.parent.mkdir(parents=True, exist_ok=True)
                local_path.write_bytes(data)
                count += 1
            except Exception as e:
                logger.warning("Failed to download blob %s: %s", name, e)

        logger.info("Downloaded %d blobs to %s", count, target_dir)
        return count

    # --- Change detection ---

    def has_content_changed(
        self,
        blob_path: str,
        content: str | bytes,
        existing_blobs: dict[str, Any],
    ) -> bool:
        """Check if content has changed by comparing MD5 hashes."""
        current_md5 = self._calculate_md5(content)
        existing = existing_blobs.get(blob_path)

        if existing is None:
            return True

        # Check soft-delete flag
        if existing.metadata and existing.metadata.get("IsDeleted") == "true":
            return True

        existing_md5 = existing.properties.get("content_md5")
        if not existing_md5:
            return True

        # Azure returns content_md5 as bytearray; convert to base64 for comparison
        if isinstance(existing_md5, (bytes, bytearray)):
            existing_md5_b64 = b64encode(existing_md5).decode()
        else:
            existing_md5_b64 = str(existing_md5)

        return existing_md5_b64 != current_md5

    def has_metadata_changed(
        self,
        blob_path: str,
        current_metadata: dict[str, str] | None,
        existing_blobs: dict[str, Any],
    ) -> bool:
        """Check if blob metadata has changed."""
        existing = existing_blobs.get(blob_path)

        if existing is None:
            return current_metadata is not None

        existing_meta = existing.metadata or {}

        if not current_metadata:
            return bool(existing_meta.get("scope") or existing_meta.get("service_type"))

        if current_metadata.get("scope") != existing_meta.get("scope"):
            return True
        if current_metadata.get("service_type") != existing_meta.get("service_type"):
            return True

        return False

    # --- Private helpers ---

    @staticmethod
    def _calculate_md5(content: str | bytes) -> str:
        """Calculate MD5 hash as base64 (matching Azure's contentMD5 format)."""
        data = content.encode("utf-8") if isinstance(content, str) else content
        return b64encode(hashlib.md5(data).digest()).decode()

    @staticmethod
    def _get_content_type(filename: str) -> str:
        ext = filename.rsplit(".", 1)[-1].lower() if "." in filename else ""
        return {
            "md": "text/markdown",
            "mdx": "text/markdown",
            "txt": "text/plain",
            "json": "application/json",
            "html": "text/html",
        }.get(ext, "application/octet-stream")
