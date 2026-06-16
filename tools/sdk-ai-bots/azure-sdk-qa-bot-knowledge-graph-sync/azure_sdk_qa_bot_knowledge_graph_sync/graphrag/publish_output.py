"""Publish a freshly-built GraphRAG snapshot.

In the blob-direct architecture, ``build_index`` writes parquets to
``STORAGE_GRAPHRAG_OUTPUT_CONTAINER/snapshots/<snapshot_id>/`` directly.
This module therefore only needs to write ``latest.json`` to the
container root pointing at the new snapshot prefix. The manifest is
written *last* (and is the bot's sole source of truth), so partially-built
snapshots are never picked up.

The bot picks up the new snapshot on its own: it polls ``latest.json``
on a daily schedule and hot-swaps the index when the ``build_id``
changes (see ``azure-sdk-qa-bot-agent`` server lifespan +
``KnowledgeGraphService.reload_if_changed``).

Required env var:

* ``STORAGE_GRAPHRAG_OUTPUT_CONTAINER``  — destination container (same
  one the build wrote into).
"""

from __future__ import annotations

import asyncio
import datetime as _dt
import json
import logging
import os
from typing import Any

from azure_sdk_qa_bot_knowledge_graph_sync.graphrag import snapshot_base_dir
from azure_sdk_qa_bot_knowledge_graph_sync.services.storage_service import BlobService

logger = logging.getLogger(__name__)

# Parquet artefacts the bot needs at query time. We do not validate
# their presence here (the build pipeline owns that contract); we just
# publish their names in the manifest so the bot knows which blobs to
# fetch from the snapshot prefix.
_PARQUET_FILES: tuple[str, ...] = (
    "entities.parquet",
    "communities.parquet",
    "community_reports.parquet",
    "text_units.parquet",
    "relationships.parquet",
    "documents.parquet",
)

_MANIFEST_BLOB_NAME = "latest.json"


async def publish_manifest(snapshot_id: str) -> dict[str, Any] | None:
    """Write ``latest.json`` pointing at ``snapshot_id``.

    Args:
        snapshot_id: Snapshot identifier returned by
            ``run_indexing.run_graphrag_pipeline``. The bot will resolve
            the actual parquet location as
            ``<container>/snapshots/<snapshot_id>/<name>.parquet``.

    Returns:
        The manifest dict on success, or ``None`` if no destination
        container is configured (degrades to a logged no-op so the build
        job doesn't fail in dev/local environments).
    """
    container = os.environ.get("STORAGE_GRAPHRAG_OUTPUT_CONTAINER")
    if not container:
        logger.warning(
            "STORAGE_GRAPHRAG_OUTPUT_CONTAINER not set — skipping manifest publish"
        )
        return None

    # Shared with run_indexing so the manifest prefix and the build's
    # output prefix can never drift apart.
    snapshot_prefix = snapshot_base_dir(snapshot_id)
    manifest: dict[str, Any] = {
        "prefix": snapshot_prefix,
        "built_at": _dt.datetime.now(_dt.timezone.utc).isoformat(),
        "build_id": snapshot_id,
        "files": list(_PARQUET_FILES),
    }

    await asyncio.to_thread(_write_manifest, container, manifest)
    logger.info(
        "Published manifest for snapshot %s — the bot will pick it up on "
        "its next daily manifest poll",
        snapshot_id,
    )
    return manifest


def _write_manifest(container: str, manifest: dict[str, Any]) -> None:
    storage = BlobService(container_name=container)
    logger.info(
        "Publishing manifest %s/%s → prefix=%s",
        container,
        _MANIFEST_BLOB_NAME,
        manifest["prefix"],
    )
    storage.put_blob(_MANIFEST_BLOB_NAME, json.dumps(manifest, indent=2))
