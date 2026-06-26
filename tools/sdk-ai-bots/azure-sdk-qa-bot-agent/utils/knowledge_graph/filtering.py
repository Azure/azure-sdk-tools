"""Tenant-scoped entity filtering for GraphRAG Local Search.

GraphRAG's ``map_query_to_entities`` runs an unfiltered ANN search against
the entity vector store. To restrict a query to a tenant's documents we
wrap that store (:class:`SourceFilteredVectorStore`) and feed it the set of
allowed entity ids via a context variable, computed per query from a
reverse index built once per snapshot load.

The filter mirrors the KB tool's two layers:

* **source-folder layer** — ``KnowledgeSource.name`` == the
  ``raw_data.source_folder`` the sync project writes for every document
  (the KB tool's ``context_id eq '...'`` clause).
* **file (source_path) layer** — per-source title filters from
  ``tenant_config.source_filter`` (the KB tool's
  ``search.ismatch(...,'title')`` clauses), translated into
  case-insensitive terms matched against each document's ``source_path``.
"""

from __future__ import annotations

import contextvars
import logging
import re
import time
from typing import Any

logger = logging.getLogger(__name__)

# Oversample factor applied when a tenant filter is active: we ask the
# entity vectorstore for ``k * factor`` ANN hits and keep the first ``k``
# that belong to the allowed entity ids. 8x comfortably covers tenants that
# own ~12% of the entity universe without paying noticeable extra latency —
# the underlying ANN call is the same cost regardless of ``k`` once ``k``
# exceeds the index's segment cache.
_FILTER_OVERSAMPLE = 8

# Per-asyncio-task context variable carrying the set of entity ids the
# current ``build_context`` call is allowed to surface. ``None`` (default)
# disables filtering — used both for legacy callers that pass no tenant and
# for tenants whose filter resolves to "no scoping".
_ALLOWED_ENTITY_IDS: contextvars.ContextVar[frozenset[str] | None] = (
    contextvars.ContextVar("graphrag_allowed_entity_ids", default=None)
)

# Extracts the literal term from each ``search.ismatch('<term>', ...)``
# clause in an OData filter string. ``*`` wildcards are stripped so the
# residual is matched as a plain case-insensitive substring.
_ISMATCH_RE = re.compile(r"search\.ismatch\(\s*'([^']*)'", re.IGNORECASE)


def allowed_entity_ids_var() -> "contextvars.ContextVar[frozenset[str] | None]":
    """Expose the context variable so the service can set/reset it per query."""
    return _ALLOWED_ENTITY_IDS


def parse_title_filter_terms(odata_filter: str) -> list[str]:
    """Translate a KB ``source_filter`` OData clause to lowercase terms.

    The KB tool scopes some sources with full-text clauses like
    ``search.ismatch('typespec-python', 'title') or
    search.ismatch('generate*', 'title')``. We can't run OData full-text on
    the parquet side, so we extract each literal (``typespec-python``,
    ``generate``) and the caller matches them as case-insensitive
    substrings of the document ``source_path``. Returns an empty list when
    the clause carries no ``search.ismatch`` literal.
    """
    terms: list[str] = []
    for match in _ISMATCH_RE.finditer(odata_filter or ""):
        term = match.group(1).strip().strip("*").lower()
        if term:
            terms.append(term)
    return terms


class SourceFilteredVectorStore:
    """Wrap a GraphRAG ``VectorStore`` so per-call entity-ANN hits are
    restricted to the ids currently allowed by ``_ALLOWED_ENTITY_IDS``.

    * When the context variable is empty (legacy callers / tenants with no
      scoping) the wrapper is a transparent pass-through.
    * Otherwise it asks the inner store for ``k * oversample`` hits, drops
      results whose ``document.id`` is not in the allowed set, and returns
      the first ``k`` survivors. Score order is preserved because the inner
      store returns results sorted by similarity.

    Attribute access for everything except ``similarity_search_by_text``
    falls through to the inner store via ``__getattr__``.
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
            if doc_id is not None and str(doc_id) in allowed:
                kept.append(result)
                if len(kept) >= k:
                    break

        if not kept:
            logger.info(
                "GraphRAG source filter: 0/%d entity hits matched the allowed "
                "source folders — query likely targets a topic outside this "
                "tenant's KnowledgeSource set.",
                len(oversampled),
            )
        elif len(kept) < k:
            logger.debug(
                "GraphRAG source filter: kept %d/%d hits (requested %d, "
                "oversample=%dx).",
                len(kept),
                len(oversampled),
                k,
                self._oversample,
            )
        return kept


def wrap_entity_store(context_builder: Any) -> None:
    """Wrap the builder's entity vector store in :class:`SourceFilteredVectorStore`.

    The factory binds the raw store in ``__init__``; we swap it once after
    construction. When no tenant filter is active on a query the wrapper is
    a pass-through, so unscoped behaviour is preserved bit-for-bit.
    """
    inner_store = getattr(context_builder, "entity_text_embeddings", None)
    if inner_store is not None and not isinstance(inner_store, SourceFilteredVectorStore):
        context_builder.entity_text_embeddings = SourceFilteredVectorStore(inner_store)


def build_entity_index(dfs: "dict[str, Any]") -> "dict[str, dict[str, frozenset[str]]]":
    """Compute the per-source-folder reverse index used to scope retrieval.

    Joins ``documents`` → ``text_units`` → ``entities`` parquets and returns
    ``{source_folder: {entity_id: frozenset(source_path_lower)}}``: for each
    folder, the entity ids it contributes mapped to the lowercased
    ``source_path``s of the documents that produced them (so the per-query
    resolver can additionally apply the file-level filter).

    Returns an empty dict (= filtering disabled) when the snapshot pre-dates
    the ``raw_data`` annotation or the required dataframes are missing.
    """
    documents_df = dfs.get("documents")
    text_units_df = dfs.get("text_units")
    entities_df = dfs.get("entities")
    if documents_df is None or text_units_df is None or entities_df is None:
        return {}

    if "raw_data" not in documents_df.columns:
        logger.warning(
            "GraphRAG source filter disabled: documents.parquet has no "
            "'raw_data' column — tenant-scoped graph retrieval requires a "
            "snapshot built with SourceAwareMarkItDownFileReader."
        )
        return {}

    start = time.monotonic()

    folder_by_doc: dict[str, str] = {}
    path_by_doc: dict[str, str] = {}
    for doc_id, raw_data in zip(
        documents_df["id"].tolist(), documents_df["raw_data"].tolist()
    ):
        if not isinstance(raw_data, dict):
            continue
        folder = raw_data.get("source_folder")
        if folder:
            folder_by_doc[str(doc_id)] = str(folder)
            path_by_doc[str(doc_id)] = str(raw_data.get("source_path") or "").lower()

    if not folder_by_doc:
        logger.warning(
            "GraphRAG source filter disabled: no document carries "
            "raw_data.source_folder — rebuild the snapshot to enable scoping."
        )
        return {}

    # text_unit id -> (folder, source_path_lower)
    tu_attribution: dict[str, tuple[str, str]] = {}
    for tu_id, doc_id in zip(
        text_units_df["id"].tolist(), text_units_df["document_id"].tolist()
    ):
        folder = folder_by_doc.get(str(doc_id))
        if folder:
            tu_attribution[str(tu_id)] = (folder, path_by_doc.get(str(doc_id), ""))

    if not tu_attribution:
        return {}

    accumulator: dict[str, dict[str, set[str]]] = {}
    for ent_id, tu_ids in zip(
        entities_df["id"].tolist(), entities_df["text_unit_ids"].tolist()
    ):
        if tu_ids is None:
            continue
        ent_id_str = str(ent_id)
        for tu in tu_ids:
            attribution = tu_attribution.get(str(tu))
            if attribution is None:
                continue
            folder, path = attribution
            accumulator.setdefault(folder, {}).setdefault(ent_id_str, set()).add(path)

    result = {
        folder: {ent_id: frozenset(paths) for ent_id, paths in ents.items()}
        for folder, ents in accumulator.items()
    }
    logger.info(
        "Built source-folder→entity-id reverse index in %.2fs "
        "(%d folders, total %d entity attributions)",
        time.monotonic() - start,
        len(result),
        sum(len(v) for v in result.values()),
    )
    return result


def resolve_allowed_entity_ids(
    index: "dict[str, dict[str, frozenset[str]]]",
    allowed_source_folders: "set[str] | None",
    source_path_filters: "dict[str, list[str]] | None" = None,
) -> "frozenset[str] | None":
    """Translate folder names (+ optional file filters) to an entity-id set.

    Returns ``None`` (= no filtering) when the caller passed no folders, the
    reverse index is empty, or none of the requested folders intersect it.

    For each requested folder, entities are taken from the index. When
    ``source_path_filters`` carries terms for that folder, an entity is kept
    only if at least one of its document ``source_path``s contains one of
    the (lowercase) terms — the file-level layer mirroring the KB tool's
    per-source title filter.
    """
    if not allowed_source_folders or not index:
        return None

    ids: set[str] = set()
    matched: list[str] = []
    for folder in allowed_source_folders:
        bucket = index.get(folder)
        if not bucket:
            continue
        matched.append(folder)
        terms = (source_path_filters or {}).get(folder)
        if not terms:
            ids.update(bucket.keys())
            continue
        for ent_id, paths in bucket.items():
            if any(term in path for path in paths for term in terms):
                ids.add(ent_id)

    if not ids:
        logger.info(
            "GraphRAG source filter: tenant requested folders %s but none "
            "matched the current snapshot — falling back to unscoped retrieval.",
            sorted(allowed_source_folders),
        )
        return None

    logger.debug(
        "GraphRAG source filter active: %d allowed entity ids across folders %s",
        len(ids),
        sorted(matched),
    )
    return frozenset(ids)
