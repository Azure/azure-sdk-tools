"""Publish a freshly-built GraphRAG snapshot to the bot.

In the blob-direct architecture, ``build_index`` writes parquets to
``STORAGE_GRAPHRAG_OUTPUT_CONTAINER/snapshots/<snapshot_id>/`` directly.
This module therefore only needs to:

1. Write ``latest.json`` to the container root pointing at the new
   snapshot prefix. The manifest is written *last* (and is the bot's
   sole source of truth), so partially-built snapshots are never picked
   up.
2. POST ``BOT_AGENT_RELOAD_URL`` with an Entra ID bearer token so the
   live bot swaps in the new build without a restart. Best-effort —
   bot will pick the new snapshot up on its next cold start otherwise.

Required env vars:

* ``STORAGE_GRAPHRAG_OUTPUT_CONTAINER``  — destination container (same
  one the build wrote into).
* ``BOT_AGENT_RELOAD_URL``      *(optional)* — POST endpoint on the bot
  (e.g. ``https://<bot>/graph/admin/reload``).
* ``BOT_AGENT_AUDIENCE``        *(optional)* — Entra ID app/client ID
  fronting the bot via App Service EasyAuth. Used as the token scope
  (``<audience>/.default``). Required when ``BOT_AGENT_RELOAD_URL``
  is set.
"""

from __future__ import annotations

import asyncio
import datetime as _dt
import json
import logging
import os
from typing import Any

from src.services.storage_service import BlobService

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


async def publish_and_notify(snapshot_id: str) -> dict[str, Any] | None:
    """Write ``latest.json`` for ``snapshot_id`` and notify the bot.

    Args:
        snapshot_id: Snapshot identifier returned by
            ``run_indexing.run_graphrag_pipeline``. The bot will resolve
            the actual parquet location as
            ``<container>/snapshots/<snapshot_id>/<name>.parquet``.

    Returns:
        The manifest dict on success, or ``None`` if no destination
        container is configured (degrades to a logged no-op so the sync
        job doesn't fail in dev/local environments).
    """
    container = os.environ.get("STORAGE_GRAPHRAG_OUTPUT_CONTAINER")
    if not container:
        logger.warning(
            "STORAGE_GRAPHRAG_OUTPUT_CONTAINER not set — skipping manifest publish"
        )
        return None

    # ``prefix`` must match the per-run base_dir used by run_indexing's
    # output_storage override; keep them in sync.
    snapshot_prefix = f"snapshots/{snapshot_id}"
    manifest: dict[str, Any] = {
        "prefix": snapshot_prefix,
        "built_at": _dt.datetime.now(_dt.timezone.utc).isoformat(),
        "build_id": snapshot_id,
        "files": list(_PARQUET_FILES),
    }

    await asyncio.to_thread(_write_manifest, container, manifest)

    reload_url = os.environ.get("BOT_AGENT_RELOAD_URL", "").strip()
    if reload_url:
        await _notify_bot(reload_url, os.environ.get("BOT_AGENT_AUDIENCE", "").strip())
    else:
        logger.info(
            "BOT_AGENT_RELOAD_URL not set — bot will pick up snapshot %s on "
            "its next reload / restart",
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


async def _acquire_bearer_token(audience: str) -> str | None:
    """Get an Entra ID access token for the bot's EasyAuth audience.

    Returns ``None`` (with a logged warning) if no credential is
    available so the caller can skip the notify step gracefully.
    """
    try:
        from azure.identity import DefaultAzureCredential
    except ImportError:
        logger.warning("azure-identity not available — cannot acquire AAD token")
        return None

    def _get() -> str:
        credential = DefaultAzureCredential()
        try:
            return credential.get_token(f"{audience}/.default").token
        finally:
            credential.close()

    try:
        return await asyncio.to_thread(_get)
    except Exception as exc:
        logger.warning("Failed to acquire AAD token for audience=%s: %s", audience, exc)
        return None


async def _notify_bot(reload_url: str, audience: str) -> None:
    """POST the reload endpoint with an Entra ID bearer; never raise — log on failure."""
    if not audience:
        logger.warning(
            "BOT_AGENT_AUDIENCE not set — skipping reload POST to %s "
            "(cannot acquire AAD token without an audience)",
            reload_url,
        )
        return

    token = await _acquire_bearer_token(audience)
    if not token:
        return

    try:
        import httpx  # type: ignore[import-not-found]
    except ImportError:
        await asyncio.to_thread(_notify_bot_urllib, reload_url, token)
        return

    try:
        async with httpx.AsyncClient(timeout=30.0) as client:
            resp = await client.post(
                reload_url, headers={"Authorization": f"Bearer {token}"}
            )
        if resp.status_code >= 400:
            logger.warning(
                "Bot reload returned HTTP %s: %s",
                resp.status_code,
                resp.text[:500],
            )
        else:
            logger.info("Bot reload accepted: HTTP %s", resp.status_code)
    except Exception as exc:
        logger.warning("Bot reload POST failed: %s", exc)


def _notify_bot_urllib(reload_url: str, token: str) -> None:
    import urllib.error
    import urllib.request

    req = urllib.request.Request(
        reload_url, method="POST", headers={"Authorization": f"Bearer {token}"}
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            logger.info("Bot reload accepted: HTTP %s", resp.status)
    except urllib.error.HTTPError as exc:
        logger.warning("Bot reload returned HTTP %s: %s", exc.code, exc.reason)
    except Exception as exc:
        logger.warning("Bot reload POST failed: %s", exc)
