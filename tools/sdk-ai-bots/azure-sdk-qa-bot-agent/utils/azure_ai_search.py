"""Azure AI Search SDK client helpers for knowledge retrieval.

This module uses the Azure AI Search Python SDK (not raw REST).  Each search
result is automatically expanded by its header hierarchy so the agent gets
full section context in a single call.  Sibling queries run concurrently
via ``asyncio.gather`` for fast response times.

Three retrieval strategies are available and combined by ``fused_search``:
  - **Agentic search** – uses the KnowledgeBaseRetrievalClient for
    intent-aware multi-step retrieval (opt-in, ``deep`` mode only).
  - **Vector search** – hybrid semantic + vector query (always on).
  - **Keyword search** – sparse BM25 full-text query (always on).
"""

from __future__ import annotations

import asyncio
import logging
import time
from enum import Enum
from typing import Callable

from azure.core.exceptions import HttpResponseError
from azure.search.documents.aio import SearchClient as AzureSearchClient
from azure.search.documents.indexes.aio import SearchIndexerClient
from azure.search.documents.indexes.models import IndexerExecutionStatus
from azure.search.documents.knowledgebases.aio import KnowledgeBaseRetrievalClient
from azure.search.documents.knowledgebases.models import (
    KnowledgeBaseRetrievalRequest,
    KnowledgeRetrievalSemanticIntent,
    KnowledgeSourceParams,
    SearchIndexKnowledgeSourceParams,
)
from azure.search.documents.models import (
    QueryType,
    VectorizableTextQuery,
)
from utils.azure_credential import get_credential

from config.app_config import get as cfg
from config.tenant_config import get_knowledge_source
from models.knowledge import KnowledgeChunk

logger = logging.getLogger(__name__)

_KB_MAX_OUTPUT_SIZE = 20000
_HIERARCHY_EXPANSION_TOP = 20
_INDEXER_POLL_INTERVAL_SECS = 2.0
_INDEXER_TIMEOUT_SECS = 300.0

# Chunks below this rerank score are considered low-relevance and dropped.
_RERANK_SCORE_LOW_RELEVANCE_THRESHOLD = 2.0

# Rank-smoothing constant for Reciprocal Rank Fusion.
_RRF_K = 60

# Page-type filters for raw source chunks and generated wiki pages.
NON_WIKI_FILTER = "(page_type eq null or page_type eq '')"
WIKI_FILTER = "(page_type eq 'summary' or page_type eq 'entity' or page_type eq 'concept')"

# Fields selected by the dense/sparse retrievers. ``page_type`` distinguishes
# generated wiki pages from raw chunks (for link rendering / optional boosting).
_RETRIEVER_SELECT_FIELDS = [
    "chunk_id",
    "title",
    "chunk",
    "context_id",
    "header_1",
    "header_2",
    "header_3",
    "ordinal_position",
    "scope",
    "service_type",
    "page_type",
    "chunk_refs_str",
]


def _and_extra(combined_filter: str, extra_filter: str | None) -> str:
    """AND an extra OData clause onto an OR-combined source filter."""
    if not extra_filter:
        return combined_filter
    if not combined_filter:
        return extra_filter
    return f"{combined_filter} and {extra_filter}"


def combine_source_filters(
    source_filters: dict[str, str],
    extra_filter: str | None = None,
) -> str:
    """Combine source filters with an optional additional clause."""
    clauses = [f"({value})" for value in source_filters.values() if value]
    combined = f"({' or '.join(clauses)})" if clauses else ""
    return _and_extra(combined, extra_filter)


def split_source_ref(source_path: str) -> tuple[str, str]:
    """Split a source path into ``(context_id, title)`` matching raw chunks.

    Mirrors the wiki reader identity split: the first segment is the source
    scope (``context_id``); the remainder is the folder-relative ``title``.
    """
    path = (source_path or "").strip().lstrip("/")
    parts = path.split("/")
    folder = parts[0] if len(parts) > 1 else ""
    rel = path[len(folder) + 1:] if folder and path.startswith(folder + "/") else path
    return folder, rel


def _title_context_clause(title: str, context_id: str) -> str:
    """OData clause matching a raw chunk by ``title`` scoped to ``context_id``."""
    t = f"title eq '{_escape_odata(title)}'"
    if context_id:
        return f"({t} and context_id eq '{_escape_odata(context_id)}')"
    return f"({t} and (context_id eq null or context_id eq ''))"


def _chunk_key(chunk: KnowledgeChunk) -> str:
    """Stable identity for a chunk: its id, or a header-path fallback."""
    if chunk.chunk_id:
        return chunk.chunk_id
    return f"{chunk.source}|{chunk.title}|{chunk.header1}|{chunk.header2}|{chunk.header3}"


def fuse_with_rrf(
    ranked_lists: list[list[KnowledgeChunk]],
    k: int = _RRF_K,
) -> list[KnowledgeChunk]:
    """Fuse ranked retriever lists using Reciprocal Rank Fusion."""
    info: dict[str, KnowledgeChunk] = {}
    rank_maps: list[dict[str, int]] = []
    for results in ranked_lists:
        rmap: dict[str, int] = {}
        for i, chunk in enumerate(results):
            key = _chunk_key(chunk)
            if key not in rmap:
                rmap[key] = i + 1  # 1-indexed rank
            info.setdefault(key, chunk)
        rank_maps.append(rmap)

    fused: list[KnowledgeChunk] = []
    for key, chunk in info.items():
        score = 0.0
        for rmap in rank_maps:
            rank = rmap.get(key)
            if rank:
                score += 1.0 / (k + rank)
        chunk.rerank_score = score
        fused.append(chunk)
    fused.sort(key=lambda c: c.rerank_score, reverse=True)
    return fused


class SearchClient:
    """Search wrapper using Azure AI Search SDK clients."""

    def __init__(
        self, settings: Callable[[str, str], str] = cfg
    ) -> None:
        self._endpoint = settings("AI_SEARCH_BASE_URL", "").rstrip("/")
        self._index = settings("AI_SEARCH_INDEX", "")
        self._indexer_name = settings("AI_SEARCH_INDEXER", "")
        self._knowledge_base_name = settings("AI_SEARCH_KNOWLEDGE_BASE", "")
        self._knowledge_source_name = settings("AI_SEARCH_KNOWLEDGE_SOURCE", "")
        self._top_k = int(settings("AI_SEARCH_TOPK", "5"))
        self._candidate_top_k = max(
            self._top_k,
            int(settings("AI_SEARCH_CANDIDATE_TOPK", str(max(self._top_k * 4, 20)))),
        )
        self._credential = get_credential()
        self._kb_client = KnowledgeBaseRetrievalClient(
            self._endpoint,
            credential=self._credential,
            knowledge_base_name=self._knowledge_base_name,
        )
        self._search_client = AzureSearchClient(
            endpoint=self._endpoint,
            index_name=self._index,
            credential=self._credential,
        )
        self._indexer_client = SearchIndexerClient(
            endpoint=self._endpoint,
            credential=self._credential,
        )

    @property
    def top_k(self) -> int:
        """The configured top-k result limit."""
        return self._top_k

    async def run_indexer(self) -> str:
        """Start the knowledge indexer and wait for it to finish."""
        if not self._indexer_name:
            raise RuntimeError("AI_SEARCH_INDEXER not configured in App Configuration")
        try:
            await self._indexer_client.run_indexer(self._indexer_name)
        except HttpResponseError as exc:
            if exc.status_code == 409:
                logger.info("AI Search indexer is already running: %s", self._indexer_name)
            else:
                raise

        await self._wait_for_indexer()
        return "succeeded"

    async def _wait_for_indexer(self) -> None:
        deadline = time.monotonic() + _INDEXER_TIMEOUT_SECS
        while time.monotonic() < deadline:
            status = await self._indexer_client.get_indexer_status(self._indexer_name)
            last_result = status.last_result
            if last_result:
                if last_result.status == IndexerExecutionStatus.SUCCESS:
                    return
                if last_result.status in (
                    IndexerExecutionStatus.TRANSIENT_FAILURE,
                    IndexerExecutionStatus.RESET,
                ):
                    raise RuntimeError(
                        f"AI Search indexer finished with status {last_result.status}"
                    )
            await asyncio.sleep(_INDEXER_POLL_INTERVAL_SECS)

        raise TimeoutError(
            f"AI Search indexer did not complete within {_INDEXER_TIMEOUT_SECS} seconds"
        )

    @property
    def candidate_top_k(self) -> int:
        """Candidate depth used before fusion and final result capping."""
        return self._candidate_top_k

    async def agentic_search(
        self,
        query: str,
        source_filters: dict[str, str],
        extra_filter: str | None = None,
    ) -> list[KnowledgeChunk]:
        """Retrieve raw chunks via agentic (intent-aware) search.

        Returns un-expanded chunks.  The caller is responsible for
        deduplication and hierarchy expansion so that work is done once
        after all search strategies complete.
        """
        # Combine per-source filters into a single filter_add_on with OR so the
        # KB retrieval client executes one sub-search instead of N.
        combined_filter = combine_source_filters(source_filters, extra_filter)

        kb_params: list[KnowledgeSourceParams] = [
            SearchIndexKnowledgeSourceParams(
                knowledge_source_name=self._knowledge_source_name,
                include_references=True,
                include_reference_source_data=True,
                filter_add_on=combined_filter or None,
            )
        ]

        request = KnowledgeBaseRetrievalRequest(
            intents=[KnowledgeRetrievalSemanticIntent(search=query)],
            include_activity=True,
            knowledge_source_params=kb_params,
            max_output_size_in_tokens=_KB_MAX_OUTPUT_SIZE,
        )

        result = await self._kb_client.retrieve(
            retrieval_request=request,
        )

        # Parse source_data directly into KnowledgeChunk via aliases
        raw_refs: list[KnowledgeChunk] = []
        for ref in result.references or []:
            source_data = getattr(ref, "source_data", None) or {}
            raw_refs.append(KnowledgeChunk.model_validate(source_data))

        return raw_refs[: max(self._top_k, 1)]

    async def vector_search(
        self,
        query: str,
        source_filters: dict[str, str],
        top_k: int | None = None,
        extra_filter: str | None = None,
    ) -> list[KnowledgeChunk]:
        """Hybrid semantic + vector search mirroring the Go backend's SearchTopKRelatedDocuments.

        Combines all source filters into a single query for efficiency,
        filters by rerank score, and returns the top-k results sorted by
        relevance.
        """
        k = top_k or self._top_k

        vector_query = VectorizableTextQuery(
            text=query,
            k_nearest_neighbors=k,
            fields="text_vector",
        )

        combined_filter = combine_source_filters(source_filters, extra_filter)

        results = await self._search_client.search(
            search_text=query,
            filter=combined_filter or None,
            query_type=QueryType.SEMANTIC,
            top=k,
            select=_RETRIEVER_SELECT_FIELDS,
            vector_queries=[vector_query],
        )

        scored_chunks: list[tuple[float, KnowledgeChunk]] = []
        async for doc in results:
            chunk = KnowledgeChunk.model_validate(dict(doc))
            if chunk.rerank_score < _RERANK_SCORE_LOW_RELEVANCE_THRESHOLD:
                continue
            scored_chunks.append((chunk.rerank_score, chunk))

        # Sort by rerank score descending and limit to top-k
        scored_chunks.sort(key=lambda x: x[0], reverse=True)
        return [chunk for _, chunk in scored_chunks[:k]]

    async def keyword_search(
        self,
        query: str,
        source_filters: dict[str, str],
        top_k: int | None = None,
        extra_filter: str | None = None,
    ) -> list[KnowledgeChunk]:
        """Run sparse full-text keyword search and return BM25-ranked chunks."""
        k = top_k or self._top_k
        combined_filter = combine_source_filters(source_filters, extra_filter)

        results = await self._search_client.search(
            search_text=query,
            filter=combined_filter or None,
            query_type=QueryType.SIMPLE,
            top=k,
            select=_RETRIEVER_SELECT_FIELDS,
        )

        chunks: list[KnowledgeChunk] = []
        async for doc in results:
            chunks.append(KnowledgeChunk.model_validate(dict(doc)))
        return chunks

    async def fused_search(
        self,
        queries: list[str],
        source_filters: dict[str, str],
        *,
        extra_filter: str | None = None,
        use_agentic: bool = False,
    ) -> list[KnowledgeChunk]:
        """Run every enabled retriever for each query and fuse them with RRF.

        Shared by both retrieval tracks — ``extra_filter`` selects raw source
        chunks or wiki pages. Vector and keyword search always run; agentic
        search is opt-in. All query/retriever rankings contribute to one RRF
        result so duplicate hits accumulate support before the caller caps.
        """

        async def _ranked_for_query(query: str) -> list[list[KnowledgeChunk]]:
            coros: list = []
            if use_agentic:
                coros.append(
                    self.agentic_search(
                        query=query,
                        source_filters=source_filters,
                        extra_filter=extra_filter,
                    )
                )
            coros.append(
                self.vector_search(
                    query=query,
                    source_filters=source_filters,
                    top_k=self.candidate_top_k,
                    extra_filter=extra_filter,
                )
            )
            coros.append(
                self.keyword_search(
                    query=query,
                    source_filters=source_filters,
                    top_k=self.candidate_top_k,
                    extra_filter=extra_filter,
                )
            )

            results = await asyncio.gather(*coros, return_exceptions=True)
            ranked_lists: list[list[KnowledgeChunk]] = []
            for res in results:
                if isinstance(res, BaseException):
                    if isinstance(res, asyncio.CancelledError):
                        raise res
                    logger.warning("Retriever failed for query=%r: %s", query, res)
                    continue
                if res:
                    ranked_lists.append(res)

            return ranked_lists

        per_query = await asyncio.gather(*[_ranked_for_query(q) for q in queries[:3]])
        ranked_lists = [
            ranked
            for query_lists in per_query
            for ranked in query_lists
            if ranked
        ]
        if not ranked_lists:
            return []
        # Fuse all query/retriever rankings together. A chunk retrieved by
        # several query formulations accumulates support instead of keeping
        # whichever duplicate happened to appear first.
        return fuse_with_rrf(ranked_lists)

    @staticmethod
    def deduplicate_chunks(chunks: list[KnowledgeChunk]) -> list[KnowledgeChunk]:
        """Remove chunks whose header section is already covered by a broader expansion."""
        unique: list[KnowledgeChunk] = []
        seen_chunk_ids: set[str] = set()
        expanded_h1: set[str] = set()
        expanded_h2: set[str] = set()
        expanded_h3: set[str] = set()

        for chunk in chunks:
            if chunk.chunk_id in seen_chunk_ids:
                continue
            seen_chunk_ids.add(chunk.chunk_id)

            hierarchy = _detect_hierarchy(chunk.header1, chunk.header2, chunk.header3)

            if chunk.header1:
                h1_key = f"{chunk.source}|{chunk.title}|{chunk.header1}"
                if h1_key in expanded_h1 and hierarchy in (
                    HierarchyLevel.header2,
                    HierarchyLevel.header3,
                ):
                    continue
            if chunk.header1 and chunk.header2:
                h2_key = f"{chunk.source}|{chunk.title}|{chunk.header1}|{chunk.header2}"
                if h2_key in expanded_h2 and hierarchy == HierarchyLevel.header3:
                    continue
            if chunk.header1 and chunk.header2 and chunk.header3:
                h3_key = f"{chunk.source}|{chunk.title}|{chunk.header1}|{chunk.header2}|{chunk.header3}"
                if h3_key in expanded_h3:
                    continue

            unique.append(chunk)

            if hierarchy == HierarchyLevel.header1 and chunk.header1:
                expanded_h1.add(f"{chunk.source}|{chunk.title}|{chunk.header1}")
            elif (
                hierarchy == HierarchyLevel.header2 and chunk.header1 and chunk.header2
            ):
                expanded_h2.add(
                    f"{chunk.source}|{chunk.title}|{chunk.header1}|{chunk.header2}"
                )
            elif (
                hierarchy == HierarchyLevel.header3
                and chunk.header1
                and chunk.header2
                and chunk.header3
            ):
                expanded_h3.add(
                    f"{chunk.source}|{chunk.title}|{chunk.header1}|{chunk.header2}|{chunk.header3}"
                )

        return unique

    async def expand_by_hierarchy(self, chunk: KnowledgeChunk) -> KnowledgeChunk:
        """Fetch sibling chunks for a single ref and assemble content."""
        hierarchy_filter = _build_hierarchy_filter(
            title=chunk.title,
            context_id=chunk.source,
            header1=chunk.header1,
            header2=chunk.header2,
            header3=chunk.header3,
        )

        sibling_results = await self._search_client.search(
            search_text="*",
            filter=hierarchy_filter,
            top=_HIERARCHY_EXPANSION_TOP,
            order_by=["ordinal_position asc"],
            select=[
                "chunk_id",
                "title",
                "chunk",
                "context_id",
                "header_1",
                "header_2",
                "header_3",
            ],
        )

        content_parts: list[str] = [f"# {chunk.title}"]
        if chunk.content:
            content_parts.extend(["", "## Matched passage", chunk.content])
        section_parts: list[str] = []
        current_h1 = ""
        current_h2 = ""
        current_h3 = ""

        async for s in sibling_results:
            sibling = KnowledgeChunk.model_validate(dict(s))
            if sibling.chunk_id and sibling.chunk_id == chunk.chunk_id:
                continue

            if sibling.header1 != current_h1:
                current_h1, current_h2, current_h3 = sibling.header1, "", ""
                if current_h1:
                    section_parts.append(f"# {current_h1}")
            if sibling.header2 != current_h2:
                current_h2, current_h3 = sibling.header2, ""
                if current_h2:
                    section_parts.append(f"## {current_h2}")
            if sibling.header3 != current_h3:
                current_h3 = sibling.header3
                if current_h3:
                    section_parts.append(f"### {current_h3}")
            if sibling.content:
                section_parts.append(sibling.content)
        if section_parts:
            content_parts.extend(["", "## Surrounding section", *section_parts])

        # Resolve link via the source's link config
        source_def = get_knowledge_source(chunk.source)
        link = source_def.get_link(chunk.title) if source_def else ""

        return KnowledgeChunk(
            source=chunk.source,
            title=chunk.title,
            link=link,
            content="\n".join(content_parts),
            chunk_id=chunk.chunk_id,
            header1=chunk.header1,
            header2=chunk.header2,
            header3=chunk.header3,
        )

    async def backfill_wiki_sources(
        self,
        chunks: list[KnowledgeChunk],
        queries: list[str] | None = None,
        per_page: int = 8,
        max_refs: int = 48,
        max_total: int = 12,
        source_filter: str | None = None,
    ) -> list[KnowledgeChunk]:
        """Fetch raw source chunks referenced by retrieved wiki pages.

        *source_filter* (the tenant's combined ``context_id`` OR clause) scopes
        the routing to the tenant's sources.
        """
        seen_ids = {c.chunk_id for c in chunks if c.chunk_id}
        wanted: list[tuple[str, str]] = []  # (title, context_id) of raw source chunks
        for c in chunks:
            if c.page_type == "summary" and c.title:
                pairs = [(c.title, c.source or "")]
            elif c.page_type in ("entity", "concept"):
                pairs = []
                for r in list(c.chunk_refs)[:per_page]:
                    folder, rel = split_source_ref(r)
                    if rel:
                        pairs.append((rel, folder))
            else:
                continue
            for p in pairs:
                if p not in wanted:
                    wanted.append(p)
        wanted = wanted[:max_refs]
        if not wanted:
            return []

        pair_clause = " or ".join(_title_context_clause(t, ctx) for t, ctx in wanted)
        query_list = [q for q in (queries or []) if q.strip()]
        if not query_list:
            query_list = [
                " ".join(c.title for c in chunks[:3] if c.title).strip()
                or "Azure SDK documentation"
            ]
        # Retrieve semantically relevant RAW chunks only within the source
        # documents referenced by the selected wiki pages.
        ranked = await self.fused_search(
            query_list,
            {"wiki_source_refs": f"({pair_clause})"},
            extra_filter=_and_extra(NON_WIKI_FILTER, source_filter),
        )

        backfilled: list[KnowledgeChunk] = []
        per_doc_count: dict[tuple[str, str], int] = {}
        for chunk in ranked:
            if not chunk.chunk_id or chunk.chunk_id in seen_ids:
                continue
            key = (chunk.title, chunk.source)
            # Limit to 2 chunks per source doc.
            if per_doc_count.get(key, 0) >= 2:
                continue
            seen_ids.add(chunk.chunk_id)
            per_doc_count[key] = per_doc_count.get(key, 0) + 1
            backfilled.append(chunk)
            if len(backfilled) >= max_total:
                break
        logger.info(
            "backfill_wiki_sources: %d source chunk(s) from %d candidate source ref(s)",
            len(backfilled),
            len(wanted),
        )
        return backfilled

    async def close(self) -> None:
        await self._kb_client.close()
        await self._search_client.close()
        await self._indexer_client.close()
        close_method = getattr(self._credential, "close", None)
        if close_method is not None:
            result = close_method()
            if result is not None:
                await result


def _escape_odata(value: str) -> str:
    return value.replace("'", "''")


def _raw_chunk_filter(source_filter: str) -> str:
    raw_filter = "(page_type eq null or page_type eq '')"
    return f"({source_filter}) and {raw_filter}" if source_filter else raw_filter


class HierarchyLevel(str, Enum):
    """Hierarchy level of a knowledge chunk."""

    header1 = "header1"
    header2 = "header2"
    header3 = "header3"
    unknown = "unknown"


def _detect_hierarchy(header1: str, header2: str, header3: str) -> HierarchyLevel:
    """Determine the hierarchy level of a chunk (mirrors Go DetectChunkHierarchy)."""
    if header3:
        return HierarchyLevel.header3
    if header2 and header1:
        return HierarchyLevel.header2
    if header1:
        return HierarchyLevel.header1
    return HierarchyLevel.unknown


def _build_hierarchy_filter(
    *,
    title: str,
    context_id: str,
    header1: str,
    header2: str,
    header3: str,
) -> str:
    """Build hierarchy-scoped filter (mirrors Go CompleteChunkByHierarchy behavior)."""
    filters = [
        f"title eq '{_escape_odata(title)}'",
        f"context_id eq '{_escape_odata(context_id)}'",
    ]

    if header3:
        filters.append(f"header_1 eq '{_escape_odata(header1)}'")
        filters.append(f"header_2 eq '{_escape_odata(header2)}'")
        filters.append(f"header_3 eq '{_escape_odata(header3)}'")
    elif header2:
        filters.append(f"header_1 eq '{_escape_odata(header1)}'")
        filters.append(f"header_2 eq '{_escape_odata(header2)}'")
    elif header1:
        filters.append(f"header_1 eq '{_escape_odata(header1)}'")

    return " and ".join(filters)


_client: SearchClient | None = None


def get_search_client() -> SearchClient:
    """Return the shared SearchClient (created once on first call)."""
    global _client
    if _client is None:
        _client = SearchClient()
    return _client
