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
from models.chat import Reference, SearchKnowledgeBaseResult
from tools import tool
from utils.azure_ai_search import get_search_client

logger = logging.getLogger(__name__)


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
        query: Annotated[
            str,
            "The user's question or search query about Azure SDKs.",
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
            "The service type of the user's question. "
            "Must be one of: 'management-plane', 'data-plane', or None if unknown. "
            "Used to filter search results to the relevant service plane.",
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
        """Search the knowledge base and return results with full section context.

        Runs agentic search and vector search in parallel, then merges and
        deduplicates the results.  Each result is automatically expanded by
        its header hierarchy so the content includes all sibling chunks under
        the same document section.
        """
        # Fall back to tenant-configured sources when none are specified
        if not sources:
            config = get_tenant_config(tenant_id)
            sources = [src.name for src in config.sources] if config else []

        search_client = get_search_client()

        # Resolve source → OData filter using tenant config
        source_filters = _resolve_source_filters(sources, tenant_id, service_type)

        use_deep = search_mode == SearchMode.deep.value

        # Build search tasks based on search_mode
        tasks: list = []
        if use_deep:
            tasks.append(search_client.agentic_search(
                query=query,
                source_filters=source_filters,
            ))
        tasks.append(search_client.vector_search(
            query=query,
            source_filters=source_filters,
        ))

        results = await asyncio.gather(*tasks, return_exceptions=True)

        # Collect results, tolerating individual search failures
        knowledges: list = []
        for result in results:
            if isinstance(result, BaseException):
                logger.warning("Search failed: %s", result)
            else:
                knowledges.extend(result)

        # Deduplicate by expanded section identity
        seen_sections: set[tuple[str, str, str, str, str]] = set()
        unique_knowledges = []
        for k in knowledges:
            section_key = (k.source, k.title, k.header1, k.header2, k.header3)
            if section_key in seen_sections:
                continue
            seen_sections.add(section_key)
            unique_knowledges.append(k)

        refs = [
            Reference(
                title=k.header1 or k.title,
                source=k.source,
                link=k.link,
                content=k.content,
                chunk_id=k.chunk_id,
                header1=k.header1,
                header2=k.header2,
                header3=k.header3,
            )
            for k in unique_knowledges
        ]

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
        else ""
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