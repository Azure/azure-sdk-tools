"""Graph-grounded retrieval service backed by GraphRAG Local Search.

A process-wide singleton (see :func:`get_knowledge_graph_service`) that
owns the loaded snapshot and exposes a single public method,
:meth:`KnowledgeGraphService.search_graph`. Per query it runs only the
**context-building** half of GraphRAG's Local Search pipeline — embed the
query, ANN-match entities, expand one hop, resolve back to source text
units — with **no completion LLM call**. The chat agent synthesises the
final answer itself from the verbatim snippets returned.

Heavy lifting is delegated to focused modules:

* :mod:`loading`    — settings.yaml + manifest + parquet downloads.
* :mod:`engine`     — community-embedding preload + context-builder build.
* :mod:`filtering`  — tenant-scoped entity filtering (folder + file level).
* :mod:`extraction` — context_records → :class:`Reference` resolution.
"""

from __future__ import annotations

import asyncio
import logging
from typing import Any

from config.app_config import get as cfg
from models.knowledge import Reference
from utils.knowledge_graph import engine, extraction, filtering, loading

logger = logging.getLogger(__name__)

_DEFAULT_COMMUNITY_LEVEL = 2


class KnowledgeGraphService:
    """Graph-grounded retrieval over a GraphRAG snapshot.

    DataFrame loading is lazy on first use and cached for the lifetime of
    the process; call :meth:`reload` (or restart the bot) to pick up a newly
    published snapshot. ``build_context`` is stateless across calls, so the
    cached context builder is shared by concurrent queries without locking.
    """

    def __init__(self) -> None:
        self._community_level = int(
            cfg("GRAPH_COMMUNITY_LEVEL", str(_DEFAULT_COMMUNITY_LEVEL))
        )
        # Blob container holding the parquet snapshots + ``latest.json``.
        self._blob_container = cfg("STORAGE_GRAPHRAG_OUTPUT_CONTAINER", "")

        self._config: Any = None
        self._dfs: "dict[str, Any] | None" = None
        self._loaded_version: dict[str, Any] | None = None
        self._report_embeddings_cache: dict[str, list[float]] = {}
        self._context_builder: Any = None
        self._context_params: dict[str, Any] = {}
        # ``{source_folder: {entity_id: frozenset(source_path)}}`` reverse
        # index used to scope retrieval per tenant. Empty disables filtering.
        self._entity_index: "dict[str, dict[str, frozenset[str]]]" = {}
        self._load_lock = asyncio.Lock()

        self._enabled = bool(self._blob_container)
        if not self._enabled:
            logger.info(
                "KnowledgeGraphService inactive: "
                "STORAGE_GRAPHRAG_OUTPUT_CONTAINER is not set."
            )
        else:
            logger.info(
                "KnowledgeGraphService ready: community_level=%d, blob_container=%s",
                self._community_level,
                self._blob_container,
            )

    @property
    def enabled(self) -> bool:
        return self._enabled

    # ------------------------------------------------------------------ #
    # Public search API
    # ------------------------------------------------------------------ #

    async def search_graph(
        self,
        query: str,
        allowed_source_folders: "set[str] | None" = None,
        source_path_filters: "dict[str, list[str]] | None" = None,
    ) -> "list[Reference] | None":
        """Retrieve graph-grounded source references for a query.

        Args:
            query: The user's question.
            allowed_source_folders: Optional set of ``KnowledgeSource.name``
                values. When provided and at least one is known to the
                snapshot's reverse index, entity-ANN hits are scoped to
                entities whose source documents belong to those folders
                (the source-folder filter layer). ``None``/empty keeps the
                unscoped behaviour.
            source_path_filters: Optional ``{folder: [terms]}`` mapping. When
                a folder has terms, its entities are further restricted to
                those whose document ``source_path`` contains one of the
                terms (the file filter layer, mirroring the KB tool's
                per-source title filter).

        Returns:
            A list of :class:`Reference` (one per unique cited document),
            an empty list when nothing matched, or ``None`` when the service
            is disabled or the context build failed.
        """
        if not self._enabled:
            logger.debug("GraphRAG search_graph short-circuit: service is disabled")
            return None
        if not await self._ensure_loaded():
            logger.warning(
                "GraphRAG search_graph short-circuit: _ensure_loaded() returned False"
            )
            return None
        if self._context_builder is None:
            # Half-loaded state (parquets present but builder missing) —
            # clear and force a rebuild on the next request.
            logger.warning(
                "GraphRAG search_graph: context builder missing — clearing "
                "state and forcing a rebuild on the next request"
            )
            async with self._load_lock:
                self._reset_state()
            return None

        allowed_entity_ids = filtering.resolve_allowed_entity_ids(
            self._entity_index, allowed_source_folders, source_path_filters
        )
        token = filtering.allowed_entity_ids_var().set(allowed_entity_ids)
        try:
            result = await asyncio.to_thread(
                self._context_builder.build_context,
                query=query,
                **self._context_params,
            )
        except Exception:
            logger.warning("GraphRAG search_graph failed", exc_info=True)
            return None
        finally:
            filtering.allowed_entity_ids_var().reset(token)

        return extraction.extract_references_from_context(
            self._dfs,
            result.context_records,
            expand_communities=(cfg("GRAPH_LS_EXPAND_COMMUNITIES", "true").lower() == "true"),
            max_expansion_units=int(cfg("GRAPH_LS_EXPANSION_UNITS", "40")),
        )

    # ------------------------------------------------------------------ #
    # Loading / reloading
    # ------------------------------------------------------------------ #

    async def reload(self) -> dict[str, Any]:
        """Atomically reload the graph from the configured blob source.

        On success the new config + DataFrames + context builder go live.
        On failure the prior state is restored exactly, so a broken reload
        never leaves the service half-loaded.
        """
        if not self._enabled:
            raise RuntimeError(
                "KnowledgeGraphService is disabled: no parquet source configured."
            )

        async with self._load_lock:
            prev_state = self._snapshot_state()
            try:
                await self._load_and_build()
            except Exception:
                logger.exception(
                    "GraphRAG reload failed; restoring previous service state"
                )
                self._restore_state(prev_state)
                raise

        logger.info(
            "KnowledgeGraphService reloaded: version=%s row_counts=%s",
            self._loaded_version,
            {name: len(df) for name, df in (self._dfs or {}).items()},
        )
        return self._status()

    async def reload_if_changed(self) -> dict[str, Any] | None:
        """Reload only when ``latest.json`` points to a new build.

        Cheap poll used by the bot's daily scheduler. Returns the new status
        dict when a reload happened, or ``None`` when already current (or
        disabled). Never raises — a transient read failure is treated as "no
        change" so the scheduler keeps serving the existing snapshot.
        """
        if not self._enabled:
            return None
        try:
            manifest = await self._load_manifest()
        except Exception:
            logger.exception("GraphRAG manifest poll failed; keeping current snapshot")
            return None

        new_build = manifest.get("build_id") if manifest else None
        current_manifest = (self._loaded_version or {}).get("manifest") or {}
        current_build = current_manifest.get("build_id")
        already_loaded = self._loaded_version is not None

        if already_loaded and new_build is not None and new_build == current_build:
            logger.info(
                "GraphRAG manifest unchanged (build_id=%s); skipping reload",
                new_build,
            )
            return None

        logger.info(
            "GraphRAG manifest changed (build_id %s -> %s); reloading",
            current_build,
            new_build,
        )
        return await self.reload()

    async def _ensure_loaded(self) -> bool:
        """Lazily load config + parquets + builder on first use."""
        if self._dfs is not None and self._config is not None:
            return True

        async with self._load_lock:
            if self._dfs is not None and self._config is not None:
                return True
            try:
                await self._load_and_build()
            except Exception:
                logger.exception("Failed to initialise KnowledgeGraphService")
                self._reset_state()
                return False

        logger.info(
            "KnowledgeGraphService loaded: %s",
            {name: len(df) for name, df in (self._dfs or {}).items()},
        )
        return True

    async def _load_and_build(self) -> None:
        """Load config + parquets and (re)build all derived query state.

        Caller must hold ``self._load_lock``. On any failure the partially
        mutated state is the caller's responsibility to restore/reset.
        """
        new_config = await asyncio.to_thread(loading.load_config)
        new_dfs, new_version = await self._load_parquets_with_manifest()
        self._config = new_config
        self._dfs = new_dfs
        self._loaded_version = new_version
        self._report_embeddings_cache = await engine.preload_report_embeddings(
            new_config
        )
        self._context_builder, self._context_params = await asyncio.to_thread(
            engine.build_context_builder,
            new_config,
            new_dfs,
            self._community_level,
            self._report_embeddings_cache,
        )
        if self._context_builder is None:
            raise RuntimeError(
                "GraphRAG context builder produced no instance — "
                "refusing to swap to a half-built state"
            )
        self._entity_index = filtering.build_entity_index(new_dfs)

    async def _load_parquets_with_manifest(
        self,
    ) -> "tuple[dict[str, Any], dict[str, Any]]":
        """Load parquets and return them alongside version metadata."""
        if not self._blob_container:
            raise RuntimeError(
                "STORAGE_GRAPHRAG_OUTPUT_CONTAINER is not configured; "
                "cannot load GraphRAG parquet artefacts."
            )
        manifest = await self._load_manifest()
        prefix = loading.snapshot_prefix(manifest)
        dfs = await loading.load_parquets_from_blob(self._blob_container, prefix)
        version: dict[str, Any] = {
            "source": "blob",
            "container": self._blob_container,
            "snapshot": prefix,
        }
        if manifest:
            version["manifest"] = manifest
        return dfs, version

    async def _load_manifest(self) -> dict[str, Any] | None:
        """Read ``latest.json`` from the blob container root, if present."""
        return await loading.load_manifest(self._blob_container)

    # ------------------------------------------------------------------ #
    # State management
    # ------------------------------------------------------------------ #

    def _reset_state(self) -> None:
        """Clear all loaded/derived state (caller holds the lock)."""
        self._config = None
        self._dfs = None
        self._loaded_version = None
        self._report_embeddings_cache = {}
        self._context_builder = None
        self._context_params = {}
        self._entity_index = {}

    def _snapshot_state(self) -> tuple:
        return (
            self._config,
            self._dfs,
            self._loaded_version,
            self._report_embeddings_cache,
            self._context_builder,
            self._context_params,
            self._entity_index,
        )

    def _restore_state(self, state: tuple) -> None:
        (
            self._config,
            self._dfs,
            self._loaded_version,
            self._report_embeddings_cache,
            self._context_builder,
            self._context_params,
            self._entity_index,
        ) = state

    def _status(self) -> dict[str, Any]:
        """Minimal load-state summary used for startup logging."""
        loaded = (
            self._dfs is not None
            and self._config is not None
            and self._context_builder is not None
        )
        status: dict[str, Any] = {"enabled": self._enabled, "loaded": loaded}
        if self._loaded_version is not None:
            status["version"] = dict(self._loaded_version)
        if self._dfs is not None:
            status["row_counts"] = {
                name: int(len(df)) for name, df in self._dfs.items()
            }
        return status


# --------------------------------------------------------------------------- #
# Singleton
# --------------------------------------------------------------------------- #

_service: KnowledgeGraphService | None = None


def get_knowledge_graph_service() -> KnowledgeGraphService:
    """Return the shared KnowledgeGraphService instance."""
    global _service
    if _service is None:
        _service = KnowledgeGraphService()
    return _service
