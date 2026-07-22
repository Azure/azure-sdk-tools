"""Azure AI Search SDK client helpers for knowledge retrieval.

This module uses the Azure AI Search Python SDK (not raw REST).  Each search
result is automatically expanded by its header hierarchy so the agent gets
full section context in a single call.  Sibling queries run concurrently
via ``asyncio.gather`` for fast response times.

Two search strategies are provided and run in parallel:
  - **Agentic search** – uses the KnowledgeBaseRetrievalClient for
    intent-aware multi-step retrieval.
  - **Vector search** – hybrid semantic + vector query.
"""

from __future__ import annotations

import asyncio
import logging
import re
from enum import Enum

from azure.search.documents.aio import SearchClient as AzureSearchClient
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

# Chunks below this rerank score are considered low-relevance and dropped.
_RERANK_SCORE_LOW_RELEVANCE_THRESHOLD = 2.0

# WeKnora-style two-track separation: the chunk retrievers search ONLY raw
# source chunks (``page_type`` null/empty); the wiki retriever searches ONLY
# generated wiki pages. Wiki pages never enter the source-chunk ranked pool, so
# they can no longer displace authoritative source chunks.
NON_WIKI_FILTER = "(page_type eq null or page_type eq '')"
WIKI_FILTER = "(page_type eq 'summary' or page_type eq 'entity' or page_type eq 'concept' or page_type eq 'synthesis')"

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
    return f"({combined_filter}) and {extra_filter}"


def _chunk_key(chunk: "KnowledgeChunk") -> str:
    """Stable identity for a chunk: its id, or a header-path fallback."""
    if chunk.chunk_id:
        return chunk.chunk_id
    return f"{chunk.source}|{chunk.title}|{chunk.header1}|{chunk.header2}|{chunk.header3}"


def fuse_with_rrf(
    ranked_lists: "list[tuple[list[KnowledgeChunk], float]]",
    k: int = 60,
) -> "list[KnowledgeChunk]":
    """Reciprocal Rank Fusion of several ranked retriever result lists.

    ``score = sum_over_retrievers( weight / (k + rank) )`` where ``rank`` is the
    1-indexed position of the chunk in that retriever's output. Fuses on
    **rank**, so heterogeneous score scales (semantic reranker vs BM25) combine
    cleanly. The fused score is written onto each chunk's ``rerank_score`` so the
    existing downstream sort/cap works unchanged. Each chunk appears once; the
    first retriever that surfaced it supplies its metadata.
    """
    info: dict[str, KnowledgeChunk] = {}
    rank_maps: list[tuple[dict[str, int], float]] = []
    for results, weight in ranked_lists:
        rmap: dict[str, int] = {}
        for i, chunk in enumerate(results):
            key = _chunk_key(chunk)
            if key not in rmap:
                rmap[key] = i + 1  # 1-indexed rank
            info.setdefault(key, chunk)
        rank_maps.append((rmap, weight))

    fused: list[KnowledgeChunk] = []
    for key, chunk in info.items():
        score = 0.0
        for rmap, weight in rank_maps:
            rank = rmap.get(key)
            if rank:
                score += weight / (k + rank)
        chunk.rerank_score = score
        fused.append(chunk)
    fused.sort(key=lambda c: c.rerank_score, reverse=True)
    return fused


_MMR_TOKEN_RE = re.compile(r"[a-z0-9@._]+")


def _token_set(chunk: "KnowledgeChunk") -> set:
    """Lower-cased token set of a chunk's text (for Jaccard diversity)."""
    text = f"{chunk.title} {chunk.header1} {chunk.content}".lower()
    return set(_MMR_TOKEN_RE.findall(text))


def _jaccard(a: set, b: set) -> float:
    if not a or not b:
        return 0.0
    inter = len(a & b)
    if inter == 0:
        return 0.0
    return inter / len(a | b)


def mmr_select(
    chunks: "list[KnowledgeChunk]",
    k: int,
    lambda_: float = 0.7,
) -> "list[KnowledgeChunk]":
    """Maximal Marginal Relevance selection (WeKnora-style, Jaccard diversity).

    Picks ``k`` chunks balancing relevance (normalized ``rerank_score``) against
    redundancy (max Jaccard token overlap with the already-selected set):
    ``mmr = lambda*rel - (1-lambda)*max_sim``. Diversifies the final result set so
    a flood of similar pages (e.g. many short synthesized wiki pages) can't crowd
    out substantive, complementary content. No embeddings needed.
    """
    if len(chunks) <= k:
        return chunks
    scores = [c.rerank_score for c in chunks]
    lo, hi = min(scores), max(scores)
    rng = (hi - lo) or 1.0
    rel = {id(c): (c.rerank_score - lo) / rng for c in chunks}
    toks = {id(c): _token_set(c) for c in chunks}

    pool = list(chunks)
    selected: list[KnowledgeChunk] = []
    while pool and len(selected) < k:
        best = None
        best_score = float("-inf")
        for c in pool:
            max_sim = 0.0
            for s in selected:
                j = _jaccard(toks[id(c)], toks[id(s)])
                if j > max_sim:
                    max_sim = j
            mmr = lambda_ * rel[id(c)] - (1.0 - lambda_) * max_sim
            if mmr > best_score:
                best_score = mmr
                best = c
        selected.append(best)
        pool.remove(best)
    return selected


class SearchClient:
    """Search wrapper using Azure AI Search SDK clients."""

    def __init__(self) -> None:
        self._endpoint = (cfg("AI_SEARCH_BASE_URL", "") or "").rstrip("/")
        self._index = cfg("AI_SEARCH_INDEX", "")
        self._knowledge_base_name = cfg("AI_SEARCH_KNOWLEDGE_BASE", "")
        self._knowledge_source_name = cfg("AI_SEARCH_KNOWLEDGE_SOURCE", "")
        self._top_k = int(cfg("AI_SEARCH_TOPK", "5"))
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

    @property
    def top_k(self) -> int:
        """The configured top-k result limit."""
        return self._top_k

    async def agentic_search(
        self,
        query: str,
        source_filters: dict[str, str],
    ) -> list[KnowledgeChunk]:
        """Retrieve raw chunks via agentic (intent-aware) search.

        Returns un-expanded chunks.  The caller is responsible for
        deduplication and hierarchy expansion so that work is done once
        after all search strategies complete.

        The per-source filters are combined into a single OData expression
        so the KB retrieval client executes one sub-search instead of N.
        """
        # Combine per-source filters into a single filter_add_on with OR
        # so the KB client performs one retrieval pass instead of N.
        combined_filter = " or ".join(f"({f})" for f in source_filters.values() if f)

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
        relevance. ``extra_filter`` (e.g. the wiki/non-wiki page-type gate) is
        AND-ed onto the OR-combined source filter.
        """
        k = top_k or self._top_k

        vector_query = VectorizableTextQuery(
            text=query,
            k_nearest_neighbors=k,
            fields="text_vector",
        )

        select_fields = _RETRIEVER_SELECT_FIELDS

        # Combine per-source filters into a single OData expression with OR
        combined_filter = " or ".join(f"({f})" for f in source_filters.values() if f)
        combined_filter = _and_extra(combined_filter, extra_filter)

        results = await self._search_client.search(
            search_text=query,
            filter=combined_filter or None,
            query_type=QueryType.SEMANTIC,
            top=k,
            select=select_fields,
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
        """Sparse full-text (BM25) keyword search.

        Complements the dense ``vector_search`` for precise matching of exact
        symbols (decorator/API names, labels, error text) that vector search
        recalls unreliably. Returns chunks ordered by BM25 relevance; the
        caller fuses this ranking with the vector ranking via
        :func:`fuse_with_rrf`. No semantic reranker is applied here (the reranker
        score lives on the vector path); RRF fuses on rank, not raw score.
        """
        k = top_k or self._top_k
        select_fields = _RETRIEVER_SELECT_FIELDS
        combined_filter = " or ".join(f"({f})" for f in source_filters.values() if f)
        combined_filter = _and_extra(combined_filter, extra_filter)

        results = await self._search_client.search(
            search_text=query,
            filter=combined_filter or None,
            query_type=QueryType.SIMPLE,
            top=k,
            select=select_fields,
        )

        chunks: list[KnowledgeChunk] = []
        async for doc in results:
            chunks.append(KnowledgeChunk.model_validate(dict(doc)))
        return chunks

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
        current_h1 = ""
        current_h2 = ""
        current_h3 = ""

        async for s in sibling_results:
            sibling = KnowledgeChunk.model_validate(dict(s))

            if sibling.header1 != current_h1:
                current_h1, current_h2, current_h3 = sibling.header1, "", ""
                if current_h1:
                    content_parts.append(f"# {current_h1}")
            if sibling.header2 != current_h2:
                current_h2, current_h3 = sibling.header2, ""
                if current_h2:
                    content_parts.append(f"## {current_h2}")
            if sibling.header3 != current_h3:
                current_h3 = sibling.header3
                if current_h3:
                    content_parts.append(f"### {current_h3}")
            if sibling.content:
                content_parts.append(sibling.content)

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
        chunks: "list[KnowledgeChunk]",
        per_page: int = 3,
        max_total: int = 12,
    ) -> "list[KnowledgeChunk]":
        """Backfill the SOURCE document chunks behind retrieved wiki pages.

        A wiki page (summary/entity/concept/synthesis) is a distilled *router* to
        the documents it was built from — retrieving it should surface, not
        replace, that specific source content. For each wiki page in *chunks*,
        this fetches the raw source chunks it points to:

        * **summary** — its ``title`` IS the source doc's rel path, so the source
          is fetched by ``title`` match.
        * **entity / concept / synthesis** — its ``chunk_refs`` list the source
          docs it aggregates.

        Returns the newly-fetched raw chunks (de-duplicated against *chunks*),
        capped at *max_total*, so the caller can add them to the deep-read set.
        """
        seen_ids = {c.chunk_id for c in chunks if c.chunk_id}
        wanted_titles: list[str] = []
        for c in chunks:
            if c.page_type == "summary" and c.title:
                refs = [c.title]
            elif c.page_type in ("entity", "concept", "synthesis"):
                refs = list(c.chunk_refs)[:per_page]
            else:
                continue
            for r in refs:
                if r and r not in wanted_titles:
                    wanted_titles.append(r)
        wanted_titles = wanted_titles[:max_total]
        if not wanted_titles:
            return []

        title_clause = " or ".join(
            f"title eq '{_escape_odata(t)}'" for t in wanted_titles
        )
        # Only backfill RAW source chunks (never other wiki pages).
        combined_filter = f"({title_clause}) and (page_type eq null or page_type eq '')"

        results = await self._search_client.search(
            search_text="*",
            filter=combined_filter,
            top=max_total * 3,
            select=_RETRIEVER_SELECT_FIELDS,
        )

        backfilled: list[KnowledgeChunk] = []
        per_title_count: dict[str, int] = {}
        async for doc in results:
            chunk = KnowledgeChunk.model_validate(dict(doc))
            if not chunk.chunk_id or chunk.chunk_id in seen_ids:
                continue
            # keep at most 2 chunks per source doc to avoid one doc dominating
            if per_title_count.get(chunk.title, 0) >= 2:
                continue
            seen_ids.add(chunk.chunk_id)
            per_title_count[chunk.title] = per_title_count.get(chunk.title, 0) + 1
            backfilled.append(chunk)
            if len(backfilled) >= max_total:
                break
        logger.info(
            "backfill_wiki_sources: %d source chunk(s) from %d wiki ref(s)",
            len(backfilled),
            len(wanted_titles),
        )
        return backfilled

    async def close(self) -> None:
        await self._kb_client.close()
        await self._search_client.close()
        close_method = getattr(self._credential, "close", None)
        if close_method is not None:
            result = close_method()
            if result is not None:
                await result


def _escape_odata(value: str) -> str:
    return value.replace("'", "''")


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
