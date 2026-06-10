"""Knowledge graph query service using Microsoft GraphRAG.

Wraps GraphRAG's ``LocalSearch`` engine against the indexes and graph
artefacts produced by the ``azure-sdk-qa-bot-knowledge-graph-sync``
project. Exposes a single public method,
:meth:`KnowledgeGraphService.local_search`, that returns a single-LLM
graph-aware answer plus the source documents it cited.

Why local search (and not DRIFT / global)
-----------------------------------------
The chat agent already has a fast vector-KB tool (Azure AI Search over
text chunks). The graph tool is meant to *enhance* that — supplying
entity-level grounding the vector index alone can't see — without
adding tens of seconds of latency. Measured on the bot's snapshot:

* ``local_search``: **~21s** per query (1 LLM call). Uses the graph
  for entity matching + 1-hop relationship expansion + community-
  membership + entity → text-unit back-references.
* ``drift_search``: ~73s (primer + N×local + reduce LLM calls).
* ``global_search``: ~110s (map over 3k+ community reports + reduce).

DRIFT / global also add multi-hop / global-summary capabilities the
chat agent doesn't need: the agent's own LLM does the cross-tool
synthesis. Local search is the sweet spot — graph-aware grounding at
single-vector-search latency.

POC scope
---------
Tenant filtering (``scope`` / ``service_type`` / ``source_folder``
allow-lists) has been removed from this layer along with the
``document_meta.parquet`` machinery. ``local_search`` now exposes a
minimal signature — ``local_search(query)`` — and serves the same graph
to every caller. Per-tenant masking will be layered back on top once
the blob-direct end-to-end pipeline is validated.

Data sources at query time
--------------------------
GraphRAG splits its query-time inputs into two stores; this service
reads from both because each contains a different *kind* of data:

* **Azure AI Search** — *all vector data.* ``LocalSearch``'s
  ``MixedLocalContextBuilder`` calls
  ``get_embedding_store(config.vector_store, ...)`` to look up the top
  entity-description matches for the query.
* **Parquet artefacts** — *graph structure only.* ``entities`` /
  ``communities`` / ``community_reports`` / ``text_units`` /
  ``relationships`` / ``documents`` parquets contain IDs, names,
  descriptions, edges, community hierarchy, and source text — but no
  vector columns. GraphRAG requires these DataFrames so it can resolve
  embedding hits back to entities, traverse relationships, pull the
  community reports the entities belong to, and recover the source
  text units for the LLM prompt.

The parquet artefacts are loaded once (lazily, on first query) from
the Azure Blob container named by ``STORAGE_GRAPHRAG_OUTPUT_CONTAINER``;
the bot then atomically swaps in a new build whenever the sync project
calls ``POST /admin/graphrag/reload``.
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
    """A source-document reference cited by a GraphRAG local search.

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
    """Query service that delegates to GraphRAG's ``LocalSearch`` engine.

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
        # building the search engine, so each ``local_search`` call
        # avoids thousands of serial ``search_by_id`` round-trips to
        # AI Search.
        self._report_embeddings_cache: dict[str, list[float]] = {}
        # Pre-built, immutable LocalSearch engine. Constructed once per
        # (re)load in ``_build_search_engine`` so per-query calls skip:
        #   * ``read_indexer_entities`` / ``read_indexer_reports``
        #     (pandas merge + groupby on 24k entities × 6k communities)
        #   * ``read_indexer_text_units`` / ``read_indexer_relationships``
        #   * ``LocalSearchMixedContext`` dict construction (24k+6k+2k+63k items)
        #   * Azure SearchClient creation + ``connect()``
        #   * LiteLLM client construction + tokenizer init
        #   * prompt file reads
        #
        # ``LocalSearch.search`` is stateless across calls (no
        # per-engine mutable ``query_state``), so the same engine
        # instance is shared by concurrent queries without locking.
        self._search_engine: Any = None
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
        parquets_loaded = self._dfs is not None and self._config is not None
        engine_ready = self._search_engine is not None
        # "loaded" means fully usable — both parquets and the search
        # engine are ready. A partially-loaded state (parquets only) is
        # exposed via the granular fields so /admin/graphrag/status can
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

        On success: the new config + DataFrames + search engine are
        live; in-flight queries that captured the old ``_search_engine``
        complete against the old graph; subsequent queries see the new
        graph. On failure: the prior state is restored exactly — so a
        broken reload never leaves the service in a half-loaded state
        (``_dfs`` set but ``_search_engine`` ``None``) that would
        cause every subsequent ``local_search`` to silently return an
        empty answer.
        """
        if not self._enabled:
            raise RuntimeError(
                "KnowledgeGraphService is disabled: no parquet source configured."
            )

        async with self._load_lock:
            # Snapshot the prior state so we can roll back any partial
            # mutation if engine construction fails after the parquet
            # swap. Without this, a failure in ``_build_search_engine``
            # would leave ``_dfs``/``_config`` pointing at the new data
            # while ``_search_engine`` stays ``None`` — every
            # subsequent query would silently short-circuit at the
            # ``_search_engine is None`` check in ``local_search``.
            prev_state = (
                self._config,
                self._dfs,
                self._loaded_version,
                self._report_embeddings_cache,
                self._search_engine,
            )
            try:
                new_config = await asyncio.to_thread(self._load_config)
                new_dfs, new_version = await self._load_parquets_with_manifest()
                self._config = new_config
                self._dfs = new_dfs
                self._loaded_version = new_version
                await self._preload_report_embeddings()
                await asyncio.to_thread(self._build_search_engine)
                if self._search_engine is None:
                    # Defensive: _build_search_engine returns silently
                    # when config/dfs are missing; treat that as a hard
                    # failure here since reload() promises a fully
                    # constructed engine.
                    raise RuntimeError(
                        "GraphRAG engine build produced no search_engine — "
                        "refusing to swap to a half-built state"
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
                    self._search_engine,
                ) = prev_state
                raise

        logger.info(
            "KnowledgeGraphService reloaded: version=%s row_counts=%s",
            new_version,
            {name: len(df) for name, df in new_dfs.items()},
        )
        return self.get_status()

    # ------------------------------------------------------------------ #
    # Public search API
    # ------------------------------------------------------------------ #

    async def local_search(
        self, query: str
    ) -> tuple[str, list[GraphSourceRef]] | None:
        """Run a GraphRAG Local Search and return the synthesised answer
        together with the source documents it cited.

        Local Search uses the graph for query-relevant grounding:
        entity-description vector match → 1-hop relationship expansion
        → community-report summaries for the matched entities →
        entity → text-unit back-references for the source chunks. A
        single LLM call then synthesises the answer over that context.

        Single LLM call ⇒ ~20s latency. We deliberately do not use
        DRIFT (multi-LLM, ~70s) or Global Search (fan-out over all
        community reports, ~110s) — the chat agent's own LLM does the
        cross-tool reasoning, so Local Search is the right level of
        graph-aware grounding to feed it.

        Args:
            query: The user's question.

        Returns:
            ``(answer, sources)`` on success, where ``sources`` is a
            list of :class:`GraphSourceRef` objects (one per unique
            cited document). Returns ``None`` when the service is
            disabled or the underlying search call fails.
        """
        if not self._enabled:
            logger.debug(
                "GraphRAG local_search short-circuit: service is disabled"
            )
            return None
        if not await self._ensure_loaded():
            logger.warning(
                "GraphRAG local_search short-circuit: _ensure_loaded() returned False"
            )
            return None
        if self._search_engine is None:
            # This is the half-loaded state — parquets are present but
            # the search engine never finished constructing. Almost
            # always caused by a failed reload() that didn't roll back
            # (fixed) or a still-running cold-start. Re-trigger a
            # lazy rebuild instead of silently returning empty.
            logger.warning(
                "GraphRAG local_search: search engine missing — "
                "clearing state and forcing a rebuild on the next request"
            )
            async with self._load_lock:
                self._dfs = None
                self._config = None
                self._loaded_version = None
                self._report_embeddings_cache = {}
                self._search_engine = None
            return None

        # ``LocalSearch.search`` is stateless across calls (no
        # per-engine mutable ``query_state``), so the same engine
        # instance is shared by concurrent queries without locking.
        try:
            result = await self._search_engine.search(query=query)
        except Exception:
            logger.warning("GraphRAG local_search failed", exc_info=True)
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
        """Walk a search ``context_data`` payload and resolve cited
        text-unit short IDs back to their original source documents.

        ``LocalSearch`` returns a flat ``{table_name: DataFrame}`` dict;
        we don't depend on its exact shape — we recursively collect any
        DataFrame with both ``id`` and ``text`` columns and treat the
        ``id`` values as text-unit ``human_readable_id``s (matches
        GraphRAG's ``TextUnit.short_id``).
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
                self._search_engine = None
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

        ``LocalSearch``'s context builder requires each
        ``CommunityReport`` carry a ``full_content_embedding`` so it can
        rank community summaries against the query embedding. By
        default ``read_indexer_report_embeddings`` issues one
        synchronous ``search_by_id`` per report against the Azure AI
        Search ``community_full_content`` index — for a 6k-report graph
        that means thousands of serial round-trips per query (~7
        minutes).

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
                "local_search will fall back to per-query fetch (slow).",
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
        """Pre-build the LocalSearch engine once per load.

        Mirrors ``graphrag.api.query.local_search`` but moves every
        reusable artefact out of the request path:

        * Azure AI Search ``description_embedding_store`` (with
          ``connect()`` done once)
        * ``read_indexer_entities`` / ``read_indexer_reports`` —
          pandas merge + groupby on 24k entities × 6k communities
        * ``read_indexer_text_units`` / ``read_indexer_relationships`` —
          DataFrame to typed list conversion
        * Community-report embeddings (assigned from
          ``self._report_embeddings_cache``)
        * Local search prompt file read
        * LiteLLM completion + embedding client + tokenizer init
        * ``MixedLocalContextBuilder`` construction, which internally
          builds id-indexed lookup dicts for 24k entities, 6k reports,
          2k text units and 63k relationships

        ``LocalSearch.search`` is stateless across calls, so the
        returned engine is safe to share across concurrent queries
        without locking.
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
        # would otherwise issue one search_by_id per report. Local
        # Search uses the community summaries (and therefore relies on
        # the same ``full_content_embedding`` field for prioritisation).
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

        local_prompt = load_search_prompt(config.local_search.prompt)

        self._search_engine = get_local_search_engine(
            config=config,
            reports=reports,
            text_units=text_units_,
            entities=entities_,
            relationships=relationships_,
            covariates={},
            description_embedding_store=description_embedding_store,
            response_type=self._response_type,
            system_prompt=local_prompt,
            callbacks=None,
        )

        elapsed = time.monotonic() - start
        logger.info(
            "Built LocalSearch engine in %.2fs "
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
    """Recursively walk a search ``context_data`` payload and collect every
    text-unit short id we can find.

    LocalSearch returns a flat ``{table_name: DataFrame}`` dict; DRIFT
    returned a nested ``{sub_query: {table: DataFrame}}``. The exact
    shape can vary across graphrag versions and search modes, so we
    treat any value that is a pandas DataFrame *and* has both ``id``
    and ``text`` columns as a candidate "sources" table.
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
