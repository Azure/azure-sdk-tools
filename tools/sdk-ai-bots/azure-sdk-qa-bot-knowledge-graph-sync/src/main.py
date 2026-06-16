#!/usr/bin/env python3
"""Main entry point for Azure SDK Knowledge Graph Sync.

This project owns a single responsibility: run a **full GraphRAG build**
over the knowledge container and publish the resulting index artefacts
for the QA bot's graph-retrieval tool.

Document collection / normalisation is **not** done here — that is the
``azure-sdk-qa-bot-knowledge-sync`` project's job. This pipeline reads
the docs that project already maintains in the knowledge container
directly from blob storage.

The build:

1. ``graphrag.run_indexing.run_graphrag_pipeline`` — full GraphRAG build
   that reads input docs directly from the knowledge container and writes
   parquets to a timestamped sub-prefix of the graphrag output container.
2. ``graphrag.publish_output.publish_and_notify`` — flips ``latest.json``
   to the new snapshot and pings the bot to reload.

Usage:
    python -m src.main
"""

from __future__ import annotations

import asyncio
import logging
import sys

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s [%(name)s] %(message)s",
)
logger = logging.getLogger(__name__)


async def run() -> None:
    """Run a full GraphRAG build and publish the resulting snapshot."""
    from src.services.app_config import init_configuration
    from src.services.app_secret import init_secrets

    logger.info("Initializing app configuration...")
    await init_configuration()
    logger.info("Initializing app secrets...")
    await init_secrets()

    from src.graphrag.publish_output import publish_and_notify
    from src.graphrag.run_indexing import run_graphrag_pipeline

    logger.info("Starting GraphRAG indexing (full build, blob-direct)...")
    snapshot_id = await run_graphrag_pipeline()
    logger.info("GraphRAG indexing completed, snapshot=%s", snapshot_id)

    # Publishing failures must not fail the build — the bot will pick up
    # the new snapshot on next cold start as long as the manifest
    # eventually lands.
    try:
        manifest = await publish_and_notify(snapshot_id)
        if manifest:
            logger.info("Published GraphRAG snapshot %s", manifest.get("build_id"))
    except Exception:
        logger.warning("Failed to publish GraphRAG snapshot", exc_info=True)


def main() -> None:
    """CLI entry point."""
    try:
        asyncio.run(run())
    except Exception as e:
        logger.error("Knowledge graph sync failed: %s", e, exc_info=True)
        sys.exit(1)


if __name__ == "__main__":
    main()
