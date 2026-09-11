from __future__ import annotations

import asyncio
import json
from pathlib import PurePosixPath, PureWindowsPath
from typing import Any

from azure.core.exceptions import ResourceExistsError, ResourceNotFoundError
from azure.storage.blob import ContentSettings

from .models import SelectedFile

_MANIFEST_NAME = "manifest.json"
_TEXT_CONTENT = ContentSettings(content_type="text/plain; charset=utf-8")
_JSON_CONTENT = ContentSettings(content_type="application/json; charset=utf-8")


class BlobRepositoryStore:
    def __init__(
        self,
        container_client: Any,
        *,
        prefix: str = "",
        concurrency: int = 32,
    ) -> None:
        if concurrency <= 0:
            raise ValueError("concurrency must be positive")
        self._container = container_client
        self._prefix = _safe_prefix(prefix)
        self._concurrency = concurrency

    async def reconcile(
        self,
        files: list[SelectedFile],
        manifest: dict[str, Any],
    ) -> None:
        """Update source blobs in place, delete stale files, and publish last."""
        current = {file.path: file for file in files}
        if len(current) != len(files):
            raise ValueError("repository sync produced duplicate blob paths")
        expected_files = sorted(current)
        if manifest.get("files") != expected_files:
            raise ValueError("manifest files do not match selected repository files")

        await self._ensure_container()
        prior_manifest = await self._load_manifest()
        prior_files = set(prior_manifest.get("files", [])) if prior_manifest else set()

        semaphore = asyncio.Semaphore(self._concurrency)

        async def upload(file: SelectedFile) -> None:
            async with semaphore:
                blob = self._container.get_blob_client(
                    self._blob_name(_safe_blob_path(file.path))
                )
                await blob.upload_blob(
                    file.content,
                    overwrite=True,
                    content_settings=_TEXT_CONTENT,
                )

        await asyncio.gather(*(upload(file) for file in files))

        async def delete(path: str) -> None:
            safe_path = _safe_blob_path(path)
            async with semaphore:
                blob = self._container.get_blob_client(
                    self._blob_name(safe_path)
                )
                try:
                    await blob.delete_blob(delete_snapshots="include")
                except ResourceNotFoundError:
                    pass

        await asyncio.gather(
            *(delete(path) for path in sorted(prior_files - set(current)))
        )

        payload = (
            json.dumps(manifest, indent=2, sort_keys=True) + "\n"
        ).encode("utf-8")
        manifest_blob = self._container.get_blob_client(
            self._blob_name(_MANIFEST_NAME)
        )
        await manifest_blob.upload_blob(
            payload,
            overwrite=True,
            content_settings=_JSON_CONTENT,
        )

    async def _ensure_container(self) -> None:
        try:
            await self._container.create_container()
        except ResourceExistsError:
            pass

    async def _load_manifest(self) -> dict[str, Any] | None:
        blob = self._container.get_blob_client(self._blob_name(_MANIFEST_NAME))
        try:
            downloader = await blob.download_blob()
            payload = await downloader.readall()
        except ResourceNotFoundError:
            return None
        try:
            value = json.loads(payload.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise RuntimeError("existing repository manifest is invalid JSON") from exc
        if not isinstance(value, dict) or value.get("schema_version") != 1:
            raise RuntimeError("existing repository manifest has an unsupported schema")
        files = value.get("files")
        if not isinstance(files, list) or not all(
            isinstance(path, str) for path in files
        ):
            raise RuntimeError("existing repository manifest files are invalid")
        for path in files:
            _safe_blob_path(path)
        return value

    def _blob_name(self, path: str) -> str:
        return f"{self._prefix}/{path}" if self._prefix else path


def _safe_blob_path(value: str) -> str:
    if "\\" in value:
        raise ValueError(f"blob path must use forward slashes: {value!r}")
    raw = value.strip()
    if raw.startswith("/") or PureWindowsPath(raw).is_absolute():
        raise ValueError(f"unsafe repository blob path: {value!r}")
    raw = raw.rstrip("/")
    path = PurePosixPath(raw)
    if (
        not raw
        or path.is_absolute()
        or ".." in path.parts
        or "." in path.parts
        or raw == _MANIFEST_NAME
    ):
        raise ValueError(f"unsafe repository blob path: {value!r}")
    return path.as_posix()


def _safe_prefix(value: str) -> str:
    raw = value.strip()
    if not raw:
        return ""
    if (
        "\\" in raw
        or raw.startswith("/")
        or PureWindowsPath(raw).is_absolute()
    ):
        raise ValueError(f"unsafe repository blob prefix: {value!r}")
    raw = raw.rstrip("/")
    path = PurePosixPath(raw)
    if ".." in path.parts or "." in path.parts:
        raise ValueError(f"unsafe repository blob prefix: {value!r}")
    return path.as_posix()
