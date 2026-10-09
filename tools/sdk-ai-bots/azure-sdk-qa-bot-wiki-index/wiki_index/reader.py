"""Read markdown blobs from the knowledge container as ``(source_path, text)``."""

from __future__ import annotations

import logging
from pathlib import PurePosixPath

logger = logging.getLogger(__name__)

_MD_SUFFIXES = {".md", ".mdx"}


def source_folder(source_path: str) -> str:
    """First path segment (the KnowledgeSource / context_id), or '' at root."""
    parts = source_path.strip().lstrip("/").split("/")
    return parts[0] if len(parts) > 1 else ""


def rel_title(source_path: str) -> str:
    """Return the folder-relative ``#``-encoded KB title."""
    path = source_path.strip().lstrip("/")
    folder = source_folder(source_path)
    if folder and path.startswith(folder + "/"):
        return path[len(folder) + 1 :]
    return path


async def read_blob_container(container_client) -> list[tuple[str, str]]:
    """Read every markdown blob in the container as ``(source_path, text)``."""
    out: list[tuple[str, str]] = []
    tombstoned = 0
    empty = 0
    # The knowledge sync retires a document by setting IsDeleted metadata and
    # leaving the content in place, so metadata has to be requested and checked.
    async for blob in container_client.list_blobs(include=["metadata"]):
        name = blob.name
        if PurePosixPath(name).suffix.lower() not in _MD_SUFFIXES:
            continue
        if (blob.metadata or {}).get("IsDeleted") == "true":
            tombstoned += 1
            continue
        downloader = await container_client.download_blob(name)
        data = await downloader.readall()
        try:
            text = data.decode("utf-8")
        except UnicodeDecodeError:
            text = data.decode("utf-8", errors="replace")
        if not text.strip():
            empty += 1
            continue
        out.append((name, text))
    logger.info(
        "read_blob_container: %d markdown blobs (%d tombstoned, %d empty)",
        len(out),
        tombstoned,
        empty,
    )
    return out
