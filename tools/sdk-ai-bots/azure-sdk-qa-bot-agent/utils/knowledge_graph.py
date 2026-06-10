"""Knowledge graph query service using Microsoft GraphRAG.

Wraps :func:`graphrag.api.drift_search` against the indexes and graph
artefacts produced by the ``azure-sdk-qa-bot-knowledge-graph-sync``
project.

POC scope
---------
Tenant filtering (``scope`` / ``service_type`` / ``source_folder``
allow-lists) has been removed from this layer along with the
``document_meta.parquet`` machinery. ``drift_search`` now exposes a
minimal signature — ``drift_search(query)`` — and serves the same graph
to every caller. Per-tenant masking will be layered back on top once
the blob-direct end-to-end pipeline is validated.

Data sources at query time
--------------------------
GraphRAG splits its query-time inputs into two stores; this service
reads from both because each contains a different *kind* of data:

* **Azure AI Search** — *all vector data.* GraphRAG's ``drift_search``
  calls ``get_embedding_store(config.vector_store, ...)`` internally,
  so every entity/community embedding similarity lookup hits the AI
  Search indexes configured in ``config/graphrag/settings.yaml``.
* **Parquet artefacts** — *graph structure only.* ``entities`` /
  ``communities`` / ``community_reports`` / ``text_units`` /
  ``relationships`` / ``documents`` parquets contain IDs, names,
  descriptions, edges, community hierarchy, and source text — but no
  vector columns. GraphRAG requires these DataFrames so it can resolve
  embedding hits back to entities, traverse relationships, and pull
  the actual report / chunk text for the LLM prompt.

The parquet artefacts are loaded once (lazily, on first query) from
the Azure Blob container named by ``STORAGE_GRAPHRAG_OUTPUT_CONTAINER``;
the bot then atomically swaps in a new build whenever the sync project
calls ``POST /admin/graphrag/reload``.

DRIFT (Dynamic Reasoning and Inference with Flexible Traversal) is
GraphRAG's hybrid search mode: a community-level "primer" generates
seed answers, then per-seed local searches expand context via graph
traversal, and a reduce step combines them into the final response. It
is heavier than local/global search but produces more comprehensive
graph-aware answers — well suited for the bot agent's complex SDK /
TypeSpec questions.
"""

from __future__ import annotations

import asyncio
import logging
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING, Any

from config.app_config import get as cfg
from utils.azure_storage import download_blob

if TYPE_CHECKING:
    import pandas as pd

logger = logging.getLogger(__name__)

_DEFAULT_COMMUNITY_LEVEL = 2
_DEFAULT_RESPONSE_TYPE = "multiple paragraphs"

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
    """A source-document reference cited by a GraphRAG DRIFT search.

    Attributes:
        title:   Human-readable document title (path with ``/`` separators).
        link:    Best-effort link to the original document. Empty string
                 when no URL can be derived without tenant context.
        content: A representative excerpt of the source text used to
                 ground the LLM answer (typically one text_unit chunk).
        source:  Short identifier for the originating data set; always
                 ``"graphrag"`` for now since the graph does not preserve
                 the per-document KnowledgeSource that produced it.
    """

    title: str
    link: str
    content: str
    source: str = "graphrag"


# Repo-root path to the bot agent's GraphRAG query config (settings.yaml).
_GRAPHRAG_CONFIG_ROOT = (
    Path(__file__).resolve().parent.parent / "config" / "graphrag"
)


class KnowledgeGraphService:
    """Query service that delegates to GraphRAG's ``drift_search`` APIs.

    The service is a process-wide singleton (see
    ``get_knowledge_graph_service``). DataFrame loading is lazy on first
    use and cached for the lifetime of the process; call ``reload()``
    (or restart the bot) to pick up newly-published parquet outputs.
    """

    def __init__(self) -> None:
        self._community_level = int(
            cfg("GRAPH_COMMUNITY_LEVEL", str(_DEFAULT_COMMUNITY_LEVEL))
        )
        self._response_type = cfg("GRAPH_RESPONSE_TYPE", _DEFAULT_RESPONSE_TYPE)
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
        # building the search engine, so each ``drift_search`` call
        # avoids thousands of serial ``search_by_id`` round-trips to
        # AI Search.
        self._report_embeddings_cache: dict[str, list[float]] = {}
        # Pre-built, immutable DRIFTSearch components. Constructed once
        # per (re)load in ``_build_search_engine`` so per-query calls
        # skip:
        #   * ``read_indexer_entities`` / ``read_indexer_reports``
        #     (pandas merge + groupby on 24k entities × 6k communities)
        #   * ``read_indexer_text_units`` / ``read_indexer_relationships``
        #   * ``LocalSearchMixedContext`` dict construction (24k+6k+2k+63k items)
        #   * Azure SearchClient creation + ``connect()``
        #   * LiteLLM client construction + tokenizer init
        #   * prompt file reads
        #
        # Each ``drift_search`` call cheaply builds a fresh
        # ``DRIFTSearch`` from these shared components — that instance
        # owns its own mutable ``query_state``/primer/local_search, so
        # concurrent queries never collide and no lock is required.
        self._chat_model: Any = None
        self._tokenizer: Any = None
        self._context_builder: Any = None
        # Metadata for the currently-loaded build (manifest contents +
        # row counts). Populated on every successful load / reload so
        # ``GET /admin/graphrag/status`` can report what's serving traffic.
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
        loaded = self._dfs is not None and self._config is not None
        status: dict[str, Any] = {
            "enabled": self._enabled,
            "loaded": loaded,
            "source": self._describe_source(),
            "community_level": self._community_level,
        }
        if self._loaded_version is not None:
            status["version"] = dict(self._loaded_version)
        if loaded and self._dfs is not None:
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

        Builds new ``config`` + ``DataFrames`` into locals, then swaps
        the service's references under ``_load_lock``. In-flight queries
        keep their captured snapshot of ``self._dfs`` and complete
        against the old data; subsequent queries see the new data.
        Failures preserve the prior state (no partial swap).
        """
        if not self._enabled:
            raise RuntimeError(
                "KnowledgeGraphService is disabled: no parquet source configured."
            )

        async with self._load_lock:
            new_config = await asyncio.to_thread(self._load_config)
            new_dfs, new_version = await self._load_parquets_with_manifest()
            self._config = new_config
            self._dfs = new_dfs
            self._loaded_version = new_version
            await self._preload_report_embeddings()
            await asyncio.to_thread(self._build_search_engine)

        logger.info(
            "KnowledgeGraphService reloaded: version=%s row_counts=%s",
            new_version,
            {name: len(df) for name, df in new_dfs.items()},
        )
        return self.get_status()

    # ------------------------------------------------------------------ #
    # Public search API
    # ------------------------------------------------------------------ #

    async def drift_search(
        self, query: str
    ) -> tuple[str, list[GraphSourceRef]] | None:
        """Run a GraphRAG DRIFT search and return the synthesised answer
        together with the source documents it cited.

        DRIFT search combines a community-level primer (global-style)
        with per-seed local searches that traverse the graph for
        follow-up context, then reduces them into a single comprehensive
        answer.

        Args:
            query: The user's question.

        Returns:
            ``(answer, sources)`` on success, where ``sources`` is a
            list of :class:`GraphSourceRef` objects (one per unique
            cited document). Returns ``None`` when the service is
            disabled or the underlying search call fails.
        """
        if not self._enabled:
            return None
        if not await self._ensure_loaded():
            return None
        if self._context_builder is None or self._chat_model is None:
            logger.warning("GraphRAG search components not initialised")
            return None

        # Build a fresh DRIFTSearch per query. The expensive bits
        # (context_builder dicts, LLM clients, tokenizer) are shared,
        # but each DRIFTSearch owns its own mutable ``query_state`` +
        # primer + local_search wrappers, so concurrent queries do not
        # contend on a single mutable engine and no lock is needed.
        # Cost of constructing a fresh engine is sub-millisecond.
        from graphrag.query.structured_search.drift_search.search import (
            DRIFTSearch,
        )

        engine = DRIFTSearch(
            model=self._chat_model,
            context_builder=self._context_builder,
            tokenizer=self._tokenizer,
        )
        try:
            result = await engine.search(query=query)
        except Exception:
            logger.warning("GraphRAG drift_search failed", exc_info=True)
            return None

        answer = _coerce_response(result.response) or ""
        sources = self._extract_sources_from_context(result.context_data)
        return answer, sources

    # ------------------------------------------------------------------ #
    # Source-document extraction
    # ------------------------------------------------------------------ #

    def _extract_sources_from_context(
        self, context_data: Any
    ) -> list[GraphSourceRef]:
        """Walk a DRIFT ``context_data`` payload and resolve cited
        text-unit short IDs back to their original source documents.

        DRIFT context is a nested structure (``{sub_query: {table_name:
        DataFrame}}``); we don't depend on its exact shape — we
        recursively collect any DataFrame with both ``id`` and ``text``
        columns and treat the ``id`` values as text-unit
        ``human_readable_id``s (matches GraphRAG's ``TextUnit.short_id``).
        """
        if self._dfs is None:
            return []

        text_units_df = self._dfs.get("text_units")
        documents_df = self._dfs.get("documents")
        if text_units_df is None or documents_df is None:
            return []

        short_ids = _collect_text_unit_short_ids(context_data)
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
            .set_index("id")[["title"]]
        )

        # Group text units by document so each citation carries a single
        # representative chunk excerpt (largest chunk wins).
        sorted_units = matched_units.assign(
            _len=[len(str(v)) for v in matched_units["text"]]
        ).sort_values("_len", ascending=False)

        sources: list[GraphSourceRef] = []
        seen_docs: set[str] = set()
        for _, row in sorted_units.iterrows():
            doc_id = row.get("document_id")
            if not doc_id or doc_id in seen_docs:
                continue
            seen_docs.add(doc_id)

            if doc_id in doc_index.index:
                title_val = doc_index.loc[doc_id, "title"]
                # ``.loc`` returns a Series when the index has duplicates;
                # dedup above guards against that but be defensive in case
                # any sneak through.
                if hasattr(title_val, "iloc"):
                    title_val = title_val.iloc[0] if len(title_val) else ""
                raw_title = str(title_val or "")
            else:
                raw_title = ""
            display_title, link = _doc_title_to_display(raw_title)

            sources.append(
                GraphSourceRef(
                    title=display_title or f"Document {doc_id[:12]}",
                    link=link,
                    content=str(row.get("text") or ""),
                    source="graphrag",
                )
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
                await asyncio.to_thread(self._build_search_engine)
            except Exception:
                logger.exception("Failed to initialise KnowledgeGraphService")
                self._config = None
                self._dfs = None
                self._loaded_version = None
                self._report_embeddings_cache = {}
                self._chat_model = None
                self._tokenizer = None
                self._context_builder = None
                return False

        logger.info(
            "KnowledgeGraphService loaded: %s",
            {name: len(df) for name, df in self._dfs.items()},
        )
        return True

    def _load_config(self):
        """Load the bot's GraphRAG settings.yaml."""
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

        ``DRIFTSearchContextBuilder`` requires each ``CommunityReport``
        carry a ``full_content_embedding``. By default
        ``read_indexer_report_embeddings`` issues one synchronous
        ``search_by_id`` per report against the Azure AI Search
        ``community_full_content`` index — for a 6k-report graph that
        means thousands of serial round-trips per query (~7 minutes).

        Instead we paginate the index *once* on (re)load and stash the
        embeddings in ``self._report_embeddings_cache``;
        ``_build_search_engine`` then applies them directly to the
        ``CommunityReport`` list before constructing the engine, and
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
                "drift_search will fall back to per-query fetch (slow).",
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

    def _build_search_engine(self) -> None:
        """Pre-build all reusable DRIFTSearch components once per load.

        Mirrors ``graphrag.api.query.drift_search_streaming`` but moves
        every reusable artefact out of the request path:

        * Azure AI Search ``description_embedding_store`` (with
          ``connect()`` done once)
        * ``read_indexer_entities`` / ``read_indexer_reports`` —
          pandas merge + groupby on 24k entities × 6k communities
        * ``read_indexer_text_units`` / ``read_indexer_relationships`` —
          DataFrame to typed list conversion
        * Community-report embeddings (assigned from
          ``self._report_embeddings_cache``)
        * Local search prompt + reduce prompt file reads
        * LiteLLM completion + embedding client + tokenizer init
        * ``DRIFTSearchContextBuilder`` construction, which internally
          builds ``LocalSearchMixedContext`` dicts indexed by id for
          24k entities, 6k reports, 2k text units and 63k relationships

        We use ``get_drift_search_engine`` once as a convenient
        constructor for these components, then steal its immutable
        fields (``model``, ``context_builder``, ``tokenizer``). Each
        per-query ``drift_search`` call then builds a fresh
        ``DRIFTSearch`` from those shared components so that
        per-query mutable state (``query_state``, primer, local_search)
        is isolated — enabling lock-free concurrent queries.
        """
        if self._config is None or self._dfs is None:
            return

        from graphrag.config.embeddings import entity_description_embedding
        from graphrag.query.factory import get_drift_search_engine
        from graphrag.query.indexer_adapters import (
            read_indexer_entities,
            read_indexer_relationships,
            read_indexer_reports,
            read_indexer_text_units,
        )
        from graphrag.utils.api import get_embedding_store, load_search_prompt

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
        # would otherwise issue one search_by_id per report.
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
            # engine still functions when preload failed.
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

        local_prompt = load_search_prompt(config.drift_search.prompt)
        reduce_prompt = load_search_prompt(config.drift_search.reduce_prompt)

        # ``get_drift_search_engine`` is a convenient way to construct
        # the model + context_builder + tokenizer wiring without
        # duplicating its setup here. We never call ``search()`` on
        # this template instance; we just lift its immutable fields.
        template = get_drift_search_engine(
            config=config,
            reports=reports,
            text_units=text_units_,
            entities=entities_,
            relationships=relationships_,
            description_embedding_store=description_embedding_store,
            local_system_prompt=local_prompt,
            reduce_system_prompt=reduce_prompt,
            response_type=self._response_type,
            callbacks=None,
        )

        self._chat_model = template.model
        self._context_builder = template.context_builder
        self._tokenizer = template.tokenizer

        elapsed = time.monotonic() - start
        logger.info(
            "Built DRIFT search components in %.2fs "
            "(entities=%d, reports=%d, text_units=%d, relationships=%d)",
            elapsed,
            len(entities_),
            len(reports),
            len(text_units_),
            len(relationships_),
        )


def _coerce_response(response: object) -> str | None:
    """Convert a GraphRAG response value to a string (or None when empty)."""
    if response is None:
        return None
    if isinstance(response, str):
        return response.strip() or None
    return str(response).strip() or None


def _collect_text_unit_short_ids(context_data: Any) -> set[str]:
    """Recursively walk a DRIFT ``context_data`` payload and collect every
    text-unit short id we can find.

    DRIFT returns a nested dict (``{sub_query: {table: DataFrame}}``)
    but the exact shape can vary across graphrag versions. We treat any
    value that is a pandas DataFrame *and* has both ``id`` and ``text``
    columns as a candidate "sources" table.
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

    visit(context_data)
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
