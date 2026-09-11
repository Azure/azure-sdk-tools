from __future__ import annotations

import asyncio
import json
from typing import Any

import pytest
from azure.core.exceptions import ResourceExistsError, ResourceNotFoundError

from code_repository_sync.models import SelectedFile
from code_repository_sync.storage import BlobRepositoryStore


class _Downloader:
    def __init__(self, content: bytes) -> None:
        self._content = content

    async def readall(self) -> bytes:
        return self._content


class _Blob:
    def __init__(self, container: "_Container", name: str) -> None:
        self._container = container
        self._name = name

    async def download_blob(self) -> _Downloader:
        if self._name not in self._container.blobs:
            raise ResourceNotFoundError("missing")
        return _Downloader(self._container.blobs[self._name])

    async def upload_blob(
        self,
        content: bytes,
        *,
        overwrite: bool,
        content_settings: Any,
    ) -> None:
        del overwrite, content_settings
        if self._name == self._container.fail_upload:
            raise RuntimeError("upload failed")
        self._container.events.append(("upload", self._name))
        self._container.blobs[self._name] = content

    async def delete_blob(self, *, delete_snapshots: str) -> None:
        del delete_snapshots
        self._container.events.append(("delete", self._name))
        self._container.blobs.pop(self._name, None)


class _Container:
    def __init__(self, blobs: dict[str, bytes]) -> None:
        self.blobs = blobs
        self.events: list[tuple[str, str]] = []
        self.fail_upload = ""

    async def create_container(self) -> None:
        raise ResourceExistsError("exists")

    def get_blob_client(self, name: str) -> _Blob:
        return _Blob(self, name)


def _manifest(files: list[str]) -> dict[str, Any]:
    return {
        "schema_version": 1,
        "updated_at": "2026-09-11T00:00:00Z",
        "files": files,
        "repositories": [],
    }


def test_reconciles_in_place_and_writes_manifest_last() -> None:
    old_manifest = _manifest(["Azure/repo/old.ts", "Azure/repo/keep.ts"])
    container = _Container(
        {
            "mirror/manifest.json": json.dumps(old_manifest).encode(),
            "mirror/Azure/repo/old.ts": b"old",
            "mirror/Azure/repo/keep.ts": b"before",
        }
    )
    files = [
        SelectedFile("Azure/repo/keep.ts", b"after"),
        SelectedFile("Azure/repo/new.ts", b"new"),
    ]
    manifest = _manifest([file.path for file in files])

    asyncio.run(
        BlobRepositoryStore(
            container, prefix="mirror", concurrency=2
        ).reconcile(files, manifest)
    )

    assert container.blobs["mirror/Azure/repo/keep.ts"] == b"after"
    assert container.blobs["mirror/Azure/repo/new.ts"] == b"new"
    assert "mirror/Azure/repo/old.ts" not in container.blobs
    assert container.events[-1] == ("upload", "mirror/manifest.json")


def test_does_not_write_manifest_when_source_upload_fails() -> None:
    old_manifest = _manifest(["Azure/repo/old.ts"])
    original = json.dumps(old_manifest).encode()
    container = _Container({"manifest.json": original})
    container.fail_upload = "Azure/repo/new.ts"

    with pytest.raises(RuntimeError, match="upload failed"):
        asyncio.run(
            BlobRepositoryStore(container).reconcile(
                [SelectedFile("Azure/repo/new.ts", b"new")],
                _manifest(["Azure/repo/new.ts"]),
            )
        )

    assert container.blobs["manifest.json"] == original


def test_rejects_absolute_manifest_paths_and_prefixes() -> None:
    container = _Container({})
    with pytest.raises(ValueError, match="unsafe repository blob prefix"):
        BlobRepositoryStore(container, prefix="/mirror")

    with pytest.raises(ValueError, match="unsafe repository blob path"):
        asyncio.run(
            BlobRepositoryStore(container).reconcile(
                [SelectedFile("/Azure/repo/main.ts", b"source")],
                _manifest(["/Azure/repo/main.ts"]),
            )
        )
