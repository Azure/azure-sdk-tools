"""GraphRAG indexing sub-package."""

# Sub-prefix under the output container that holds individual snapshots.
# Keeping snapshots under a stable parent prefix lets the cache live at
# ``<container>/cache/`` without colliding with snapshot data. Shared by
# run_indexing (build output) and publish_output (manifest prefix) so the
# two can never drift apart.
_SNAPSHOT_PARENT_PREFIX = "snapshots"


def snapshot_base_dir(snapshot_id: str) -> str:
    """Compose the output-container base_dir for a snapshot."""
    return f"{_SNAPSHOT_PARENT_PREFIX}/{snapshot_id}"
