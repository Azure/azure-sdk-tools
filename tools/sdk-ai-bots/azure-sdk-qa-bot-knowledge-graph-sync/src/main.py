#!/usr/bin/env python3
"""Main entry point for Azure SDK Knowledge Sync.

Usage:
    python -m src.main [--skip-graphrag] [--graphrag-only] [--sources SRC1,SRC2]
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

    # Step 1: Initialize configuration and secrets
    logger.info("Initializing app configuration...")
    await init_configuration()
    logger.info("Initializing app secrets...")
    await init_secrets()

    # Step 2: Run main knowledge sync (unless --graphrag-only)
    changed_blob_paths: list[str] = []
    deleted_blob_paths: list[str] = []

    if not args.graphrag_only:
        from src.daily_sync import process_daily_sync_knowledge

        logger.info("Starting Azure SDK Knowledge Sync...")
        sync_result = await process_daily_sync_knowledge()
        changed_blob_paths = sync_result.changed_blob_paths
        deleted_blob_paths = sync_result.deleted_blob_paths
        logger.info(
            "Knowledge sync completed: %d changed, %d deleted",
            len(changed_blob_paths),
            len(deleted_blob_paths),
        )

    # Step 3: Run GraphRAG indexing (unless --skip-graphrag)
    if not args.skip_graphrag:
        from src.graphrag.run_indexing import OUTPUT_DIR, run_graphrag_pipeline

        sources = [s.strip() for s in args.sources.split(",")] if args.sources else None
        logger.info("Starting GraphRAG indexing...")
        await run_graphrag_pipeline(
            sources=sources,
            full=args.full_graphrag,
            changed_blob_paths=changed_blob_paths,
            deleted_blob_paths=deleted_blob_paths,
        )
        logger.info("GraphRAG indexing completed successfully")

        # Step 4: Publish parquets to blob + notify bot agent. Runs after
        # every successful indexing pass; degrades to a logged no-op
        # when STORAGE_GRAPHRAG_OUTPUT_CONTAINER is unset. Failures here
        # don't fail the sync — the bot will pick up the new build on
        # next cold start.
        from src.graphrag.publish_output import publish_and_notify

        try:
            manifest = await publish_and_notify(OUTPUT_DIR)
            if manifest:
                logger.info(
                    "Published GraphRAG snapshot %s", manifest.get("build_id")
                )
        except Exception:
            logger.warning(
                "Failed to publish GraphRAG output to blob", exc_info=True
            )


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
    parser.add_argument(
        "--sources",
        type=str,
        default=None,
        help="Comma-separated list of sources to process",
    )
    parser.add_argument(
        "--full-graphrag",
        action="store_true",
        help="Run GraphRAG on all sources (expensive)",
    )
    args = parser.parse_args()

    try:
        asyncio.run(run(args))
    except Exception as e:
        logger.error("Knowledge sync failed: %s", e, exc_info=True)
        sys.exit(1)


if __name__ == "__main__":
    main()
