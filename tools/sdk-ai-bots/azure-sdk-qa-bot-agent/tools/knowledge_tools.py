"""Knowledge retrieval tools for the Azure SDK QA Bot Agent.

Provides a single search tool that queries the Azure SDK knowledge base
via Azure AI Search and automatically expands each result by its header
hierarchy, returning full section context to the agent.
"""

from __future__ import annotations

import asyncio
import logging
from enum import Enum
from typing import Annotated

from config.tenant_config import TenantID, get_tenant_config
from config.app_config import get as cfg
from models.knowledge import Reference, SearchKnowledgeBaseResult
from tools import tool
from utils.azure_ai_search import get_search_client, fuse_with_rrf

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

        # Hybrid retrieval: run a dense (vector) and a sparse
        # (keyword/BM25) retriever per query and fuse their rankings with
        # Reciprocal Rank Fusion. The sparse path precisely matches exact
        # symbols (decorator/API names, labels) that dense recall misses.
        # ``deep`` mode adds the agentic retriever as a third fused list.
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
                search_client.vector_search(query=q, source_filters=source_filters)
            )
            weights.append(vector_weight)
            if enable_keyword:
                retriever_coros.append(
                    search_client.keyword_search(query=q, source_filters=source_filters)
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

        # Reorder by rerank_score descending and cap at top_k
        unique_chunks.sort(key=lambda c: c.rerank_score, reverse=True)
        top_k = search_client.top_k
        if len(unique_chunks) > top_k:
            logger.info(
                "Capping results from %d to %d (top_k)", len(unique_chunks), top_k
            )
            unique_chunks = unique_chunks[:top_k]

        logger.info(
            "After deduplication + rerank: %d chunks (from %d raw)",
            len(unique_chunks),
            len(raw_chunks),
        )

        # Lightweight 1-hop knowledge-graph expansion: pull the neighbours of any
        # retrieved wiki entity/concept node (via their related_slugs edges) into
        # the deep-read set. Disabled by default: measured to regress answer
        # quality on the perf eval (~-5.5pp total / -6.2pp typespec) — appending
        # tangential neighbour nodes dilutes answer completeness.
        if cfg("KB_ENABLE_GRAPH_EXPANSION", "false").lower() == "true":
            max_neighbors = int(cfg("KB_GRAPH_MAX_NEIGHBORS", "4"))
            neighbours = await search_client.expand_by_graph(
                unique_chunks, max_neighbors=max_neighbors
            )
            if neighbours:
                logger.info("Graph expansion added %d neighbour node(s)", len(neighbours))
                unique_chunks.extend(neighbours)

        expand_tasks = [
            search_client.expand_by_hierarchy(chunk) for chunk in unique_chunks
        ]
        expanded = await asyncio.gather(*expand_tasks)

        # Log final search results (mirrors Go backend's "Final Search Result" log)
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
