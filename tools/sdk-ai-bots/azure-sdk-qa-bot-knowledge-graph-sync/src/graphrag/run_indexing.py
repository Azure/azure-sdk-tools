"""GraphRAG indexing pipeline — using GraphRAG Python API directly.

GraphRAG natively supports:
- Azure AI Search as vector store (configured in settings.yaml)
- Incremental indexing via `build_index(is_update_run=True)`
- Full indexing via `build_index(is_update_run=False)`

This module orchestrates:
1. Downloading source documents to the GraphRAG input directory
2. Calling GraphRAG's Python API for indexing (no subprocess/CLI)

GraphRAG handles all vector indexing (Azure AI Search) and graph
extraction internally — no custom Cosmos upload or embedding needed.
"""

from __future__ import annotations

import logging
import os
import shutil
from pathlib import Path

from src.services.configuration_loader import ConfigurationLoader
from src.services.storage_service import BlobService

from graphrag.api.index import build_index
from graphrag.config.enums import IndexingMethod
from graphrag.config.load_config import load_config

logger = logging.getLogger(__name__)

# The graphrag config lives in the graphrag_config/ directory at project root
GRAPHRAG_ROOT = Path(__file__).resolve().parent.parent.parent / "graphrag_config"
INPUT_DIR = GRAPHRAG_ROOT / "input"
OUTPUT_DIR = GRAPHRAG_ROOT / "output"
UPDATE_OUTPUT_DIR = GRAPHRAG_ROOT / "update_output"


def _resolve_source_prefixes() -> list[str]:
    """Return the source-folder prefixes to index from knowledge-config.json.

    Mirrors how `daily_sync` derives blob paths: each `DocumentationSource.folder`
    is the prefix used when uploading blobs (`source.folder/...`), so the full
    rebuild downloads exactly the same set the doc sync produced.

    Returns folders in config order, de-duplicated.
    """
    seen: set[str] = set()
    folders: list[str] = []
    for source in ConfigurationLoader.get_documentation_sources():
        if source.folder and source.folder not in seen:
            seen.add(source.folder)
            folders.append(source.folder)
    return folders


async def run_graphrag_pipeline(
    sources: list[str] | None = None,
    full: bool = False,
    changed_blob_paths: list[str] | None = None,
    deleted_blob_paths: list[str] | None = None,
) -> None:
    """Run the GraphRAG indexing pipeline.

    Uses GraphRAG's Python API for indexing:
    - `build_index(is_update_run=False)` for full re-indexing
    - `build_index(is_update_run=True)` for incremental updates

    GraphRAG writes vectors directly to Azure AI Search (configured in
    settings.yaml) and stores graph structure in parquet output files.

    Args:
        sources: Source prefixes to index (None = derive from knowledge-config.json). Used in full mode.
        full: If True, re-index everything from scratch.
        changed_blob_paths: Blob paths that changed in the current sync.
        deleted_blob_paths: Blob paths that were deleted in the current sync.
    """
    changed = changed_blob_paths or []
    deleted = deleted_blob_paths or []

    if full:
        logger.info("Full GraphRAG indexing mode")
        await _run_full_indexing(sources)
    elif changed or deleted:
        logger.info(
            "Incremental GraphRAG update: %d changed, %d deleted",
            len(changed),
            len(deleted),
        )
        await _run_incremental_indexing(changed, deleted, sources)
    else:
        logger.info("No changes detected — skipping GraphRAG indexing")


async def _run_incremental_indexing(
    changed_blob_paths: list[str],
    deleted_blob_paths: list[str],
    sources: list[str] | None = None,
) -> None:
    """Incremental indexing using GraphRAG's native update API.

    GraphRAG's update mode:
    - Detects new/modified files in the input directory
    - Only processes changed documents
    - Merges new entities into the existing graph
    - Updates vector store indexes in Azure AI Search
    """
    # Ensure we have an existing index output (required for update)
    if not OUTPUT_DIR.exists() or not any(OUTPUT_DIR.rglob("*.parquet")):
        logger.info("No existing index found — falling back to full indexing")
        await _run_full_indexing(sources)
        return

    # Remove deleted files from input directory
    if deleted_blob_paths:
        _remove_deleted_from_input(deleted_blob_paths)

    # Download changed blobs into the input directory (additive, not destructive)
    if changed_blob_paths:
        blob_service = BlobService()
        count = _download_blobs_additive(blob_service, changed_blob_paths)
        if count == 0:
            logger.warning("Could not download any changed blobs")
            return

    # Run GraphRAG update (native incremental via Python API)
    await _run_graphrag_build_index(is_update_run=True)

    # Merge update_output back into output for next run
    _merge_update_output()

    logger.info("Incremental GraphRAG update done")


async def _run_full_indexing(sources: list[str] | None = None) -> None:
    """Full re-indexing: download all source blobs and rebuild the graph."""
    src_list = sources or _resolve_source_prefixes()
    if not src_list:
        logger.warning(
            "No source folders resolved from knowledge-config.json — skipping indexing"
        )
        return

    blob_service = BlobService()
    count = blob_service.download_all_blobs_to_dir(INPUT_DIR, source_prefixes=src_list)
    if count == 0:
        logger.warning("No documents downloaded — skipping indexing")
        return

    # Run full GraphRAG index via Python API
    await _run_graphrag_build_index(is_update_run=False)

    logger.info("Full GraphRAG indexing done")


# =============================================================================
# GraphRAG Python API execution
# =============================================================================


async def _run_graphrag_build_index(is_update_run: bool = False) -> None:
    """Run GraphRAG indexing using the Python API directly.

    Args:
        is_update_run: If True, runs incremental update. If False, full index.
    """

    mode = "update" if is_update_run else "full index"
    logger.info("Starting GraphRAG %s via Python API...", mode)

    # Load config from settings.yaml
    config = load_config(GRAPHRAG_ROOT)

    # Run the indexing pipeline
    results = await build_index(
        config=config,
        method=IndexingMethod.Standard,
        is_update_run=is_update_run,
        verbose=True,
    )

    # Check for errors
    errors = [r for r in results if r.error is not None]
    if errors:
        error_msgs = [f"{r.workflow}: {r.error}" for r in errors]
        logger.error("GraphRAG %s had errors:\n%s", mode, "\n".join(error_msgs))
        raise RuntimeError(
            f"GraphRAG {mode} failed with {len(errors)} workflow error(s): "
            + "; ".join(error_msgs[:3])
        )

    logger.info(
        "GraphRAG %s completed: %d workflows succeeded",
        mode,
        len(results),
    )


# =============================================================================
# File management helpers
# =============================================================================


def _download_blobs_additive(blob_service: BlobService, blob_paths: list[str]) -> int:
    """Download blobs into the input directory without clearing existing files.

    Unlike download_blobs_to_dir (which clears the target), this preserves
    existing files so GraphRAG update can compare against prior state.
    """
    INPUT_DIR.mkdir(parents=True, exist_ok=True)
    count = 0
    for blob_path in blob_paths:
        try:
            data = blob_service.download_blob(blob_path)
            local_path = INPUT_DIR / blob_path
            local_path.parent.mkdir(parents=True, exist_ok=True)
            local_path.write_bytes(data)
            count += 1
        except Exception as e:
            logger.warning("Failed to download blob %s: %s", blob_path, e)

    logger.info("Downloaded %d/%d changed blobs to input/", count, len(blob_paths))
    return count


def _remove_deleted_from_input(deleted_blob_paths: list[str]) -> None:
    """Remove deleted documents from the input directory."""
    removed = 0
    for blob_path in deleted_blob_paths:
        local_path = INPUT_DIR / blob_path
        if local_path.exists():
            local_path.unlink()
            removed += 1
            # Clean up empty parent directories
            parent = local_path.parent
            if parent != INPUT_DIR and not any(parent.iterdir()):
                parent.rmdir()

    logger.info(
        "Removed %d/%d deleted files from input/", removed, len(deleted_blob_paths)
    )


def _merge_update_output() -> None:
    """Merge update_output into output directory for subsequent updates.

    GraphRAG's update mode writes to update_output/ by default.
    We merge these results back into output/ so the next run can use them.
    """
    if not UPDATE_OUTPUT_DIR.exists():
        return

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    for src_file in UPDATE_OUTPUT_DIR.rglob("*"):
        if src_file.is_file():
            rel = src_file.relative_to(UPDATE_OUTPUT_DIR)
            dst = OUTPUT_DIR / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src_file, dst)

    # Clean up update_output for next run
    shutil.rmtree(UPDATE_OUTPUT_DIR)
    logger.info("Merged update_output into output")
