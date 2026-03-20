"""Knowledge retrieval tools for the Azure SDK QA Bot Agent.

Provides a single search tool that queries the Azure SDK knowledge base
via Azure AI Search and automatically expands each result by its header
hierarchy, returning full section context to the agent.
"""

from __future__ import annotations

import asyncio
import logging
from typing import Annotated

from config.tenant_config import get_tenant_config
from models.chat import Reference, SearchKnowledgeBaseResult
from tools import tool
from utils.azure_ai_search import get_search_client

logger = logging.getLogger(__name__)


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
            "Pick from the sources returned by ``route_tenant``. "
            "Example: ['typespec_docs', 'azure_api_guidelines']. "
            "If not provided, all sources configured for the tenant will be used.",
        ] = None,
        tenant_id: Annotated[
            str,
            "The tenant ID returned by ``route_tenant``.",
        ],
    ) -> SearchKnowledgeBaseResult:
        """Search the knowledge base and return results with full section context.

        Each result is automatically expanded by its header hierarchy so the
        content includes all sibling chunks under the same document section.
        """
        # Fall back to tenant-configured sources when none are specified
        if not sources:
            config = get_tenant_config(tenant_id)
            sources = [src.name for src in config.sources] if config else []

        search_client = get_search_client()

        # Resolve source → OData filter using tenant config
        source_filters = _resolve_source_filters(sources, tenant_id)

        knowledges = await search_client.agentic_search(
            query=query,
            sources=sources,
            source_filters=source_filters,
        )

        refs = [
            Reference(
                title=k.title,
                source=k.source,
                link=k.link,
                content=k.content,
                chunk_id=k.chunk_id,
                header1=k.header1,
                header2=k.header2,
                header3=k.header3,
            )
            for k in knowledges
        ]

        return SearchKnowledgeBaseResult(results=refs)


def _resolve_source_filters(sources: list[str], tenant_id: str) -> dict[str, str]:
    """Build source-name → OData-filter mapping.

    Each source always gets a base filter ``context_id eq '{name}'``.
    If the tenant defines a ``source_filter_overrides`` entry for that
    source, it is combined with ``and``.
    """
    config = get_tenant_config(tenant_id)
    overrides = config.source_filter if config else {}

    filters: dict[str, str] = {}
    for name in sources:
        default_filter = f"context_id eq '{name}'"
        extra = overrides.get(name)
        if extra:
            filters[name] = f"({default_filter}) and ({extra})"
        else:
            filters[name] = default_filter
    return filters