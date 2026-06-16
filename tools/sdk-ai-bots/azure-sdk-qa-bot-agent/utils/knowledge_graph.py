"""Knowledge graph retrieval service using Microsoft GraphRAG.

Wraps GraphRAG's Local Search **context builder** against the indexes
and graph artifacts produced by the
``azure-sdk-qa-bot-knowledge-graph-sync`` project. Exposes a single
public method, :meth:`KnowledgeGraphService.search_graph`, that returns
the graph-grounded source documents most relevant to a query.

Data sources at query time
--------------------------
GraphRAG splits its query-time inputs into two stores:

* **Azure AI Search** — *all vector data.* The mixed context builder
  embeds the query and runs an ANN search against the entity index.
* **Parquet artifacts** — *graph structure only.* ``entities`` /
  ``communities`` / ``community_reports`` / ``text_units`` /
  ``relationships`` / ``documents`` parquets carry IDs, descriptions,
  edges, community hierarchy, and source text — but no vectors. The
  builder needs them to resolve entity hits back through the graph and
  pull the text units (and their parent documents) we ultimately cite.
"""

from __future__ import annotations

import asyncio
import contextvars
import logging
import re
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING, Any

from config.app_config import get as cfg
from config.tenant_config import get_knowledge_source
from utils.azure_storage import download_blob

if TYPE_CHECKING:
    import pandas as pd

logger = logging.getLogger(__name__)

_DEFAULT_COMMUNITY_LEVEL = 2

# Oversample factor applied when a tenant filter is active: we ask the
# entity vectorstore for ``k * factor`` ANN hits and keep the first ``k``
# that belong to the allowed source folders. 8x comfortably covers
# tenants that own ~12% of the entity universe (typical: ``typespec``
# is ~30%, ``python``/``api_spec_review`` ~5-10%) without paying
# noticeable extra latency — the underlying ANN call is the same cost
# regardless of ``k`` once ``k`` exceeds the index's segment cache.
_FILTER_OVERSAMPLE = 8

# Per-asyncio-task context variable carrying the set of entity ids the
# current ``build_context`` call is allowed to surface. Used by
# :class:`_SourceFilteredVectorStore` to scope ANN hits without
# threading a new parameter through every layer of GraphRAG's context
# builder. ``None`` (default) disables filtering — used both for
# legacy callers that pass no tenant_id and for tenants whose
# ``KnowledgeSource`` list is empty.
_ALLOWED_ENTITY_IDS: contextvars.ContextVar[frozenset[str] | None] = (
    contextvars.ContextVar("graphrag_allowed_entity_ids", default=None)
)

# Parquet artefacts produced by the GraphRAG indexing pipeline that we
# need to drive query operations. ``documents`` is required so we can
# map text_units back to their original source-document filename / path
# when building citations.
_REQUIRED_PARQUETS: tuple[str, ...] = (
    "entities",
    "communities",
    "community_reports",
    "text_units",
    "relationships",
    "documents",
)


@dataclass(frozen=True)
class GraphSourceRef:
    """A source-document reference cited by a GraphRAG local search.

    Attributes:
        title:   Human-readable reference title. Prefers a section-level
                 ``h1 | h2 | h3`` heading path parsed from the cited chunk
                 (matching the KB tool's header-based titles); falls back
                 to the document path (``/`` separators) when the chunk
                 carries no heading.
        link:    Best-effort link to the original document. Empty string
                 when no URL can be derived without tenant context.
        content: A representative excerpt of the source text used to
                 ground the LLM answer (typically one text_unit chunk).
        source:  Short identifier for the originating KnowledgeSource
                 (e.g. ``"typespec_docs"``, matching what the KB tool
                 attaches to a chunk from the same document). Recovered
                 from the ``raw_data.source_folder`` column that the
                 sync project's ``SourceAwareMarkItDownFileReader``
                 writes into ``documents.parquet`` per row. Falls back
                 to ``"graphrag"`` only when a snapshot row is missing
                 that attribution.
    """

    title: str
    link: str
    content: str
    source: str = "graphrag"


# Repo-root path to the bot agent's GraphRAG query config (settings.yaml).
_GRAPHRAG_CONFIG_ROOT = (
    Path(__file__).resolve().parent.parent / "config" / "graphrag"
)


class _SourceFilteredVectorStore:
    """Wraps a GraphRAG ``VectorStore`` so per-call entity-ANN hits are
    restricted to the ids currently allowed by ``_ALLOWED_ENTITY_IDS``.

    GraphRAG's ``map_query_to_entities`` calls
    ``entity_text_embeddings.similarity_search_by_text(text, embedder,
    k=top_k * 2)`` — no pre-filter param is plumbed through. Rather
    than fork ``LocalSearchMixedContext.build_context``, we wrap the
    vectorstore once at builder-construction time:

    * When the context variable is empty (legacy callers / tenants
      with no ``KnowledgeSource`` list) the wrapper is a transparent
      pass-through.
    * Otherwise it asks the inner store for ``k * oversample`` hits,
      drops results whose ``document.id`` is not in the allowed set,
      and returns the first ``k`` survivors. Score order is preserved
      because the inner store returns results sorted by similarity.

    Attribute access for everything except ``similarity_search_by_text``
    falls through to the inner store via ``__getattr__``, so the
    factory keeps full access to ``connect``, ``search_by_id``, etc.
    """

    def __init__(self, inner: Any, oversample: int = _FILTER_OVERSAMPLE) -> None:
        self._inner = inner
        self._oversample = max(1, int(oversample))

    def __getattr__(self, name: str) -> Any:
        return getattr(self._inner, name)

    def similarity_search_by_text(
        self,
        text: str,
        text_embedder: Any,
        k: int = 10,
        **kwargs: Any,
    ) -> list[Any]:
        allowed = _ALLOWED_ENTITY_IDS.get()
        if not allowed:
            return self._inner.similarity_search_by_text(
                text=text, text_embedder=text_embedder, k=k, **kwargs
            )

        oversampled = self._inner.similarity_search_by_text(
            text=text,
            text_embedder=text_embedder,
            k=k * self._oversample,
            **kwargs,
        )
        kept: list[Any] = []
        for result in oversampled:
            doc_id = getattr(getattr(result, "document", None), "id", None)
            if doc_id is None:
                continue
            if str(doc_id) in allowed:
                kept.append(result)
                if len(kept) >= k:
                    break

        if not kept:
            logger.info(
                "GraphRAG source filter: 0/%d entity hits matched the "
                "allowed source folders — query likely targets a topic "
                "outside this tenant's KnowledgeSource set.",
                len(oversampled),
            )
        elif len(kept) < k:
            logger.debug(
                "GraphRAG source filter: kept %d/%d hits (requested %d, "
                "oversample=%dx) — consider raising _FILTER_OVERSAMPLE.",
                len(kept),
                len(oversampled),
                k,
                self._oversample,
            )
        return kept


class KnowledgeGraphService:
    """Graph-grounded *retrieval* service backed by GraphRAG's Local
    Search context builder.

    The service is a process-wide singleton (see
    ``get_knowledge_graph_service``). DataFrame loading is lazy on first
    use and cached for the lifetime of the process; call ``reload()``
    (or restart the bot) to pick up newly-published parquet outputs.

    Per query the service performs only the **context-building** half of
    GraphRAG's Local Search pipeline — embed the query, do an ANN match
    against the entity vector store, expand one hop through the graph,
    and resolve back to text units. **No completion LLM call is made**;
    the chat agent does final synthesis itself from the verbatim
    snippets we return.
    """

    def __init__(self) -> None:
        self._community_level = int(
            cfg("GRAPH_COMMUNITY_LEVEL", str(_DEFAULT_COMMUNITY_LEVEL))
        )
        # Blob container holding the parquet outputs. The sync project
        # writes versioned snapshots at
        # ``<container>/snapshots/<snapshot>/<name>.parquet`` and the
        # ``latest.json`` manifest pointer at the container root.
        self._blob_container = cfg("STORAGE_GRAPHRAG_OUTPUT_CONTAINER", "")

        self._config = None  # graphrag GraphRagConfig
        self._dfs: "dict[str, pd.DataFrame] | None" = None
        # Per-service community-report embedding cache. Populated by
        # ``_preload_report_embeddings()`` after every (re)load and
        # applied directly to the ``CommunityReport`` objects when
        # building the context builder, so each ``search_graph`` call
        # avoids thousands of serial ``search_by_id`` round-trips to
        # AI Search.
        self._report_embeddings_cache: dict[str, list[float]] = {}
        # Pre-built, immutable LocalSearch context builder + the params
        # we want to drive each ``build_context`` call with (text_unit_
        # prop, top_k_*, max_context_tokens, ...). Constructed once per
        # (re)load in ``_build_context_builder`` so per-query calls skip:
        #   * ``read_indexer_entities`` / ``read_indexer_reports``
        #     (pandas merge + groupby on 24k entities × 6k communities)
        #   * ``read_indexer_text_units`` / ``read_indexer_relationships``
        #   * ``LocalSearchMixedContext`` dict construction (24k+6k+2k+63k items)
        #   * Azure SearchClient creation + ``connect()``
        #   * LiteLLM embedding client + tokenizer init
        #
        # ``build_context`` is stateless across calls — no per-builder
        # mutable query state — so the same instance is shared by
        # concurrent queries without locking.
        self._context_builder: Any = None
        self._context_params: dict[str, Any] = {}
        # Reverse index used to scope graph retrieval per tenant. Each
        # entry maps ``KnowledgeSource.name`` (= ``raw_data.source_folder``
        # written by the sync project) to the set of ``entities.id``
        # values whose ``text_unit_ids`` touch any document tagged with
        # that folder. Built once per (re)load in
        # ``_build_entity_ids_by_source_folder`` so per-query lookups
        # are pure dict ops. Empty when filtering would be a no-op (no
        # documents carry ``source_folder``).
        self._entity_ids_by_source_folder: dict[str, frozenset[str]] = {}
        # Metadata for the currently-loaded build (manifest contents +
        # row counts). Populated on every successful load / reload so
        # ``GET /graph/admin/status`` can report what's serving traffic.
        self._loaded_version: dict[str, Any] | None = None
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

    def get_status(self) -> dict[str, Any]:
        """Return a snapshot of the currently-loaded graph build."""
        parquets_loaded = self._dfs is not None and self._config is not None
        engine_ready = self._context_builder is not None
        # "loaded" means fully usable — both parquets and the context
        # builder are ready. A partially-loaded state (parquets only) is
        # exposed via the granular fields so /graph/admin/status can
        # surface the half-loaded condition.
        status: dict[str, Any] = {
            "enabled": self._enabled,
            "loaded": parquets_loaded and engine_ready,
            "parquets_loaded": parquets_loaded,
            "engine_ready": engine_ready,
            "source": self._describe_source(),
            "community_level": self._community_level,
        }
        if self._loaded_version is not None:
            status["version"] = dict(self._loaded_version)
        if parquets_loaded and self._dfs is not None:
            status["row_counts"] = {
                name: int(len(df)) for name, df in self._dfs.items()
            }
        return status

    def _describe_source(self) -> dict[str, str]:
        if self._blob_container:
            return {"type": "blob", "container": self._blob_container}
        return {"type": "none"}

    async def reload(self) -> dict[str, Any]:
        """Atomically reload the graph from the configured source.

        On success: the new config + DataFrames + context builder are
        live; in-flight queries that captured the old
        ``_context_builder`` complete against the old graph; subsequent
        queries see the new graph. On failure: the prior state is
        restored exactly — so a broken reload never leaves the service
        in a half-loaded state (``_dfs`` set but ``_context_builder``
        ``None``) that would cause every subsequent ``search_graph`` to
        silently return ``None``.
        """
        if not self._enabled:
            raise RuntimeError(
                "KnowledgeGraphService is disabled: no parquet source configured."
            )

        async with self._load_lock:
            # Snapshot the prior state so we can roll back any partial
            # mutation if builder construction fails after the parquet
            # swap. Without this, a failure in ``_build_context_builder``
            # would leave ``_dfs``/``_config`` pointing at the new data
            # while ``_context_builder`` stays ``None`` — every
            # subsequent query would silently short-circuit at the
            # ``_context_builder is None`` check in ``search_graph``.
            prev_state = (
                self._config,
                self._dfs,
                self._loaded_version,
                self._report_embeddings_cache,
                self._context_builder,
                self._context_params,
                self._entity_ids_by_source_folder,
            )
            try:
                new_config = await asyncio.to_thread(self._load_config)
                new_dfs, new_version = await self._load_parquets_with_manifest()
                self._config = new_config
                self._dfs = new_dfs
                self._loaded_version = new_version
                await self._preload_report_embeddings()
                await asyncio.to_thread(self._build_context_builder)
                if self._context_builder is None:
                    # Defensive: _build_context_builder returns silently
                    # when config/dfs are missing; treat that as a hard
                    # failure here since reload() promises a fully
                    # constructed builder.
                    raise RuntimeError(
                        "GraphRAG context builder produced no instance — "
                        "refusing to swap to a half-built state"
                    )
                self._entity_ids_by_source_folder = (
                    self._build_entity_ids_by_source_folder()
                )
            except Exception:
                logger.exception(
                    "GraphRAG reload failed; restoring previous service state"
                )
                (
                    self._config,
                    self._dfs,
                    self._loaded_version,
                    self._report_embeddings_cache,
                    self._context_builder,
                    self._context_params,
                    self._entity_ids_by_source_folder,
                ) = prev_state
                raise

        logger.info(
            "KnowledgeGraphService reloaded: version=%s row_counts=%s",
            new_version,
            {name: len(df) for name, df in new_dfs.items()},
        )
        return self.get_status()

    async def reload_if_changed(self) -> dict[str, Any] | None:
        """Reload only when ``latest.json`` points to a new build.

        Cheap poll used by the bot's daily scheduler: read the manifest,
        compare its ``build_id`` against the currently-loaded build, and
        only pay the full parquet reload when they differ. Returns the
        new status dict when a reload happened, or ``None`` when the
        snapshot was already current (or the service is disabled).

        Never raises — a transient blob/manifest read failure is logged
        and treated as "no change" so the scheduler keeps serving the
        existing snapshot and retries on the next tick.
        """
        if not self._enabled:
            return None
        try:
            manifest = await self._load_manifest()
        except Exception:
            logger.exception(
                "GraphRAG manifest poll failed; keeping current snapshot"
            )
            return None

        new_build = manifest.get("build_id") if manifest else None
        current_manifest = (self._loaded_version or {}).get("manifest") or {}
        current_build = current_manifest.get("build_id")
        already_loaded = self._loaded_version is not None

        if (
            already_loaded
            and new_build is not None
            and new_build == current_build
        ):
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

    # ------------------------------------------------------------------ #
    # Public search API
    # ------------------------------------------------------------------ #

    async def search_graph(
        self,
        query: str,
        allowed_source_folders: set[str] | None = None,
    ) -> list[GraphSourceRef] | None:
        """Retrieve graph-grounded source snippets for a query.

        Performs the **context-building** half of GraphRAG's Local
        Search pipeline only: embed the query, ANN-match against the
        entity vector store, expand one hop through the graph, pull the
        community reports the matched entities belong to, and resolve
        back to the source text units. **No completion LLM call is
        made** — the chat agent gets verbatim snippets it can ground
        its own answer on, the same way it consumes
        ``search_knowledge_base`` results.

        Args:
            query: The user's question.
            allowed_source_folders: Optional set of
                ``KnowledgeSource.name`` values (equivalent to the
                ``raw_data.source_folder`` written by the sync project
                into ``documents.parquet``). When provided **and**
                non-empty **and** at least one folder is known to the
                pre-built reverse index, entity-ANN hits are scoped to
                entities whose source documents belong to those
                folders — mirroring how ``search_knowledge_base``
                restricts its AI Search query per tenant. ``None`` or
                an empty set (legacy callers, tenants with no source
                list, unknown folders) keeps the unscoped behaviour.

        Returns:
            A list of :class:`GraphSourceRef` objects (one per unique
            cited document, deduplicated). Returns ``None`` when the
            service is disabled or the underlying context build fails.
            Returns an empty list when the build succeeds but matches
            no documents.
        """
        if not self._enabled:
            logger.debug(
                "GraphRAG search_graph short-circuit: service is disabled"
            )
            return None
        if not await self._ensure_loaded():
            logger.warning(
                "GraphRAG search_graph short-circuit: _ensure_loaded() returned False"
            )
            return None
        if self._context_builder is None:
            # Half-loaded state — parquets are present but the context
            # builder never finished constructing. Almost always caused
            # by a failed reload() that didn't roll back (fixed) or a
            # still-running cold-start. Re-trigger a lazy rebuild
            # instead of silently returning empty.
            logger.warning(
                "GraphRAG search_graph: context builder missing — "
                "clearing state and forcing a rebuild on the next request"
            )
            async with self._load_lock:
                self._dfs = None
                self._config = None
                self._loaded_version = None
                self._report_embeddings_cache = {}
                self._context_builder = None
                self._context_params = {}
                self._entity_ids_by_source_folder = {}
            return None

        # ``build_context`` is stateless across calls, so the same
        # builder instance is shared by concurrent queries without
        # locking. The builder makes one embedding call + one AI
        # Search ANN request + several DataFrame joins — no completion
        # LLM call.
        allowed_entity_ids = self._resolve_allowed_entity_ids(
            allowed_source_folders
        )
        token = _ALLOWED_ENTITY_IDS.set(allowed_entity_ids)
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
            _ALLOWED_ENTITY_IDS.reset(token)

        return self._extract_sources_from_context(result.context_records)

    def _resolve_allowed_entity_ids(
        self, allowed_source_folders: set[str] | None
    ) -> frozenset[str] | None:
        """Translate a set of source-folder names to a flat entity-id set.

        Returns ``None`` (= no filtering) when:

        * the caller passed ``None`` or an empty set,
        * the reverse index is empty (no documents carry
          ``raw_data.source_folder``), or
        * none of the requested folders intersect the reverse index.

        Returns a non-empty frozenset otherwise; the wrapper
        :class:`_SourceFilteredVectorStore` consults it on each
        per-query ANN call.
        """
        if not allowed_source_folders:
            return None
        if not self._entity_ids_by_source_folder:
            return None
        ids: set[str] = set()
        matched: list[str] = []
        for folder in allowed_source_folders:
            bucket = self._entity_ids_by_source_folder.get(folder)
            if bucket:
                ids.update(bucket)
                matched.append(folder)
        if not ids:
            logger.info(
                "GraphRAG source filter: tenant requested folders %s but "
                "none are present in the current snapshot's reverse "
                "index — falling back to unscoped retrieval.",
                sorted(allowed_source_folders),
            )
            return None
        logger.debug(
            "GraphRAG source filter active: %d allowed entity ids across folders %s",
            len(ids),
            sorted(matched),
        )
        return frozenset(ids)

    # ------------------------------------------------------------------ #
    # Source-document extraction
    # ------------------------------------------------------------------ #

    def _extract_sources_from_context(
        self, context_records: Any
    ) -> list[GraphSourceRef]:
        """Walk a ``context_records`` payload and resolve cited
        text-unit short IDs back to their original source documents.

        The LocalSearch mixed context builder returns a flat
        ``{table_name: DataFrame}`` dict; we don't depend on its exact
        shape — we recursively collect any DataFrame with both ``id``
        and ``text`` columns and treat the ``id`` values as text-unit
        ``human_readable_id``s (matches GraphRAG's
        ``TextUnit.short_id``).
        """
        if self._dfs is None:
            return []

        text_units_df = self._dfs.get("text_units")
        documents_df = self._dfs.get("documents")
        if text_units_df is None or documents_df is None:
            return []

        short_ids = _collect_text_unit_short_ids(context_records)
        if not short_ids:
            return []

        normalised_ids: set[int] = set()
        for sid in short_ids:
            try:
                normalised_ids.add(int(sid))
            except (TypeError, ValueError):
                continue
        if not normalised_ids:
            return []

        matched_units = text_units_df[
            text_units_df["human_readable_id"].isin(list(normalised_ids))
        ]
        if matched_units.empty:
            return []

        doc_index = (
            documents_df.drop_duplicates(subset=["id"])
            .set_index("id")
        )
        # ``raw_data`` is the column GraphRAG persists from
        # ``TextDocument.raw_data``. The sync project's
        # ``SourceAwareMarkItDownFileReader`` writes
        # ``{"source_folder": "<kb-folder>"}`` into every row so the
        # bot can attribute each graph reference to a concrete
        # KnowledgeSource. Fallback paths are intentionally omitted —
        # a snapshot without ``raw_data`` is treated as incompatible
        # rather than silently regressing to lossy basename matching.
        if "raw_data" not in doc_index.columns:
            logger.error(
                "documents.parquet is missing the 'raw_data' column — "
                "this snapshot is incompatible. Republish from the "
                "sync project to fix."
            )
            return []

        # Group text units by document so each citation carries a single
        # representative chunk excerpt (largest chunk wins).
        sorted_units = matched_units.assign(
            _len=[len(str(v)) for v in matched_units["text"]]
        ).sort_values("_len", ascending=False)

        sources: list[GraphSourceRef] = []
        seen_docs: set[str] = set()
        unattributed = 0
        for _, row in sorted_units.iterrows():
            doc_id = row.get("document_id")
            if not doc_id or doc_id in seen_docs:
                continue
            seen_docs.add(doc_id)

            raw_title = ""
            source_name = ""
            if doc_id in doc_index.index:
                doc_row = doc_index.loc[doc_id]
                # ``.loc`` returns a DataFrame for duplicate ids; dedup
                # above guards against that but be defensive.
                if getattr(doc_row, "ndim", 1) > 1:
                    doc_row = doc_row.iloc[0]
                raw_title = str(doc_row.get("title") or "")
                rd = doc_row.get("raw_data")
                # Parquet round-trips dict-valued cells as plain dicts;
                # tolerate the (unexpected) None case for individual
                # rows by recording them under the generic source name.
                if isinstance(rd, dict):
                    source_name = str(rd.get("source_folder") or "")

            if not source_name:
                unattributed += 1
                source_name = "graphrag"
                knowledge_source = None
            else:
                knowledge_source = get_knowledge_source(source_name)

            # The document title is the full ``#``-encoded input path
            # (``typespec_docs#sub#file.md``). The KB-style link/display
            # contract expects a *source-folder-relative* encoded path
            # (``sub#file.md``) — the per-source ``base_url`` already
            # covers the folder — so strip the ``{source_folder}#`` prefix
            # we know from ``raw_data``. No-op for root-level blobs and for
            # unattributed rows (source_name == "graphrag").
            rel_title = _strip_source_prefix(raw_title, source_name)

            display_title, fallback_link = _doc_title_to_display(rel_title)
            # Prefer the KnowledgeSource's URL resolver so the link is
            # consistent with the KB tool's references — it knows about
            # per-source quirks (trim_format, suffix, custom link_fn).
            # Fall back to the raw path when the title's source folder
            # is registered but the KnowledgeSource lookup fails (e.g.
            # a folder that was removed from tenant_config).
            if knowledge_source is not None and rel_title:
                link = knowledge_source.get_link(rel_title)
            else:
                link = fallback_link

            # Title resolution mirrors the KB tool's _build_reference_title:
            # prefer a section-level ``h1 | h2 | h3`` path parsed from the
            # cited chunk so graph references read like the KB's
            # header-based titles instead of a bare file path. Fall back to
            # the document path (display_title) when the chunk has no
            # heading, then to a synthetic id as a last resort.
            chunk_title = _extract_chunk_header_path(str(row.get("text") or ""))
            ref_title = chunk_title or display_title or f"Document {doc_id[:12]}"

            sources.append(
                GraphSourceRef(
                    title=ref_title,
                    link=link,
                    content=str(row.get("text") or ""),
                    source=source_name,
                )
            )

        if unattributed:
            logger.warning(
                "GraphRAG context referenced %d document(s) with no "
                "raw_data.source_folder — they were attributed to "
                "source='graphrag'. Rebuild the snapshot to fix.",
                unattributed,
            )

        return sources

    # ------------------------------------------------------------------ #
    # Lazy loading
    # ------------------------------------------------------------------ #

    async def _ensure_loaded(self) -> bool:
        """Load GraphRagConfig + parquet DataFrames on first use."""
        if self._dfs is not None and self._config is not None:
            return True

        async with self._load_lock:
            if self._dfs is not None and self._config is not None:
                return True

            try:
                self._config = await asyncio.to_thread(self._load_config)
                self._dfs, self._loaded_version = (
                    await self._load_parquets_with_manifest()
                )
                await self._preload_report_embeddings()
                await asyncio.to_thread(self._build_context_builder)
                self._entity_ids_by_source_folder = (
                    self._build_entity_ids_by_source_folder()
                )
            except Exception:
                logger.exception("Failed to initialise KnowledgeGraphService")
                self._config = None
                self._dfs = None
                self._loaded_version = None
                self._report_embeddings_cache = {}
                self._context_builder = None
                self._context_params = {}
                self._entity_ids_by_source_folder = {}
                return False

        logger.info(
            "KnowledgeGraphService loaded: %s",
            {name: len(df) for name, df in self._dfs.items()},
        )
        return True

    def _load_config(self):
        """Load the bot's GraphRAG settings.yaml.

        GraphRAG's ``load_config`` calls
        ``string.Template(text).substitute(os.environ)`` to resolve
        ``${VAR}`` placeholders in the yaml — it reads strictly from
        ``os.environ``. The bot's ``config.app_config.init()`` mirrors
        every Azure App Configuration key into ``os.environ`` at
        startup (with ``setdefault`` semantics so ``.env`` / real env
        vars win), so by the time this runs the placeholders in
        ``config/graphrag/settings.yaml`` resolve correctly without any
        per-key whitelist here.
        """
        from graphrag.config.load_config import load_config

        return load_config(_GRAPHRAG_CONFIG_ROOT)

    async def _load_parquets_with_manifest(
        self,
    ) -> "tuple[dict[str, pd.DataFrame], dict[str, Any]]":
        """Load parquets and return them alongside version metadata.

        Fetches ``latest.json`` from the container root to discover the
        active snapshot prefix, then downloads each parquet from
        ``<container>/<prefix>/<name>.parquet``. The manifest is opaque
        to the bot — whatever the sync project wrote is echoed back via
        ``get_status()``.
        """
        if not self._blob_container:
            raise RuntimeError(
                "STORAGE_GRAPHRAG_OUTPUT_CONTAINER is not configured; "
                "cannot load GraphRAG parquet artefacts."
            )

        manifest = await self._load_manifest()
        snapshot_prefix = self._snapshot_prefix(manifest)
        dfs = await self._load_parquets_from_blob(snapshot_prefix)
        version: dict[str, Any] = {
            "source": "blob",
            "container": self._blob_container,
            "snapshot": snapshot_prefix,
        }
        if manifest:
            version["manifest"] = manifest
        return dfs, version

    async def _load_manifest(self) -> dict[str, Any] | None:
        """Read ``latest.json`` from the blob container root, if present."""
        import json

        data = await download_blob(self._blob_container, "latest.json")
        if data is None:
            logger.info(
                "GraphRAG manifest not found at %s/latest.json — using unversioned layout",
                self._blob_container,
            )
            return None
        try:
            manifest = json.loads(data.decode("utf-8"))
        except (ValueError, UnicodeDecodeError) as exc:
            logger.warning(
                "GraphRAG manifest unparseable (%s); falling back to unversioned layout",
                exc,
            )
            return None
        if not isinstance(manifest, dict):
            logger.warning(
                "GraphRAG manifest is not a JSON object; falling back to unversioned layout"
            )
            return None
        return manifest

    def _snapshot_prefix(self, manifest: dict[str, Any] | None) -> str:
        """Resolve the parquet prefix to read from for this load."""
        if manifest:
            sub = str(manifest.get("prefix", "")).strip("/")
            if sub:
                return sub
        return ""

    async def _load_parquets_from_blob(
        self, snapshot_prefix: str
    ) -> "dict[str, pd.DataFrame]":
        """Download the parquet files from blob storage into a temp dir."""
        temp_dir = Path(tempfile.mkdtemp(prefix="graphrag-output-"))
        logger.info(
            "Downloading GraphRAG parquets from blob container '%s' (prefix='%s') to %s",
            self._blob_container,
            snapshot_prefix,
            temp_dir,
        )

        await asyncio.gather(
            *(
                self._download_one_parquet(name, snapshot_prefix, temp_dir)
                for name in _REQUIRED_PARQUETS
            )
        )

        return await asyncio.to_thread(self._load_parquets_from_path, temp_dir)

    async def _download_one_parquet(
        self, name: str, snapshot_prefix: str, dest_dir: Path
    ) -> None:
        blob_name = (
            f"{snapshot_prefix}/{name}.parquet" if snapshot_prefix else f"{name}.parquet"
        )
        data = await download_blob(self._blob_container, blob_name)
        if data is None:
            raise FileNotFoundError(
                f"GraphRAG parquet not found: {self._blob_container}/{blob_name}"
            )
        (dest_dir / f"{name}.parquet").write_bytes(data)

    @staticmethod
    def _load_parquets_from_path(path: Path) -> dict[str, "pd.DataFrame"]:
        import pandas as pd

        dfs: dict[str, pd.DataFrame] = {}
        for name in _REQUIRED_PARQUETS:
            file_path = path / f"{name}.parquet"
            if not file_path.is_file():
                raise FileNotFoundError(
                    f"GraphRAG parquet not found: {file_path}"
                )
            dfs[name] = pd.read_parquet(file_path)
        return dfs

    # ------------------------------------------------------------------ #
    # Community-report embedding preload + monkey-patch
    # ------------------------------------------------------------------ #

    async def _preload_report_embeddings(self) -> None:
        """Bulk-fetch every community-report embedding into memory.

        The LocalSearch context builder requires each
        ``CommunityReport`` carry a ``full_content_embedding`` so it can
        rank community summaries against the query embedding. By
        default ``read_indexer_report_embeddings`` issues one
        synchronous ``search_by_id`` per report against the Azure AI
        Search ``community_full_content`` index — for a 6k-report graph
        that means thousands of serial round-trips per query (~7
        minutes).

        Instead we paginate the index *once* on (re)load and stash the
        embeddings in ``self._report_embeddings_cache``;
        ``_build_context_builder`` then applies them directly to the
        ``CommunityReport`` list before constructing the builder, and
        per-query calls never touch AI Search for embeddings.

        Pagination uses Azure AI Search's ``search('*', top=N)`` which
        the SDK transparently iterates across all pages. With ~6k
        documents this completes in ~5-10 seconds.
        """
        if self._config is None:
            return

        try:
            from graphrag.config.embeddings import (
                community_full_content_embedding,
            )
            from graphrag.utils.api import get_embedding_store
        except ImportError:
            logger.warning(
                "Could not import graphrag vector store factory; "
                "community embedding preload disabled."
            )
            return

        def _fetch_all() -> dict[str, list[float]]:
            store = get_embedding_store(
                config=self._config.vector_store,
                embedding_name=community_full_content_embedding,
            )
            id_field = store.id_field
            vector_field = store.vector_field
            results = store.db_connection.search(
                search_text="*",
                select=[id_field, vector_field],
                top=100000,
            )
            cache: dict[str, list[float]] = {}
            for result in results:
                doc_id = result.get(id_field)
                vector = result.get(vector_field)
                if doc_id and vector is not None:
                    cache[str(doc_id)] = list(vector)
            return cache

        start = time.monotonic()
        try:
            cache = await asyncio.to_thread(_fetch_all)
        except Exception:
            logger.warning(
                "Failed to preload community-report embeddings; "
                "search_graph will fall back to per-query fetch (slow).",
                exc_info=True,
            )
            self._report_embeddings_cache = {}
            return

        elapsed = time.monotonic() - start
        self._report_embeddings_cache = cache
        logger.info(
            "Preloaded %d community-report embeddings in %.2fs "
            "(eliminates per-query AI Search round-trips)",
            len(cache),
            elapsed,
        )

    def _build_context_builder(self) -> None:
        """Pre-build the LocalSearch context builder once per load.

        Mirrors ``graphrag.api.query.local_search`` *up to but not
        including the LLM completion call*: we drive
        ``LocalSearchMixedContext.build_context`` directly to get raw
        graph-grounded snippets without paying the ~20s completion
        cost. To avoid duplicating the factory's wiring of embedder,
        tokenizer, vector store, search filters, etc. (which changes
        across graphrag versions), we still call
        ``get_local_search_engine`` and just keep its
        ``context_builder`` + ``context_builder_params`` — the wrapper
        ``LocalSearch`` instance and its ``chat_model`` are garbage-
        collected immediately.

        Moves every reusable artefact out of the request path:

        * Azure AI Search ``description_embedding_store`` (with
          ``connect()`` done once)
        * ``read_indexer_entities`` / ``read_indexer_reports`` —
          pandas merge + groupby on 24k entities × 6k communities
        * ``read_indexer_text_units`` / ``read_indexer_relationships`` —
          DataFrame → typed list conversion
        * Community-report embeddings (assigned from
          ``self._report_embeddings_cache``)
        * LiteLLM embedding client + tokenizer init
        * ``LocalSearchMixedContext`` construction, which internally
          builds id-indexed lookup dicts for 24k entities, 6k reports,
          2k text units and 63k relationships

        ``build_context`` is stateless across calls, so the resulting
        builder is safe to share across concurrent queries without
        locking.
        """
        if self._config is None or self._dfs is None:
            return

        from graphrag.config.embeddings import entity_description_embedding
        from graphrag.query.factory import get_local_search_engine
        from graphrag.query.indexer_adapters import (
            read_indexer_entities,
            read_indexer_relationships,
            read_indexer_reports,
            read_indexer_text_units,
        )
        from graphrag.utils.api import get_embedding_store

        config = self._config
        dfs = self._dfs

        start = time.monotonic()

        description_embedding_store = get_embedding_store(
            config=config.vector_store,
            embedding_name=entity_description_embedding,
        )

        entities_ = read_indexer_entities(
            dfs["entities"], dfs["communities"], self._community_level
        )
        reports = read_indexer_reports(
            dfs["community_reports"], dfs["communities"], self._community_level
        )
        text_units_ = read_indexer_text_units(dfs["text_units"])
        relationships_ = read_indexer_relationships(dfs["relationships"])

        # Apply pre-fetched embeddings to the report list. Replaces
        # graphrag's per-query read_indexer_report_embeddings, which
        # would otherwise issue one search_by_id per report. The
        # community context selector uses these embeddings to pick
        # which reports to include in the prompt.
        cache = self._report_embeddings_cache
        if cache:
            hits = 0
            for report in reports:
                vec = cache.get(str(report.id))
                report.full_content_embedding = vec
                if vec is not None:
                    hits += 1
            logger.info(
                "Applied %d/%d community-report embeddings from cache",
                hits,
                len(reports),
            )
        else:
            # Fall back to the original (slow) per-report lookup so the
            # builder still functions when preload failed.
            from graphrag.config.embeddings import (
                community_full_content_embedding,
            )
            from graphrag.query.indexer_adapters import (
                read_indexer_report_embeddings,
            )

            fallback_store = get_embedding_store(
                config=config.vector_store,
                embedding_name=community_full_content_embedding,
            )
            read_indexer_report_embeddings(reports, fallback_store)

        # The factory wires up embedder + tokenizer + vector store +
        # context-builder-params dict according to graphrag's current
        # internal contract. We immediately discard the LocalSearch
        # wrapper (and its chat_model — we never use the completion
        # path) and keep only what build_context needs.
        engine = get_local_search_engine(
            config=config,
            reports=reports,
            text_units=text_units_,
            entities=entities_,
            relationships=relationships_,
            covariates={},
            description_embedding_store=description_embedding_store,
            response_type="multiple paragraphs",  # unused (no LLM call)
            system_prompt=None,
            callbacks=None,
        )
        self._context_builder = engine.context_builder
        self._context_params = dict(engine.context_builder_params or {})

        # Drop the source-filtering wrapper around the entity vector
        # store after the engine is constructed (the factory binds the
        # raw store in __init__). When no tenant filter is active on a
        # given query (legacy callers / tenants without a source list)
        # the wrapper is a pass-through, so unscoped behaviour is
        # preserved bit-for-bit.
        inner_store = getattr(self._context_builder, "entity_text_embeddings", None)
        if inner_store is not None and not isinstance(
            inner_store, _SourceFilteredVectorStore
        ):
            self._context_builder.entity_text_embeddings = (
                _SourceFilteredVectorStore(inner_store)
            )

        elapsed = time.monotonic() - start
        logger.info(
            "Built LocalSearch context builder in %.2fs "
            "(entities=%d, reports=%d, text_units=%d, relationships=%d)",
            elapsed,
            len(entities_),
            len(reports),
            len(text_units_),
            len(relationships_),
        )

    def _build_entity_ids_by_source_folder(self) -> dict[str, frozenset[str]]:
        """Compute the per-source-folder entity-id reverse index.

        Joins ``documents`` → ``text_units`` → ``entities`` parquets:

        * ``documents.raw_data['source_folder']`` is the tag the sync
          project writes for every input file
          (``SourceAwareMarkItDownFileReader``).
        * ``text_units.document_id`` points back at ``documents.id``.
        * ``entities.text_unit_ids`` is the list of text-unit ids each
          extracted entity appears in.

        Returns an empty dict (= filtering disabled) when the
        snapshot pre-dates the source_folder annotation or the
        required dataframes are missing — the caller treats an empty
        index as "no filtering" and falls back to unscoped retrieval.
        """
        if self._dfs is None:
            return {}

        documents_df = self._dfs.get("documents")
        text_units_df = self._dfs.get("text_units")
        entities_df = self._dfs.get("entities")
        if documents_df is None or text_units_df is None or entities_df is None:
            return {}

        if "raw_data" not in documents_df.columns:
            logger.warning(
                "GraphRAG source filter disabled: documents.parquet has no "
                "'raw_data' column — tenant-scoped graph retrieval requires "
                "a snapshot built with SourceAwareMarkItDownFileReader."
            )
            return {}

        start = time.monotonic()

        folder_by_doc: dict[str, str] = {}
        for doc_id, raw_data in zip(
            documents_df["id"].tolist(),
            documents_df["raw_data"].tolist(),
        ):
            if not isinstance(raw_data, dict):
                continue
            folder = raw_data.get("source_folder")
            if folder:
                folder_by_doc[str(doc_id)] = str(folder)

        if not folder_by_doc:
            logger.warning(
                "GraphRAG source filter disabled: no document carries "
                "raw_data.source_folder — rebuild the snapshot to enable "
                "tenant scoping."
            )
            return {}

        folder_by_text_unit: dict[str, str] = {}
        for tu_id, doc_id in zip(
            text_units_df["id"].tolist(),
            text_units_df["document_id"].tolist(),
        ):
            folder = folder_by_doc.get(str(doc_id))
            if folder:
                folder_by_text_unit[str(tu_id)] = folder

        if not folder_by_text_unit:
            return {}

        accumulator: dict[str, set[str]] = {}
        for ent_id, tu_ids in zip(
            entities_df["id"].tolist(),
            entities_df["text_unit_ids"].tolist(),
        ):
            if tu_ids is None:
                continue
            ent_id_str = str(ent_id)
            for tu in tu_ids:
                folder = folder_by_text_unit.get(str(tu))
                if folder:
                    accumulator.setdefault(folder, set()).add(ent_id_str)

        result = {folder: frozenset(ids) for folder, ids in accumulator.items()}
        elapsed = time.monotonic() - start
        logger.info(
            "Built source-folder→entity-id reverse index in %.2fs "
            "(%d folders, total %d entity attributions)",
            elapsed,
            len(result),
            sum(len(v) for v in result.values()),
        )
        return result


def _collect_text_unit_short_ids(context_records: Any) -> set[str]:
    """Recursively walk a ``context_records`` payload and collect every
    text-unit short id we can find.

    The LocalSearch mixed context builder returns a flat
    ``{table_name: DataFrame}`` dict. The exact shape can vary across
    graphrag versions, so we treat any value that is a pandas DataFrame
    *and* has both ``id`` and ``text`` columns as a candidate
    "sources" table.
    """
    import pandas as pd

    found: set[str] = set()

    def visit(node: Any) -> None:
        if node is None:
            return
        if isinstance(node, pd.DataFrame):
            cols = set(node.columns)
            if {"id", "text"}.issubset(cols):
                for value in node["id"].astype(str).tolist():
                    if value:
                        found.add(value)
            return
        if isinstance(node, dict):
            for v in node.values():
                visit(v)
            return
        if isinstance(node, (list, tuple, set)):
            for v in node:
                visit(v)
            return

    visit(context_records)
    return found


def _doc_title_to_display(raw_title: str) -> tuple[str, str]:
    """Convert a stored ``documents.title`` into ``(display_title, link)``.

    The sync project encodes original file paths by replacing ``/`` and
    ``os.sep`` with ``#``. We reverse that here so titles look like
    ordinary paths in the agent's reference list.
    """
    title = (raw_title or "").strip()
    if not title:
        return "", ""
    pretty = title.replace("#", "/")
    return pretty, pretty


def _strip_source_prefix(raw_title: str, source_folder: str) -> str:
    """Drop the leading ``{source_folder}#`` segment from a doc title.

    The sync project's ``SourceAwareMarkItDownFileReader`` stores the
    *full* ``#``-encoded input path as ``documents.title``
    (``typespec_docs#sub#file.md``) so the title is globally unique
    across source folders. The KB-style link/display contract, however,
    works on the **source-folder-relative** encoded path
    (``sub#file.md``) because each ``KnowledgeSource``'s ``base_url``
    already encodes the folder. We strip the known ``source_folder``
    prefix to bridge the two.

    No-op when ``source_folder`` is empty (root-level blob), when it is
    the synthetic ``"graphrag"`` fallback used for unattributed rows, or
    when the title does not actually start with that prefix.
    """
    if not source_folder or not raw_title:
        return raw_title
    prefix = f"{source_folder}#"
    if raw_title.startswith(prefix):
        return raw_title[len(prefix):]
    return raw_title


# Matches an ATX markdown header line (``#`` .. ``######`` followed by a
# space and the heading text). We deliberately ignore the leading-``#``
# heading levels' count beyond classifying depth — only the text matters
# for display. Trailing ``#`` characters (closed ATX headers) are
# stripped by the caller.
_HEADER_RE = re.compile(r"^(#{1,6})\s+(.*?)\s*#*\s*$")
# Fenced code-block delimiters (``` or ~~~, with optional leading
# whitespace and an optional info string). ``#`` lines inside a fence are
# comments/shell prompts, never markdown headers, so we must skip them.
_FENCE_RE = re.compile(r"^\s*(`{3,}|~{3,})")
# How many headers deep we keep, mirroring the KB tool's
# ``header1 | header2 | header3`` (3 levels).
_MAX_HEADER_DEPTH = 3


def _extract_chunk_header_path(text: str) -> str:
    """Derive a ``h1 | h2 | h3`` heading path from a text-unit chunk.

    GraphRAG splits documents into fixed-size token windows that — unlike
    the KB tool's header-aware chunks — carry no header metadata. We
    recover a section-level title by scanning the chunk's own markdown:
    we track the most recent heading at each level (1-3) in document
    order and join them with `` | `` (same shape as the KB tool's
    ``_build_reference_title``).

    Lines inside fenced code blocks are skipped so shell prompts and
    comments (``# do this``) are not mistaken for headers. Returns an
    empty string when the chunk contains no usable heading (e.g. it
    starts mid-section); the caller then falls back to the document
    title.
    """
    if not text:
        return ""

    # Most recent heading text seen at each depth (1-indexed).
    headings: list[str | None] = [None, None, None]
    in_fence = False
    for line in text.splitlines():
        if _FENCE_RE.match(line):
            in_fence = not in_fence
            continue
        if in_fence:
            continue
        m = _HEADER_RE.match(line)
        if not m:
            continue
        depth = len(m.group(1))
        heading_text = m.group(2).strip()
        if not heading_text or depth > _MAX_HEADER_DEPTH:
            continue
        # Record this heading at its depth and clear any deeper levels so
        # a later shallow heading doesn't keep a stale deeper sibling.
        idx = depth - 1
        headings[idx] = heading_text
        for deeper in range(idx + 1, _MAX_HEADER_DEPTH):
            headings[deeper] = None

    parts = [h for h in headings if h]
    return " | ".join(parts)



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
