"""Tests for the read-only Azure Blob repository file store."""

from __future__ import annotations

import asyncio
import json
import re

import pytest

from utils.code_repository_store import (
    REPOSITORY_FILE_PROVIDER_INSTRUCTIONS,
    AzureBlobAgentFileStore,
)


class _Downloader:
    def __init__(self, data: bytes) -> None:
        self._data = data

    async def readall(self) -> bytes:
        return self._data


class _BlobClient:
    def __init__(self, blobs: dict[str, bytes], name: str) -> None:
        self._blobs = blobs
        self._name = name

    async def download_blob(self) -> _Downloader:
        if self._name not in self._blobs:
            from azure.core.exceptions import ResourceNotFoundError

            raise ResourceNotFoundError("missing")
        return _Downloader(self._blobs[self._name])


class _ContainerClient:
    def __init__(self, blobs: dict[str, bytes]) -> None:
        self._blobs = blobs

    def get_blob_client(self, name: str) -> _BlobClient:
        return _BlobClient(self._blobs, name)


def _store() -> AzureBlobAgentFileStore:
    files = [
        "Azure/typespec-azure/packages/core/main.tsp",
        "Azure/typespec-azure/packages/core/lib/operations.tsp",
        "Azure/typespec-azure/packages/samples/delete.tsp",
        "microsoft/typespec/packages/compiler/src/checker.ts",
    ]
    blobs = {
        "mirror/manifest.json": json.dumps(
            {
                "schema_version": 1,
                "files": files,
                "repositories": [
                    {"name": "Azure/typespec-azure"},
                    {"name": "microsoft/typespec"},
                ],
            }
        ).encode(),
        "mirror/Azure/typespec-azure/packages/core/main.tsp": (
            b"model Widget {\n  name: string;\n}\n"
        ),
        "mirror/Azure/typespec-azure/packages/core/lib/operations.tsp": (
            b"op deleteResource(): void;\n"
        ),
        "mirror/Azure/typespec-azure/packages/samples/delete.tsp": (
            b"op deleteSample(): void;\n"
        ),
        "mirror/microsoft/typespec/packages/compiler/src/checker.ts": (
            b"export function checkWidget() {}\n"
        ),
    }
    return AzureBlobAgentFileStore(
        container_client=_ContainerClient(blobs),  # type: ignore[arg-type]
        prefix="mirror",
        read_concurrency=2,
    )


def test_lists_virtual_directories_and_reads_current_blob() -> None:
    store = _store()

    entries = asyncio.run(store.list_children(""))
    assert [(entry.name, entry.type) for entry in entries] == [
        ("Azure", "directory"),
        ("microsoft", "directory"),
        ("manifest.json", "file"),
    ]
    assert (
        asyncio.run(
            store.read("Azure/typespec-azure/packages/core/main.tsp")
        )
        == "model Widget {\n  name: string;\n}\n"
    )
    assert asyncio.run(store.read("not-listed.txt")) is None


def test_searches_current_manifest_files_with_bounds() -> None:
    store = _store()

    results = asyncio.run(
        store.search(
            "Azure/typespec-azure/packages",
            "widget",
            glob_pattern="*.tsp",
            recursive=True,
        )
    )

    assert len(results) == 1
    assert results[0].file_name == "core/main.tsp"
    assert [match.line_number for match in results[0].matching_lines] == [1]
    assert [match.line for match in results[0].matching_lines] == [
        "model Widget {"
    ]


def test_search_prioritizes_library_declarations_over_samples() -> None:
    store = _store()

    results = asyncio.run(
        store.search(
            "Azure/typespec-azure/packages",
            "delete",
            glob_pattern="*.tsp",
            recursive=True,
        )
    )

    assert [result.file_name for result in results] == [
        "core/lib/operations.tsp",
        "samples/delete.tsp",
    ]


def test_rejects_unsafe_paths_and_invalid_regex() -> None:
    store = _store()

    with pytest.raises(ValueError, match="invalid repository path"):
        asyncio.run(store.read("../secret"))
    with pytest.raises(ValueError, match="invalid repository path"):
        asyncio.run(store.read("/Azure/typespec-azure/packages/core/main.tsp"))
    with pytest.raises(ValueError, match="forward slashes"):
        asyncio.run(store.read(r"Azure\typespec-azure\packages\main.tsp"))
    with pytest.raises(re.error):
        asyncio.run(store.search("", "[", recursive=True))
    with pytest.raises(ValueError, match="requires a directory"):
        asyncio.run(store.search("", "widget", recursive=True))
    with pytest.raises(ValueError, match="within a synchronized repository"):
        asyncio.run(store.search("packages", "widget", recursive=True))


def test_rejects_unsafe_blob_prefix() -> None:
    with pytest.raises(ValueError, match="invalid repository path"):
        AzureBlobAgentFileStore(
            container_client=_ContainerClient({}),  # type: ignore[arg-type]
            prefix="/mirror",
        )


def test_rejects_mutations() -> None:
    store = _store()

    with pytest.raises(PermissionError, match="read-only"):
        asyncio.run(store.write("file.tsp", "model A {}"))
    with pytest.raises(PermissionError, match="read-only"):
        asyncio.run(store.delete("file.tsp"))


def test_provider_instructions_limit_repository_search_to_implementation_evidence() -> None:
    assert "only when the active skill declares repositories" in (
        REPOSITORY_FILE_PROVIDER_INSTRUCTIONS
    )
    assert "exact implementation evidence" in REPOSITORY_FILE_PROVIDER_INSTRUCTIONS
    assert "read the most relevant declaration, rule, test, or sample" in (
        REPOSITORY_FILE_PROVIDER_INSTRUCTIONS
    )
    assert "empty directories and generic suffixes such as packages are invalid" in (
        REPOSITORY_FILE_PROVIDER_INSTRUCTIONS
    )
    assert "no more than two grep calls and two read calls" in (
        REPOSITORY_FILE_PROVIDER_INSTRUCTIONS
    )
    assert "Do not rely on grep snippets alone" in REPOSITORY_FILE_PROVIDER_INSTRUCTIONS
    assert "Do not use repository access for policy, process" in (
        REPOSITORY_FILE_PROVIDER_INSTRUCTIONS
    )
