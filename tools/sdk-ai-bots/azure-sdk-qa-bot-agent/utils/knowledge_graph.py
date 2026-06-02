"""Knowledge graph query service using Microsoft GraphRAG.

Wraps :func:`graphrag.api.drift_search` against the indexes and graph
artefacts produced by the ``azure-sdk-qa-bot-knowledge-graph-sync`` project.

Data sources at query time
--------------------------
GraphRAG splits its query-time inputs into two stores; this service reads
from both because each contains a different *kind* of data:

* **Azure AI Search** — *all vector data.*
  GraphRAG's ``drift_search`` calls ``get_embedding_store(config.vector_store,
  ...)`` internally, so every entity/community embedding similarity lookup
  hits the AI Search indexes configured in
  ``config/graphrag/settings.yaml`` (``entities``, ``communities``,
  ``text_units``). No embeddings live in this process.
* **Parquet artefacts** — *graph structure only.*
  ``entities`` / ``communities`` / ``community_reports`` / ``text_units`` /
  ``relationships`` parquets contain IDs, names, descriptions, edges,
  community hierarchy, and source text — but **no vector columns**.
  GraphRAG requires these DataFrames so it can resolve embedding hits back
  to entities, traverse relationships, and pull the actual report / chunk
  text for the LLM prompt.

The parquet artefacts are loaded once (lazily, on first query) from
the Azure Blob container named by ``STORAGE_GRAPHRAG_OUTPUT_CONTAINER``;
the bot then atomically swaps in a new build whenever the sync
project calls ``POST /admin/graphrag/reload``.

DRIFT (Dynamic Reasoning and Inference with Flexible Traversal) is
GraphRAG's hybrid search mode: a community-level "primer" generates seed
answers, then per-seed local searches expand context via graph traversal,
and a reduce step combines them into the final response. It is heavier than
local/global search but produces more comprehensive graph-aware answers —
well suited for the bot agent's complex SDK / TypeSpec questions.
"""

from __future__ import annotations

import asyncio
import logging
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING, Any
from urllib.parse import urlparse

from config.app_config import get as cfg
from utils.azure_storage import download_blob

if TYPE_CHECKING:
    import pandas as pd

logger = logging.getLogger(__name__)

_DEFAULT_COMMUNITY_LEVEL = 2
_DEFAULT_RESPONSE_TYPE = "multiple paragraphs"

# Parquet artefacts produced by the GraphRAG indexing pipeline that we need
# to drive query operations.
#  - ``documents`` is required so we can map text_units back to their original
#    source-document filename / path when building Reference entries.
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

    The service is a process-wide singleton (see ``get_knowledge_graph_service``).
    DataFrame loading is lazy on first use and cached for the lifetime of the
    process; restart the bot to pick up newly-published parquet outputs.
    """

    def __init__(self) -> None:
        self._community_level = int(
            cfg("GRAPH_COMMUNITY_LEVEL", str(_DEFAULT_COMMUNITY_LEVEL))
        )
        self._response_type = cfg("GRAPH_RESPONSE_TYPE", _DEFAULT_RESPONSE_TYPE)
        # Blob container holding the parquet outputs. The sync project
        # writes both the versioned snapshots
        # (``<snapshot>/<name>.parquet``) and the ``latest.json``
        # manifest pointer at the container root — no sub-prefix.
        self._blob_container = cfg("STORAGE_GRAPHRAG_OUTPUT_CONTAINER", "")

        self._config = None  # graphrag GraphRagConfig
        self._dfs: "dict[str, pd.DataFrame] | None" = None
        # Metadata for the currently-loaded build (manifest contents +
        # row counts). Populated on every successful load / reload so
        # ``GET /admin/graphrag/status`` can report what's serving traffic.
        self._loaded_version: dict[str, Any] | None = None
        self._load_lock = asyncio.Lock()

        # Service is considered "available" when a blob container is
        # configured. Tool registration is governed separately by
        # KNOWLEDGE_TOOL_MODE in agents/chat_agent.
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
        """Return a snapshot of the currently-loaded graph build.

        Used by ``GET /admin/graphrag/status`` and surfaced in
        ``reload()``'s response so callers can confirm which build is
        serving traffic.
        """
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
            return {
                "type": "blob",
                "container": self._blob_container,
            }
        return {"type": "none"}

    async def reload(self) -> dict[str, Any]:
        """Atomically reload the graph from the configured source.

        Builds new ``config`` + ``DataFrames`` into locals, then swaps
        the service's references under ``_load_lock``. In-flight queries
        keep their captured snapshot of ``self._dfs`` and complete
        against the old data; subsequent queries see the new data.
        Failures preserve the prior state (no partial swap).

        Raises:
            RuntimeError: when the service is disabled (no source
                configured) — the caller should surface this as 409.
            Exception:    any failure during config load / parquet read.
        """
        if not self._enabled:
            raise RuntimeError(
                "KnowledgeGraphService is disabled: no parquet source configured."
            )

        async with self._load_lock:
            new_config = await asyncio.to_thread(self._load_config)
            new_dfs, new_version = await self._load_parquets_with_manifest()
            # Atomic pointer swap (Python ref assignment under GIL).
            self._config = new_config
            self._dfs = new_dfs
            self._loaded_version = new_version

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
        self,
        query: str,
        *,
        context_ids: list[str] | None = None,  # kept for caller compat
    ) -> tuple[str, list[GraphSourceRef]] | None:
        """Run a GraphRAG DRIFT search and return the synthesised answer
        together with the source documents it cited.

        DRIFT search combines a community-level primer (global-style) with
        per-seed local searches that traverse the graph for follow-up
        context, then reduces them into a single comprehensive answer.

        Args:
            query:       The user's question.
            context_ids: Ignored — kept for backwards compatibility with the
                         previous chunk-id-based API. GraphRAG does not
                         currently support per-source filtering at query time.

        Returns:
            ``(answer, sources)`` on success, where:
              - ``answer`` is the synthesised graph-aware response string,
              - ``sources`` is a list of :class:`GraphSourceRef` objects,
                one per unique original-document file that contributed to
                the answer.
            Returns ``None`` when the service is disabled or the underlying
            search call fails.
        """
        if not self._enabled:
            return None
        if not await self._ensure_loaded():
            return None

        # Imported lazily so the bot can boot when graphrag is unavailable.
        from graphrag.api import drift_search as graphrag_drift_search

        try:
            response, context = await graphrag_drift_search(
                config=self._config,
                entities=self._dfs["entities"],
                communities=self._dfs["communities"],
                community_reports=self._dfs["community_reports"],
                text_units=self._dfs["text_units"],
                relationships=self._dfs["relationships"],
                community_level=self._community_level,
                response_type=self._response_type,
                query=query,
            )
        except Exception:
            logger.warning("GraphRAG drift_search failed", exc_info=True)
            return None

        answer = _coerce_response(response)
        if answer is None:
            answer = ""

        sources = self._extract_sources_from_context(context)
        return answer, sources

    # ------------------------------------------------------------------ #
    # Source-document extraction
    # ------------------------------------------------------------------ #

    def _extract_sources_from_context(
        self, context_data: Any
    ) -> list[GraphSourceRef]:
        """Walk a DRIFT ``context_data`` payload and resolve cited
        text-unit IDs back to their original source documents.

        The DRIFT context is a nested structure (``{sub_query: {table_name:
        DataFrame}}``). We do not depend on its exact shape — we recursively
        collect any DataFrame that looks like a "sources" table (has both
        ``id`` and ``text`` columns) and treat the ``id`` column values as
        text-unit ``human_readable_id``s (matches GraphRAG's
        ``TextUnit.short_id``).
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

        # human_readable_id is stored as int; cast collected values defensively.
        normalised_ids: set[int] = set()
        for sid in short_ids:
            try:
                normalised_ids.add(int(sid))
            except (TypeError, ValueError):
                continue
        if not normalised_ids:
            return []

        matched_units = text_units_df[
            text_units_df["human_readable_id"].isin(normalised_ids)
        ]
        if matched_units.empty:
            return []

        # documents.id ↔ text_units.document_id
        doc_index = documents_df.set_index("id")[["title"]]

        # Group text units by document so each Reference carries a single
        # representative chunk excerpt from that document (largest chunk).
        sources: list[GraphSourceRef] = []
        seen_docs: set[str] = set()
        # Sort by chunk size descending so the most informative chunk wins
        # when multiple chunks from the same doc are cited.
        sorted_units = matched_units.assign(
            _len=matched_units["text"].astype(str).str.len()
        ).sort_values("_len", ascending=False)

        for _, row in sorted_units.iterrows():
            doc_id = row.get("document_id")
            if not doc_id or doc_id in seen_docs:
                continue
            seen_docs.add(doc_id)

            if doc_id in doc_index.index:
                raw_title = str(doc_index.loc[doc_id, "title"] or "")
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
            except Exception:
                logger.exception("Failed to initialise KnowledgeGraphService")
                # Force re-attempt on the next call
                self._config = None
                self._dfs = None
                self._loaded_version = None
                return False

        logger.info(
            "KnowledgeGraphService loaded: %s",
            {name: len(df) for name, df in self._dfs.items()},
        )
        return True

    def _load_config(self):
        """Load the bot's GraphRAG settings.yaml.

        The DRIFT-search latency tuning lives in ``settings.yaml`` under
        the ``drift_search:`` block — see that file for what's being
        overridden vs upstream defaults.
        """
        from graphrag.config.load_config import load_config

        return load_config(_GRAPHRAG_CONFIG_ROOT)

    async def _load_parquets_with_manifest(
        self,
    ) -> "tuple[dict[str, pd.DataFrame], dict[str, Any]]":
        """Load parquets and return them alongside version metadata.

        For blob sources, fetches ``latest.json`` first to find the
        active versioned snapshot prefix and uses it to download the
        parquets. The manifest is opaque to the bot — whatever the sync
        project wrote is echoed back via ``get_status()``.

        Falls back to ``<container>/*.parquet`` (unversioned root
        layout) when no manifest exists.
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
        """Read ``latest.json`` from the blob container root, if present.

        Returns ``None`` when the manifest is missing or unparseable —
        callers then fall back to assuming an unversioned root layout.
        """
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
        """Resolve the parquet prefix to read from for this load.

        When a manifest is present, its ``prefix`` field names the
        versioned snapshot directory (e.g. ``2026-06-02T...``); without
        a manifest the parquets are assumed to live at the container
        root (returns ``""``).
        """
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
            "Downloading GraphRAG parquets from blob container '%s' (prefix='%s') "
            "to %s",
            self._blob_container,
            snapshot_prefix,
            temp_dir,
        )

        download_tasks = [
            self._download_one_parquet(name, snapshot_prefix, temp_dir)
            for name in _REQUIRED_PARQUETS
        ]
        await asyncio.gather(*download_tasks)

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

    DRIFT returns a nested dict (``{sub_query: {table: DataFrame}}``) but
    the exact shape can vary across graphrag versions. We treat any value
    that is a pandas DataFrame *and* has both ``id`` and ``text`` columns as
    a candidate "sources" table — the convention used by
    ``build_text_unit_context``.
    """
    import pandas as pd  # local import keeps top-level import lazy

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
    ``os.sep`` with ``#`` (see ``daily_sync.py`` / ``typespec_processor.py``
    in ``azure-sdk-qa-bot-knowledge-graph-sync``). We reverse that here so
    titles look like ordinary paths in the agent's reference list.

    ``link`` is best-effort: we return the path-style title so downstream
    rendering can prefix a base URL if appropriate, or leave it as a path
    when no URL prefix is known. We do not have per-document
    ``KnowledgeSource`` context at query time.
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
