"""Publish GraphRAG parquet outputs to blob storage + notify the bot.

After ``run_graphrag_pipeline`` completes the parquets live in
``graphrag_config/output/``. This module uploads them to a versioned
prefix in blob storage (``<snapshot_id>/<name>.parquet`` at the
container root) and then writes ``latest.json`` last, so a
partially-published snapshot is never picked up by the bot.

After publishing it POSTs ``BOT_AGENT_RELOAD_URL`` with a shared-secret
header (``X-Admin-Token: BOT_AGENT_ADMIN_TOKEN``) so the live bot
swaps in the new build without restarting. The notify step is
best-effort — if the bot is unreachable we log a warning and let the
nightly process exit successfully (the bot will pick up the new build
on its next cold start or on the next reload).

Publishing always runs after a successful indexing pass; the only
required knob is the destination container. The remaining env vars are:

* ``STORAGE_GRAPHRAG_OUTPUT_CONTAINER``    — destination container
* ``BOT_AGENT_RELOAD_URL``                 — POST endpoint on the bot
* ``BOT_AGENT_ADMIN_TOKEN``                — shared secret
"""

from __future__ import annotations

import asyncio
import datetime as _dt
import json
import logging
import os
import uuid
from pathlib import Path
from typing import Any

from src.services.storage_service import BlobService

logger = logging.getLogger(__name__)

_PARQUET_FILES = (
    "entities.parquet",
    "communities.parquet",
    "community_reports.parquet",
    "text_units.parquet",
    "relationships.parquet",
    "documents.parquet",
)


def _build_snapshot_id() -> str:
    """Return a sortable, filesystem-safe timestamp for the snapshot prefix."""
    ts = _dt.datetime.now(_dt.timezone.utc).strftime("%Y-%m-%dT%H-%M-%SZ")
    short = uuid.uuid4().hex[:6]
    return f"{ts}-{short}"


async def publish_and_notify(output_dir: Path) -> dict[str, Any] | None:
    """Publish parquets from ``output_dir`` and notify the bot agent.

    Returns the manifest dict on success, or ``None`` when no destination
    container is configured (so the step degrades to a logged no-op
    rather than failing the sync).

    The function offloads sync SDK calls to ``asyncio.to_thread`` so it
    doesn't block the event loop, but the underlying ``BlobService``
    (and azure-storage-blob) remains synchronous, consistent with the
    rest of the sync project.
    """
    container = os.environ.get("STORAGE_GRAPHRAG_OUTPUT_CONTAINER")
    if not container:
        logger.warning(
            "STORAGE_GRAPHRAG_OUTPUT_CONTAINER not set — skipping parquet publish"
        )
        return None

    snapshot_id = _build_snapshot_id()
    manifest = {
        "prefix": snapshot_id,
        "built_at": _dt.datetime.now(_dt.timezone.utc).isoformat(),
        "build_id": snapshot_id,
        "files": list(_PARQUET_FILES),
    }

    await asyncio.to_thread(_upload_snapshot, container, snapshot_id, output_dir, manifest)

    reload_url = os.environ.get("BOT_AGENT_RELOAD_URL", "").strip()
    if reload_url:
        await _notify_bot(reload_url, os.environ.get("BOT_AGENT_ADMIN_TOKEN", ""))
    else:
        logger.info(
            "BOT_AGENT_RELOAD_URL not set — bot will pick up the new snapshot "
            "on its next reload / restart"
        )

    return manifest


def _upload_snapshot(
    container: str,
    snapshot_id: str,
    output_dir: Path,
    manifest: dict[str, Any],
) -> None:
    """Upload all parquets + manifest (manifest LAST for atomicity)."""
    storage = BlobService(container_name=container)

    missing: list[str] = []
    for name in _PARQUET_FILES:
        src = output_dir / name
        if not src.is_file():
            missing.append(name)
            continue
        blob_name = f"{snapshot_id}/{name}"
        logger.info("Uploading %s -> %s/%s", src, container, blob_name)
        storage.put_blob(blob_name, src.read_bytes())

    if missing:
        raise FileNotFoundError(
            f"GraphRAG output is incomplete; missing parquets: {missing}"
        )

    # Manifest written LAST — readers polling latest.json never see a
    # half-uploaded snapshot.
    logger.info("Publishing manifest %s/latest.json", container)
    storage.put_blob("latest.json", json.dumps(manifest, indent=2))


async def _notify_bot(reload_url: str, admin_token: str) -> None:
    """POST the reload endpoint; never raise — log on failure."""
    if not admin_token:
        logger.warning(
            "BOT_AGENT_ADMIN_TOKEN not set — skipping reload POST to %s",
            reload_url,
        )
        return

    try:
        import httpx  # type: ignore[import-not-found]
    except ImportError:
        # Fallback to stdlib urllib so we don't add a hard runtime dep
        await asyncio.to_thread(_notify_bot_urllib, reload_url, admin_token)
        return

    try:
        async with httpx.AsyncClient(timeout=30.0) as client:
            resp = await client.post(
                reload_url, headers={"X-Admin-Token": admin_token}
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


def _notify_bot_urllib(reload_url: str, admin_token: str) -> None:
    import urllib.error
    import urllib.request

    req = urllib.request.Request(
        reload_url, method="POST", headers={"X-Admin-Token": admin_token}
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            logger.info("Bot reload accepted: HTTP %s", resp.status)
    except urllib.error.HTTPError as exc:
        logger.warning("Bot reload returned HTTP %s: %s", exc.code, exc.reason)
    except Exception as exc:
        logger.warning("Bot reload POST failed: %s", exc)
