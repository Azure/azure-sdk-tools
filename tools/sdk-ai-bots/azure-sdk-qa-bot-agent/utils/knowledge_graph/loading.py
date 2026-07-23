"""Load GraphRAG query-time artefacts: settings.yaml + parquet snapshots.

The ``azure-sdk-qa-bot-knowledge-graph-sync`` project publishes versioned
snapshots to blob storage at ``<container>/<prefix>/<name>.parquet`` with a
``latest.json`` manifest pointer at the container root. This module reads
the manifest, downloads the active snapshot's parquets into memory, and
loads the bot's GraphRAG config.
"""

from __future__ import annotations

import asyncio
import json
import logging
import shutil
import tempfile
from pathlib import Path
from typing import Any

from utils.azure_storage import download_blob

logger = logging.getLogger(__name__)

# Repo-root path to the bot agent's GraphRAG query config (settings.yaml).
# This file lives at ``utils/knowledge_graph/loading.py`` so the agent root
# is three parents up.
_GRAPHRAG_CONFIG_ROOT = (
    Path(__file__).resolve().parent.parent.parent / "config" / "graphrag"
)

# Parquet artefacts produced by the GraphRAG indexing pipeline that we need
# to drive query operations. ``documents`` is required so we can map
# text_units back to their original source-document path when building
# citations.
REQUIRED_PARQUETS: tuple[str, ...] = (
    "entities",
    "communities",
    "community_reports",
    "text_units",
    "relationships",
    "documents",
)


def load_config() -> Any:
    """Load the bot's GraphRAG settings.yaml.

    GraphRAG's ``load_config`` resolves ``${VAR}`` placeholders strictly
    from ``os.environ``. The bot's ``config.app_config.init()`` mirrors
    every Azure App Configuration key into ``os.environ`` at startup, so the
    placeholders in ``config/graphrag/settings.yaml`` resolve here without
    any per-key whitelist.
    """
    from graphrag.config.load_config import load_config as _load

    return _load(_GRAPHRAG_CONFIG_ROOT)


async def load_manifest(blob_container: str) -> dict[str, Any] | None:
    """Read ``latest.json`` from the blob container root, if present."""
    data = await download_blob(blob_container, "latest.json")
    if data is None:
        logger.info(
            "GraphRAG manifest not found at %s/latest.json — using unversioned layout",
            blob_container,
        )
        return None
    try:
        manifest = json.loads(data.decode("utf-8"))
    except (ValueError, UnicodeDecodeError) as exc:
        logger.warning(
            "GraphRAG manifest unparseable (%s); falling back to unversioned layout",
            exc,
        )
        return None
    if not isinstance(manifest, dict):
        logger.warning(
            "GraphRAG manifest is not a JSON object; falling back to unversioned layout"
        )
        return None
    return manifest


def snapshot_prefix(manifest: dict[str, Any] | None) -> str:
    """Resolve the parquet prefix to read from for this load."""
    if manifest:
        sub = str(manifest.get("prefix", "")).strip("/")
        if sub:
            return sub
    return ""


async def load_parquets_from_blob(
    blob_container: str, prefix: str
) -> "dict[str, Any]":
    """Download the snapshot parquets into a temp dir and read them in.

    The temp dir is removed once the parquets are loaded into memory, so
    repeated reloads don't leak disk.
    """
    temp_dir = Path(tempfile.mkdtemp(prefix="graphrag-output-"))
    try:
        logger.info(
            "Downloading GraphRAG parquets from blob container '%s' (prefix='%s') to %s",
            blob_container,
            prefix,
            temp_dir,
        )
        await asyncio.gather(
            *(
                _download_one_parquet(blob_container, name, prefix, temp_dir)
                for name in REQUIRED_PARQUETS
            )
        )
        return await asyncio.to_thread(_read_parquets, temp_dir)
    finally:
        await asyncio.to_thread(shutil.rmtree, temp_dir, True)


async def _download_one_parquet(
    blob_container: str, name: str, prefix: str, dest_dir: Path
) -> None:
    blob_name = f"{prefix}/{name}.parquet" if prefix else f"{name}.parquet"
    data = await download_blob(blob_container, blob_name)
    if data is None:
        raise FileNotFoundError(
            f"GraphRAG parquet not found: {blob_container}/{blob_name}"
        )
    (dest_dir / f"{name}.parquet").write_bytes(data)


def _read_parquets(path: Path) -> "dict[str, Any]":
    import pandas as pd

    dfs: dict[str, Any] = {}
    for name in REQUIRED_PARQUETS:
        file_path = path / f"{name}.parquet"
        if not file_path.is_file():
            raise FileNotFoundError(f"GraphRAG parquet not found: {file_path}")
        dfs[name] = pd.read_parquet(file_path)
    return dfs
