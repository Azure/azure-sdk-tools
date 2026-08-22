"""Knowledge retrieval tools for the Azure SDK QA Bot Agent."""

from __future__ import annotations

import asyncio
import logging
from enum import Enum
from typing import Annotated

from config.tenant_config import (
    SRC_WIKI_CONCEPT,
    SRC_WIKI_ENTITY,
    TenantID,
    get_knowledge_source,
    get_tenant_config,
)
from config.app_config import get as cfg
from models.knowledge import KnowledgeChunk, Reference, SearchKnowledgeBaseResult
from tools import tool
from utils.azure_ai_search import (
    NON_WIKI_FILTER,
    WIKI_FILTER,
    _escape_odata,
    get_search_client,
)

logger = logging.getLogger(__name__)


# Expanded content beyond this limit is truncated to control context size.
_MAX_CONTENT_CHARS_PER_RESULT = 3000
# Wiki search returns more references than raw search. Keep its serialized tool
# result below the hosted-agent streaming limit while retaining every ranked
# page and routed source.
_WIKI_PAGE_CONTENT_CHARS = 1800
_WIKI_SOURCE_CONTENT_CHARS = 1100

# Cross-document wiki pages; reachable only through wiki_search.
_WIKI_ONLY_SOURCES = (SRC_WIKI_ENTITY, SRC_WIKI_CONCEPT)

# Wiki pages kept as full evidence, and the next-ranked pages surfaced as
# titles only so the agent can see the neighbourhood it just missed.
_WIKI_TOP = 6
_WIKI_NEIGHBORS = 8
# Source chunks each kept page is routed back to, for grounded detail.
_WIKI_ROUTE_PER_PAGE = 8
_WIKI_ROUTE_MAX_REFS = 48
_WIKI_ROUTE_MAX_TOTAL = 12

# Shared by both retrieval tracks so the agent picks a strategy the same way.
_SEARCH_MODE_DESC = (
    "Search strategy to use. "
    "'quick' — dense + keyword retrieval, fast, good for straightforward "
    "factual lookups about a single feature, symbol, or process step "
    "(e.g., 'Which decorator marks an operation as long-running?'). "
    "Use 'quick' by default. "
    "'deep' — additionally runs agentic (intent-aware, multi-step) retrieval "
    "in parallel, better for complex questions that need cross-referencing "
    "multiple topics "
    "(e.g., 'How does adding a new API version interact with the SDK release "
    "and breaking-change review process?'). "
    "Use 'deep' only when the question genuinely spans multiple unrelated concepts. "
    "Default: 'quick'."
)


class SearchMode(str, Enum):
    """Search strategy for knowledge retrieval."""

    quick = "quick"
    """Dense + keyword retrieval — fast, good for straightforward factual lookups."""

    deep = "deep"
    """Adds agentic retrieval in parallel — better for complex or multi-faceted questions."""


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
            "The search combines semantic/vector and BM25 keyword retrieval. "
            "Provide the queries as a **progressive abstraction ladder** — "
            "start concrete and get more abstract with each query, so the set "
            "covers both exact-wording recall and conceptual-topic recall. "
            "QUERY 1 (REQUIRED) — the **most concrete** version: a full, "
            "standalone restatement of the user's question that KEEPS their "
            "concrete nouns (decorator names, model/property names, version "
            "numbers, error text, rule IDs, check names, config keys) verbatim. "
            "This is also the exact-term lookup path. Resolve follow-up context "
            "(replace 'it'/'this' with the real subject) and normalize obvious synonyms "
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
        search_mode: Annotated[str, _SEARCH_MODE_DESC] = "quick",
    ) -> SearchKnowledgeBaseResult:
        """Search the knowledge base with one or more queries and return results with full section context.

        Each query runs vector and keyword search in parallel, plus agentic
        search in deep mode, then all results are merged and deduplicated. Use
        multiple queries to cover different facets of the user's problem —
        the original question, related concepts, and potential solutions.
        """
        # Fall back to tenant-configured sources when none are specified
        sources = _raw_source_names(tenant_id, sources)

        search_client = get_search_client()

        # Resolve source → OData filter using tenant config
        source_filters = _resolve_source_filters(sources, tenant_id, service_type)

        use_deep = search_mode == SearchMode.deep.value
        capped_queries = queries[:3]

        raw_chunks = await search_client.fused_search(
            capped_queries,
            source_filters,
            extra_filter=NON_WIKI_FILTER,
            use_agentic=use_deep,
        )

        logger.info(
            "Search completed: mode=%s, queries=%s, raw_chunks=%d",
            search_mode,
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
        search_mode: Annotated[str, _SEARCH_MODE_DESC] = "quick",
    ) -> SearchKnowledgeBaseResult:
        """Search wiki pages, their routed source chunks, and adjacent page titles."""
        sources = _wiki_source_names(tenant_id, sources)
        search_client = get_search_client()
        source_filters = _resolve_source_filters(sources, tenant_id, None)
        capped_queries = queries[:3]

        raw = await search_client.fused_search(
            capped_queries,
            source_filters,
            extra_filter=WIKI_FILTER,
            use_agentic=search_mode == SearchMode.deep.value,
        )
        unique = [
            c for c in search_client.deduplicate_chunks(raw)
            if c.page_type in ("summary", "entity", "concept")
        ]
        page_hits = _deduplicate_wiki_pages(unique)
        page_hits.sort(key=lambda c: c.rerank_score, reverse=True)
        wiki_pages = page_hits[:_WIKI_TOP]
        neighbors = page_hits[_WIKI_TOP : _WIKI_TOP + _WIKI_NEIGHBORS]
        # Route each page to the SOURCE chunks it was built from (grounded detail).
        routed = await search_client.backfill_wiki_sources(
            wiki_pages,
            queries=capped_queries,
            per_page=_WIKI_ROUTE_PER_PAGE,
            max_refs=_WIKI_ROUTE_MAX_REFS,
            max_total=_WIKI_ROUTE_MAX_TOTAL,
            source_filter=_combined_source_filter(source_filters),
        )
        combined = wiki_pages + routed
        if not combined:
            logger.info("wiki_search: no wiki pages for queries=%s", capped_queries)
            return SearchKnowledgeBaseResult(results=[])
        expanded = await asyncio.gather(
            *[search_client.expand_by_hierarchy(c) for c in combined]
        )
        page_count = len(wiki_pages)
        results = _refs_from_expanded(
            expanded[:page_count],
            combined[:page_count],
            max_content_chars=_WIKI_PAGE_CONTENT_CHARS,
        )
        results.extend(
            _refs_from_expanded(
                expanded[page_count:],
                combined[page_count:],
                max_content_chars=_WIKI_SOURCE_CONTENT_CHARS,
            )
        )
        neighbor_ref = _neighbor_reference(neighbors)
        if neighbor_ref:
            results.append(neighbor_ref)
        logger.info(
            "wiki_search: mode=%s, %d page(s) + %d routed source(s) + %d neighbor(s) "
            "for queries=%s",
            search_mode, len(wiki_pages), len(routed), len(neighbors), capped_queries,
        )
        return SearchKnowledgeBaseResult(results=results)


def _tenant_source_names(tenant_id: str) -> list[str]:
    """All source names the tenant is configured for."""
    config = get_tenant_config(TenantID(tenant_id))
    return [src.name for src in config.sources] if config else []


def _raw_source_names(tenant_id: str, sources: list[str] | None) -> list[str]:
    """Source names for raw-chunk search.

    Wiki page sources carry their own ``context_id`` and never match a raw
    chunk, so they are dropped — unless that would empty the list, which would
    leave the search unfiltered.
    """
    names = sources or _tenant_source_names(tenant_id)
    kept = [n for n in names if n not in _WIKI_ONLY_SOURCES]
    return kept or names


def _wiki_source_names(tenant_id: str, sources: list[str] | None) -> list[str]:
    """Source names for ``wiki_search``.

    Topical scoping from the caller must not drop the tenant's cross-document
    wiki sources, which is what reduces wiki retrieval to summary pages only.
    """
    if not sources:
        return _tenant_source_names(tenant_id)
    extra = [
        n
        for n in _tenant_source_names(tenant_id)
        if n in _WIKI_ONLY_SOURCES and n not in sources
    ]
    return list(sources) + extra


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
        # ``sources`` reaches here straight from a model-authored tool call, so
        # the value is escaped before it goes into the filter.  Unregistered
        # names are kept rather than dropped: an escaped unknown name matches
        # nothing, whereas dropping every name would leave the search unfiltered.
        if not get_knowledge_source(source_name):
            logger.warning("Unknown knowledge source %r requested", source_name)
        filter_clauses = [f"context_id eq '{_escape_odata(source_name)}'"]
        if source_filter_overrides.get(source_name):
            filter_clauses.append(f"({source_filter_overrides[source_name]})")
        if service_type_filter:
            filter_clauses.append(service_type_filter)
        source_filters[source_name] = " and ".join(filter_clauses)
    return source_filters


def _combined_source_filter(source_filters: dict[str, str]) -> str | None:
    """Combine per-source filters into one parenthesized OR clause (or None)."""
    clauses = [f"({f})" for f in source_filters.values() if f]
    if not clauses:
        return None
    return "(" + " or ".join(clauses) + ")"


def _truncate_content(
    content: str | None,
    max_chars: int = _MAX_CONTENT_CHARS_PER_RESULT,
) -> str:
    """Truncate content to *max_chars* to control context size."""
    if not content:
        return ""
    if len(content) <= max_chars:
        return content
    return content[:max_chars] + "\n... [truncated]"


def _deduplicate_wiki_pages(chunks: list[KnowledgeChunk]) -> list[KnowledgeChunk]:
    """Keep the strongest chunk hit for each synthesized wiki page."""
    best: dict[tuple[str, str, str], KnowledgeChunk] = {}
    for chunk in chunks:
        key = (chunk.page_type, chunk.source, chunk.title)
        prior = best.get(key)
        if prior is None or chunk.rerank_score > prior.rerank_score:
            best[key] = chunk
    return list(best.values())


def _build_reference_title(
    document_title: str,
    header1: str | None,
    header2: str | None,
    header3: str | None,
) -> str:
    """Build a reference title from the deepest available header path."""
    parts = [part for part in (header1, header2, header3) if part]
    return " | ".join(parts) if parts else document_title


def _neighbor_reference(neighbors: list) -> Reference | None:
    """List adjacent wiki page titles for orientation."""
    seen: list[str] = []
    for c in neighbors:
        label = f"{c.title} ({c.page_type})"
        if c.title and label not in seen:
            seen.append(label)
    if not seen:
        return None
    return Reference(
        title="Related wiki pages",
        source="wiki",
        link="",
        content=(
            "Adjacent page titles for orientation only; their content was not "
            "returned and must not be treated as evidence.\n"
            + "\n".join(f"- {s}" for s in seen)
        ),
        score=0.0,
    )


def _refs_from_expanded(
    expanded: list,
    scored: list,
    *,
    max_content_chars: int = _MAX_CONTENT_CHARS_PER_RESULT,
) -> list[Reference]:
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
            content=_truncate_content(
                expanded[i].content,
                max_chars=max_content_chars,
            ),
            score=scored[i].rerank_score,
        )
        for i in range(len(expanded))
    ]
