"""Corpus reader — stream ``(source_path, text)`` from the knowledge blob container.

Reads the same markdown the KB indexer ingests, in memory (avoids Windows path
issues with quoted blob names). ``source_path`` is the container-relative blob
name (e.g. ``"typespec_docs/getting-started#...#06-versioning.mdx"``); the first
path segment is the source folder = the KB ``context_id``.
"""

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
    """Folder-relative ``#``-encoded path — the KB ``title`` (drives link resolution).

    ``typespec_docs/getting-started#...#06-versioning.mdx`` →
    ``getting-started#...#06-versioning.mdx`` (drop the leading folder segment;
    keep the rest verbatim, since blob names already ``#``-encode subpaths).
    """
    path = source_path.strip().lstrip("/")
    folder = source_folder(source_path)
    if folder and path.startswith(folder + "/"):
        return path[len(folder) + 1 :]
    return path


async def read_blob_container(container_client, prefix: str = "") -> list[tuple[str, str]]:
    """Read every markdown blob under *prefix* as ``(source_path, text)``."""
    out: list[tuple[str, str]] = []
    async for blob in container_client.list_blobs(name_starts_with=prefix or None):
        name = blob.name
        if PurePosixPath(name).suffix.lower() not in _MD_SUFFIXES:
            continue
        downloader = await container_client.download_blob(name)
        data = await downloader.readall()
        try:
            text = data.decode("utf-8")
        except UnicodeDecodeError:
            text = data.decode("utf-8", errors="replace")
        out.append((name, text))
    logger.info("read_blob_container: %d markdown blobs under %r", len(out), prefix)
    return out
