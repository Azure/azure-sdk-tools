"""Knowledge retrieval tools for the Azure SDK QA Bot Agent."""

from __future__ import annotations

import asyncio
import logging
from enum import Enum
from typing import Annotated

from config.tenant_config import TenantID, get_knowledge_source, get_tenant_config
from config.app_config import get as cfg
from models.knowledge import Reference, SearchKnowledgeBaseResult
from tools import tool
from utils.azure_ai_search import (
    NON_WIKI_FILTER,
    WIKI_FILTER,
    fuse_with_rrf,
    get_search_client,
)

logger = logging.getLogger(__name__)


# Expanded content beyond this limit is truncated to control context size.
_MAX_CONTENT_CHARS_PER_RESULT = 3000


class SearchMode(str, Enum):
    """Search strategy for knowledge retrieval."""

    quick = "quick"
    """Vector search only — fast, good for straightforward factual lookups."""

    deep = "deep"
    """Agentic + vector search in parallel — better for complex or multi-faceted questions."""


class ServiceType(str, Enum):
    """Azure service plane classification."""

    management_plane = "management-plane"
    data_plane = "data-plane"


class KnowledgeTools:
    """Tools for Azure SDK knowledge retrieval and search operations."""

    @tool
    async def search_knowledge_base(
        self,
        *,
        queries: Annotated[
            list[str],
            "1–3 search queries to run against the knowledge base. "
            "The knowledge base contains both documentation and historical "
            "Q&A; the index embeds the full body text, so complete natural "
            "sentences retrieve far better than stripped keyword fragments. "
            "Provide the queries as a **progressive abstraction ladder** — "
            "start concrete and get more abstract with each query, so the set "
            "covers both exact-wording recall and conceptual-topic recall. "
            "QUERY 1 (REQUIRED) — the **most concrete** version: a full, "
            "standalone restatement of the user's question that KEEPS their "
            "concrete nouns (decorator names, model/property names, version "
            "numbers, error text). Resolve follow-up context (replace "
            "'it'/'this' with the real subject) and normalize obvious synonyms "
            "(e.g. 'CI failure' → 'validation failure'). "
            "QUERY 2 (optional) — **more abstract**: drop the conversation-"
            "specific sample values (their own model name, exact version "
            "strings) but keep the underlying feature/concept terms. "
            "QUERY 3 (optional) — the **most abstract / core question**: "
            "distill to the underlying concept the docs are titled by, as one "
            "short question or topic phrase. "
            "At least 1, at most 3 queries.",
        ],
        sources: Annotated[
            list[str] | None,
            "List of knowledge source **names** to search. "
            "Pick from the sources exposed by the active skill or tenant context. "
            "Example: ['typespec_docs', 'azure_api_guidelines']. "
            "GUIDANCE: When seeking prescriptive guidance (the right pattern/template to use), "
            "prioritize authoritative sources (e.g., 'typespec_azure_docs', 'azure_resource_manager_rpc') "
            "OVER historical Q&A sources (e.g., 'static_typespec_qa'). "
            "Q&A sources often discuss workarounds and edge cases; for template selection, "
            "start with the official docs. "
            "If not provided, all sources configured for the tenant will be used.",
        ] = None,
        tenant_id: Annotated[
            str,
            "The active tenant ID for the current conversation.",
        ],
        service_type: Annotated[
            str | None,
            "Filter results by Azure service plane. ALMOST ALWAYS use None. "
            "Derive it ONLY from an explicit, unambiguous signal, checking in "
            "this order and stopping at the first match: "
            "1) PR label — azure-rest-api-specs PR carries a 'management-plane' "
            "or 'data-plane' label. "
            "2) File path — path contains 'resource-manager' → management-plane; "
            "path contains 'data-plane' → data-plane. "
            "3) Keyword — user literally says ARM / management-plane / RPaaS / "
            "RPSaaS → management-plane; user literally says data-plane → data-plane. "
            "4) Otherwise → None (SDK release, onboarding, pipelines, reviews, "
            "general questions, anything ambiguous). "
            "When in doubt, use None.",
        ] = None,
        search_mode: Annotated[
            str,
            "Search strategy to use. "
            "'quick' — vector search only, fast, good for straightforward "
            "factual lookups (e.g., 'What template emits x-ms-pageable?'). "
            "Use 'quick' by default. "
            "'deep' — runs both agentic and vector search in parallel, "
            "better for complex questions that need cross-referencing multiple topics "
            "(e.g., 'How do I use SDK versioning with spread properties AND deprecation policies?'). "
            "Use 'deep' only when the question genuinely spans multiple unrelated concepts. "
            "Default: 'quick'.",
        ] = "quick",
    ) -> SearchKnowledgeBaseResult:
        """Search the knowledge base with one or more queries and return results with full section context.

        Each query runs agentic and/or vector search in parallel, then all
        results are merged and deduplicated. Use multiple queries to cover
        different facets of the user's problem — the original question,
        related concepts, and potential solutions.
        """
        # Fall back to tenant-configured sources when none are specified
        if not sources:
            config = get_tenant_config(TenantID(tenant_id))
            sources = [src.name for src in config.sources] if config else []

        search_client = get_search_client()

        # Resolve source → OData filter using tenant config
        source_filters = _resolve_source_filters(sources, tenant_id, service_type)

        use_deep = search_mode == SearchMode.deep.value

        # Cap queries to avoid excessive parallel searches
        capped_queries = queries[:3]

        # Run configured retrievers per query and fuse their rankings with RRF.
        enable_keyword = cfg("KB_ENABLE_KEYWORD", "true").lower() == "true"
        rrf_k = int(cfg("KB_RRF_K", "60"))
        vector_weight = float(cfg("KB_RRF_VECTOR_WEIGHT", "1.0"))
        keyword_weight = float(cfg("KB_RRF_KEYWORD_WEIGHT", "1.0"))
        agentic_weight = float(cfg("KB_RRF_AGENTIC_WEIGHT", "1.0"))

        async def _fused_for_query(q: str) -> list:
            retriever_coros: list = []
            weights: list[float] = []
            if use_deep:
                retriever_coros.append(
                    search_client.agentic_search(query=q, source_filters=source_filters)
                )
                weights.append(agentic_weight)
            retriever_coros.append(
                search_client.vector_search(
                    query=q, source_filters=source_filters, extra_filter=NON_WIKI_FILTER,
                )
            )
            weights.append(vector_weight)
            if enable_keyword:
                retriever_coros.append(
                    search_client.keyword_search(
                        query=q, source_filters=source_filters, extra_filter=NON_WIKI_FILTER,
                    )
                )
                weights.append(keyword_weight)

            retriever_results = await asyncio.gather(
                *retriever_coros, return_exceptions=True
            )
            ranked_lists: list = []
            for res, weight in zip(retriever_results, weights):
                if isinstance(res, BaseException):
                    logger.warning("Retriever failed for query=%r: %s", q, res)
                    continue
                if res:
                    ranked_lists.append((res, weight))

            if not ranked_lists:
                return []
            # Fuse by RRF when more than one retriever contributed; otherwise
            # keep the single retriever's own (semantic-reranker) ordering.
            if len(ranked_lists) > 1:
                return fuse_with_rrf(ranked_lists, k=rrf_k)
            return list(ranked_lists[0][0])

        per_query_results = await asyncio.gather(
            *[_fused_for_query(q) for q in capped_queries]
        )

        # Collect fused chunks across all queries
        raw_chunks: list = []
        for chunk_list in per_query_results:
            raw_chunks.extend(chunk_list)

        logger.info(
            "Search completed: mode=%s, keyword=%s, queries=%s, raw_chunks=%d",
            search_mode,
            enable_keyword,
            capped_queries,
            len(raw_chunks),
        )

        # Deduplicate across all search results
        unique_chunks = search_client.deduplicate_chunks(raw_chunks)

        # This tool returns raw source chunks only; wiki pages are retrieved separately.
        unique_chunks = [c for c in unique_chunks if not c.page_type]

        # Reorder by rerank_score, then select the final top_k.
        unique_chunks.sort(key=lambda c: c.rerank_score, reverse=True)
        top_k = search_client.top_k
        if len(unique_chunks) > top_k:
            logger.info("Capping results from %d to %d (top_k)", len(unique_chunks), top_k)
            unique_chunks = unique_chunks[:top_k]

        logger.info(
            "After deduplication + rerank: %d chunks (from %d raw)",
            len(unique_chunks),
            len(raw_chunks),
        )

        expand_tasks = [
            search_client.expand_by_hierarchy(chunk) for chunk in unique_chunks
        ]
        expanded = await asyncio.gather(*expand_tasks)

        # Log final search results.
        logger.info("=========Final Search Result=========")
        refs = [
            Reference(
                title=_build_reference_title(
                    expanded[i].title,
                    expanded[i].header1,
                    expanded[i].header2,
                    expanded[i].header3,
                ),
                source=expanded[i].source,
                link=expanded[i].link,
                content=_truncate_content(expanded[i].content),
                score=unique_chunks[i].rerank_score,
            )
            for i in range(len(expanded))
        ]
        for i, ref in enumerate(refs):
            logger.info(
                "Result [%d] score=%.2f, source=%s, title=%s, link=%s, content_len=%d",
                i + 1,
                ref.score,
                ref.source,
                ref.title,
                ref.link,
                len(ref.content or ""),
            )
        logger.info(
            "===================================== total=%d results",
            len(refs),
        )

        return SearchKnowledgeBaseResult(results=refs)

    @tool
    async def grep_chunks(
        self,
        *,
        query: Annotated[
            str,
            "A single literal/keyword query to match EXACTLY inside SOURCE "
            "document chunks — the tool's strength is exact identifiers: "
            "decorator names (`@added`), type/model names, error text, linter "
            "rule IDs, property names. Pack a few synonyms into one query with "
            "spaces (BM25 OR). Use this instead of `search_knowledge_base` when "
            "you know the exact term/string to find.",
        ],
        tenant_id: Annotated[str, "The active tenant ID for the current conversation."],
        sources: Annotated[
            list[str] | None,
            "Optional list of knowledge source names to scope the search. "
            "If omitted, all sources configured for the tenant are used.",
        ] = None,
    ) -> SearchKnowledgeBaseResult:
        """Literal keyword search over raw source document chunks."""
        if not sources:
            config = get_tenant_config(TenantID(tenant_id))
            sources = [src.name for src in config.sources] if config else []
        search_client = get_search_client()
        source_filters = _resolve_source_filters(sources, tenant_id, None)
        hits = await search_client.keyword_search(
            query=query, source_filters=source_filters, extra_filter=NON_WIKI_FILTER
        )
        unique = [c for c in search_client.deduplicate_chunks(hits) if not c.page_type]
        unique = unique[: search_client.top_k]
        if not unique:
            return SearchKnowledgeBaseResult(results=[])
        expanded = await asyncio.gather(
            *[search_client.expand_by_hierarchy(c) for c in unique]
        )
        return SearchKnowledgeBaseResult(results=_refs_from_expanded(expanded, unique))

    @tool
    async def wiki_search(
        self,
        *,
        queries: Annotated[
            list[str],
            "1-3 queries for the curated WIKI layer: per-document SUMMARY pages, "
            "per-symbol ENTITY pages (decorators/APIs/types), per-topic CONCEPT "
            "pages. Use symbol/concept names or short topic phrases. Returns the "
            "top pages' full synthesized content PLUS the source-document chunks "
            "they were built from — enough to answer most conceptual/overview "
            "questions in one call.",
        ],
        tenant_id: Annotated[str, "The active tenant ID for the current conversation."],
        sources: Annotated[
            list[str] | None,
            "Optional list of knowledge source names to scope the search. "
            "If omitted, all sources configured for the tenant are used.",
        ] = None,
    ) -> SearchKnowledgeBaseResult:
        """Search wiki pages and return their routed source chunks."""
        if not sources:
            config = get_tenant_config(TenantID(tenant_id))
            sources = [src.name for src in config.sources] if config else []
        search_client = get_search_client()
        source_filters = _resolve_source_filters(sources, tenant_id, None)
        rrf_k = int(cfg("KB_RRF_K", "60"))
        enable_keyword = cfg("KB_ENABLE_KEYWORD", "true").lower() == "true"

        async def _fused(q: str) -> list:
            coros = [
                search_client.vector_search(
                    query=q, source_filters=source_filters, extra_filter=WIKI_FILTER
                )
            ]
            weights = [1.0]
            if enable_keyword:
                coros.append(
                    search_client.keyword_search(
                        query=q, source_filters=source_filters, extra_filter=WIKI_FILTER
                    )
                )
                weights.append(1.0)
            res = await asyncio.gather(*coros, return_exceptions=True)
            ranked = [(r, w) for r, w in zip(res, weights) if not isinstance(r, BaseException) and r]
            if not ranked:
                return []
            return fuse_with_rrf(ranked, k=rrf_k) if len(ranked) > 1 else list(ranked[0][0])

        per_query = await asyncio.gather(*[_fused(q) for q in queries[:3]])
        raw: list = []
        for lst in per_query:
            raw.extend(lst)
        unique = [
            c for c in search_client.deduplicate_chunks(raw)
            if c.page_type in ("summary", "entity", "concept", "synthesis")
        ]
        unique.sort(key=lambda c: c.rerank_score, reverse=True)
        wiki_pages = unique[: int(cfg("KB_WIKI_TOP", "6"))]
        # Route each page to the SOURCE chunks it was built from (grounded detail).
        routed = await search_client.backfill_wiki_sources(
            wiki_pages,
            per_page=int(cfg("KB_WIKI_ROUTE_PER_PAGE", "3")),
            max_total=int(cfg("KB_WIKI_ROUTE_MAX_TOTAL", "12")),
        )
        combined = wiki_pages + routed
        if not combined:
            logger.info("wiki_search: no wiki pages for queries=%s", queries[:3])
            return SearchKnowledgeBaseResult(results=[])
        expanded = await asyncio.gather(
            *[search_client.expand_by_hierarchy(c) for c in combined]
        )
        logger.info(
            "wiki_search: %d page(s) + %d routed source(s) for queries=%s",
            len(wiki_pages), len(routed), queries[:3],
        )
        return SearchKnowledgeBaseResult(results=_refs_from_expanded(expanded, combined))

    @tool
    async def wiki_read_page(
        self,
        *,
        titles: Annotated[
            list[str],
            "1-5 wiki page titles (exactly as returned by `wiki_search`) to read "
            "in full. Read several related pages at once for broad coverage.",
        ],
        tenant_id: Annotated[str, "The active tenant ID for the current conversation."],
        sources: Annotated[
            list[str] | None,
            "Optional list of knowledge source names to scope the lookup. "
            "If omitted, all sources configured for the tenant are used.",
        ] = None,
    ) -> SearchKnowledgeBaseResult:
        """Read full wiki pages and list their source document refs."""
        if not sources:
            config = get_tenant_config(TenantID(tenant_id))
            sources = [src.name for src in config.sources] if config else []
        search_client = get_search_client()
        chunks = await search_client.fetch_by_title(titles[:5], WIKI_FILTER)
        if not chunks:
            return SearchKnowledgeBaseResult(results=[])
        # Group multi-chunk pages by title; concat body + collect source refs.
        by_title: dict[str, list] = {}
        for c in chunks:
            by_title.setdefault(c.title, []).append(c)
        results: list[Reference] = []
        for title, parts in by_title.items():
            body = "\n".join(p.content for p in parts if p.content)
            refs: list[str] = []
            for p in parts:
                for r in p.chunk_refs:
                    if r and r not in refs:
                        refs.append(r)
            page_type = parts[0].page_type
            source_def = get_knowledge_source(parts[0].source)
            link = source_def.get_link(title) if source_def else ""
            if refs:
                body += "\n\nSources (drill with wiki_read_source_doc(source_ref=...)):\n" + \
                    "\n".join(f"- {r}" for r in refs[:10])
            results.append(
                Reference(
                    title=title,
                    source=page_type,
                    link=link,
                    content=_truncate_content(body),
                    score=0.0,
                )
            )
        logger.info("wiki_read_page: read %d page(s) for titles=%s", len(results), titles[:5])
        return SearchKnowledgeBaseResult(results=results)

    @tool
    async def wiki_read_source_doc(
        self,
        *,
        source_ref: Annotated[
            str,
            "A source document reference exactly as listed in a wiki page's "
            "`Sources` section (from `wiki_read_page`). Reads that original "
            "source document to drill into details the wiki page omits.",
        ],
        tenant_id: Annotated[str, "The active tenant ID for the current conversation."],
        query: Annotated[
            str | None,
            "Optional keyword(s) to focus the source doc on a specific detail "
            "(BM25). Omit to read the document from the top.",
        ] = None,
    ) -> SearchKnowledgeBaseResult:
        """Read an original source document referenced by a wiki page."""
        search_client = get_search_client()
        chunks = await search_client.fetch_by_title(
            [source_ref], NON_WIKI_FILTER, top=60, keyword=query
        )
        chunks = [c for c in chunks if not c.page_type]
        if not chunks:
            return SearchKnowledgeBaseResult(results=[])
        body = "\n".join(c.content for c in chunks if c.content)
        source_def = get_knowledge_source(chunks[0].source)
        link = source_def.get_link(source_ref) if source_def else ""
        return SearchKnowledgeBaseResult(
            results=[
                Reference(
                    title=source_ref,
                    source=chunks[0].source,
                    link=link,
                    content=_truncate_content(body),
                    score=0.0,
                )
            ]
        )


def _resolve_source_filters(
    sources: list[str],
    tenant_id: str,
    service_type: str | None = None,
) -> dict[str, str]:
    """Build source-name → OData-filter mapping.

    Each source gets a base ``context_id`` filter.  Tenant-level overrides
    and the service-type clause are layered on with ``and``.
    """
    tenant_config = get_tenant_config(TenantID(tenant_id))
    source_filter_overrides = tenant_config.source_filter if tenant_config else {}

    valid_service_types = {t.value for t in ServiceType}
    service_type_filter = (
        f"(service_type eq '{service_type}' or service_type eq null)"
        if service_type and service_type in valid_service_types
        else None
    )

    source_filters: dict[str, str] = {}
    for source_name in sources:
        filter_clauses = [f"context_id eq '{source_name}'"]
        if source_filter_overrides.get(source_name):
            filter_clauses.append(f"({source_filter_overrides[source_name]})")
        if service_type_filter:
            filter_clauses.append(service_type_filter)
        source_filters[source_name] = " and ".join(filter_clauses)
    return source_filters


def _truncate_content(content: str | None) -> str:
    """Truncate content to _MAX_CONTENT_CHARS_PER_RESULT to control context size."""
    if not content:
        return ""
    if len(content) <= _MAX_CONTENT_CHARS_PER_RESULT:
        return content
    return content[:_MAX_CONTENT_CHARS_PER_RESULT] + "\n... [truncated]"


def _build_reference_title(
    document_title: str,
    header1: str | None,
    header2: str | None,
    header3: str | None,
) -> str:
    """Build a reference title from the deepest available header path."""
    parts = [part for part in (header1, header2, header3) if part]
    return " | ".join(parts) if parts else document_title


def _refs_from_expanded(expanded: list, scored: list) -> list[Reference]:
    """Build References from expanded chunks, taking scores from *scored*."""
    return [
        Reference(
            title=_build_reference_title(
                expanded[i].title,
                expanded[i].header1,
                expanded[i].header2,
                expanded[i].header3,
            ),
            source=expanded[i].source,
            link=expanded[i].link,
            content=_truncate_content(expanded[i].content),
            score=scored[i].rerank_score,
        )
        for i in range(len(expanded))
    ]
