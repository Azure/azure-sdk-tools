"""GraphRAG indexing pipeline — using GraphRAG Python API directly.

Drives a complete GraphRAG build directly against Azure Blob Storage:

* **input**  — read from the knowledge container (``STORAGE_KNOWLEDGE_CONTAINER``)
  using GraphRAG's native ``azure_blob`` input storage. No local INPUT_DIR.
* **output** — written to ``STORAGE_GRAPHRAG_OUTPUT_CONTAINER/snapshots/<UTC ts>/``
  via GraphRAG's native ``azure_blob`` output storage. Each run gets a
  fresh sub-prefix so old snapshots are preserved until the manifest is
  rotated.
* **cache**  — shared ``STORAGE_GRAPHRAG_OUTPUT_CONTAINER/cache/`` so LLM
  extraction results survive across runs.
* **vector store** — Azure AI Search (configured in ``settings.yaml``).

The single public entry point is :func:`run_graphrag_pipeline`, which
returns the snapshot id (the timestamped sub-prefix) so callers can pass
it to ``publish_output.publish_manifest`` to flip ``latest.json``.
"""

from __future__ import annotations

import datetime as _dt
import logging
import uuid
from pathlib import Path

from graphrag.api.index import build_index
from graphrag.config.enums import IndexingMethod
from graphrag.config.load_config import load_config

from . import snapshot_base_dir
from .source_aware_reader import register_source_aware_input_reader

logger = logging.getLogger(__name__)

# The graphrag config (settings.yaml) lives in graphrag_config/ at project root.
GRAPHRAG_ROOT = Path(__file__).resolve().parent.parent.parent / "graphrag_config"


def _new_snapshot_id() -> str:
    """Return a sortable, filesystem-safe per-run snapshot identifier."""
    ts = _dt.datetime.now(_dt.timezone.utc).strftime("%Y-%m-%dT%H-%M-%SZ")
    short = uuid.uuid4().hex[:6]
    return f"{ts}-{short}"


async def run_graphrag_pipeline() -> str:
    """Run a full GraphRAG build into a fresh timestamped blob snapshot.

    Returns:
        The snapshot id (``"<UTC ts>-<short>"``) that callers should pass
        to ``publish_output.publish_manifest`` to update ``latest.json``.
    """
    snapshot_id = _new_snapshot_id()
    output_base_dir = snapshot_base_dir(snapshot_id)

    # Override GraphRAG's default markitdown reader with our source-aware
    # variant so every documents.parquet row carries
    # raw_data['source_folder'] and raw_data['source_path']. The agent
    # reads those to attribute each graph reference to its KnowledgeSource
    # and resolve its link — see utils/knowledge_graph.py in the bot.
    register_source_aware_input_reader()

    logger.info(
        "Starting GraphRAG full build → snapshot %s (output_base_dir=%s)",
        snapshot_id,
        output_base_dir,
    )

    # Inject the per-run snapshot prefix into the loaded GraphRagConfig.
    # ``load_config`` merges this override recursively into the parsed
    # settings.yaml dict before instantiating the model.
    config = load_config(
        GRAPHRAG_ROOT,
        cli_overrides={"output_storage": {"base_dir": output_base_dir}},
    )

    results = await build_index(
        config=config,
        method=IndexingMethod.Standard,
        is_update_run=False,
        verbose=True,
    )

    errors = [r for r in results if r.error is not None]
    if errors:
        error_msgs = [f"{r.workflow}: {r.error}" for r in errors]
        logger.error(
            "GraphRAG build had %d workflow error(s):\n%s",
            len(errors),
            "\n".join(error_msgs),
        )
        raise RuntimeError(
            f"GraphRAG build failed with {len(errors)} workflow error(s): "
            + "; ".join(error_msgs[:3])
        )

    logger.info(
        "GraphRAG full build complete: snapshot=%s, workflows=%d",
        snapshot_id,
        len(results),
    )
    return snapshot_id
