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

from config.tenant_config import get_tenant_config
from models.knowledge import Reference, SearchKnowledgeBaseResult
from tools import tool
from utils.azure_ai_search import get_search_client

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
            "Multiple search queries to run against the knowledge base. "
            "Queries should target the **documentation topic or solution** that "
            "would answer the user's question — NOT just restate the symptom. "
            "Think: 'what document or guide would solve this?' "
            "Good query strategy: "
            "1) A query targeting the **root cause or process** behind the issue. "
            "2) A query using **official terms, doc titles, or guide names** the "
            "knowledge base likely indexes. "
            "3) Optionally, a broader topic query for context. "
            "Example — user asks 'teammate cannot approve my PR in azure-rest-api-specs': "
            "  BAD: ['PR approval button not visible cannot approve pull request'] "
            "  GOOD: ['request access to Azure API and SDK repositories write access', "
            "          'azure-rest-api-specs CODEOWNERS approval branch protection rules', "
            "          'azure SDK partners GitHub team permissions onboarding'] "
            "At least 1, at most 3 queries.",
        ],
        sources: Annotated[
            list[str] | None,
            "List of knowledge source **names** to search. "
            "Pick from the sources exposed by the active skill or tenant context. "
            "Example: ['typespec_docs', 'azure_api_guidelines']. "
            "If not provided, all sources configured for the tenant will be used.",
        ] = None,
        tenant_id: Annotated[
            str,
            "The active tenant ID for the current conversation.",
        ],
        service_type: Annotated[
            str | None,
            "Filter results by Azure service plane. ALMOST ALWAYS use None. "
            "Only set when the question has an EXPLICIT, UNAMBIGUOUS signal: "
            "• 'management-plane' — PR has management-plane label, file path contains "
            "'resource-manager', or user literally says ARM/management-plane/RPaaS/RPSaaS. "
            "• 'data-plane' — PR has data-plane label, file path contains "
            "'data-plane', or user literally says data-plane. "
            "• None — everything else (SDK release, onboarding, pipelines, reviews, "
            "general questions, etc.). "
            "When in doubt, use None.",
        ] = None,
        search_mode: Annotated[
            str,
            "Search strategy to use. "
            "'quick' — vector search only, fast, good for straightforward "
            "factual lookups. "
            "'deep' — runs both agentic and vector search in parallel, "
            "better for complex or multi-faceted questions. "
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
            config = get_tenant_config(tenant_id)
            sources = [src.name for src in config.sources] if config else []

        search_client = get_search_client()

        # Resolve source → OData filter using tenant config
        source_filters = _resolve_source_filters(sources, tenant_id, service_type)

        use_deep = search_mode == SearchMode.deep.value

        # Cap queries to avoid excessive parallel searches
        capped_queries = queries[:3]

        # Build search tasks for all queries
        tasks: list = []
        for q in capped_queries:
            if use_deep:
                tasks.append(
                    search_client.agentic_search(
                        query=q,
                        source_filters=source_filters,
                    )
                )
            tasks.append(
                search_client.vector_search(
                    query=q,
                    source_filters=source_filters,
                )
            )

        results = await asyncio.gather(*tasks, return_exceptions=True)

        # Collect raw chunks, tolerating individual search failures
        raw_chunks: list = []
        for result in results:
            if isinstance(result, BaseException):
                logger.warning("Search failed: %s", result)
            else:
                raw_chunks.extend(result)

        logger.info(
            "Search completed: mode=%s, queries=%s, raw_chunks=%d",
            search_mode,
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
    tenant_config = get_tenant_config(tenant_id)
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


def _truncate_content(content: str | None) -> str | None:
    """Truncate content to _MAX_CONTENT_CHARS_PER_RESULT to control context size."""
    if not content or len(content) <= _MAX_CONTENT_CHARS_PER_RESULT:
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
