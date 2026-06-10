#!/usr/bin/env python3
"""Main entry point for Azure SDK Knowledge Graph Sync.

The pipeline has two steps:

1. ``daily_sync.process_daily_sync_knowledge`` — refresh source docs in
   the knowledge container from upstream repos / files.
2. ``graphrag.run_indexing.run_graphrag_pipeline`` — full GraphRAG build
   that reads input docs *directly* from the knowledge container and
   writes parquets to a timestamped sub-prefix of the graphrag output
   container. ``publish_output.publish_and_notify`` then flips
   ``latest.json`` and pings the bot.

Usage:
    python -m src.main [--skip-graphrag] [--graphrag-only]
"""

from __future__ import annotations

import argparse
import asyncio
import logging
import sys

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s [%(name)s] %(message)s",
)
logger = logging.getLogger(__name__)


async def run(args: argparse.Namespace) -> None:
    """Run the knowledge sync pipeline."""
    from src.services.app_config import init_configuration
    from src.services.app_secret import init_secrets

    logger.info("Initializing app configuration...")
    await init_configuration()
    logger.info("Initializing app secrets...")
    await init_secrets()

    if not args.graphrag_only:
        from src.daily_sync import process_daily_sync_knowledge

        logger.info("Starting Azure SDK Knowledge Sync...")
        sync_result = await process_daily_sync_knowledge()
        logger.info(
            "Knowledge sync completed: %d changed, %d deleted",
            len(sync_result.changed_blob_paths),
            len(sync_result.deleted_blob_paths),
        )

    if not args.skip_graphrag:
        from src.graphrag.publish_output import publish_and_notify
        from src.graphrag.run_indexing import run_graphrag_pipeline

        logger.info("Starting GraphRAG indexing (full build, blob-direct)...")
        snapshot_id = await run_graphrag_pipeline()
        logger.info("GraphRAG indexing completed, snapshot=%s", snapshot_id)

        # Publishing failures must not fail the sync — the bot will pick
        # up the new build on next cold start as long as the manifest
        # eventually lands.
        try:
            manifest = await publish_and_notify(snapshot_id)
            if manifest:
                logger.info("Published GraphRAG snapshot %s", manifest.get("build_id"))
        except Exception:
            logger.warning("Failed to publish GraphRAG snapshot", exc_info=True)


def main() -> None:
    """CLI entry point."""
    parser = argparse.ArgumentParser(
        description="Azure SDK QA Bot Knowledge Sync Pipeline"
    )
    parser.add_argument(
        "--skip-graphrag",
        action="store_true",
        help="Skip GraphRAG indexing step",
    )
    parser.add_argument(
        "--graphrag-only",
        action="store_true",
        help="Run only GraphRAG indexing (skip document sync)",
    )
    args = parser.parse_args()

    try:
        asyncio.run(run(args))
    except Exception as e:
        logger.error("Knowledge sync failed: %s", e, exc_info=True)
        sys.exit(1)


if __name__ == "__main__":
    main()
