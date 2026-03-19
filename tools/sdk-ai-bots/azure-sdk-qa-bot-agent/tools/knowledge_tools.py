"""Knowledge retrieval tools for the Azure SDK QA Bot Agent.

Provides tools to search the Azure SDK knowledge base via Azure AI Search,
and retrieve full document context. The agent decides which sources to query
based on the source list returned by the ``route_tenant`` tool, then passes
source names here. This tool resolves the corresponding filters from the
tenant config / knowledge source registry.
"""

from __future__ import annotations

import json
from typing import Annotated

from config.tenant_config import get_knowledge_source, get_tenant_config
from models.chat import SearchKnowledgeBaseResult
from tools import tool


class KnowledgeTools:
    """Tools for Azure SDK knowledge retrieval and search operations."""

    def __init__(self, tenant_id: str | None = None) -> None:
        self._tenant_id = tenant_id

    def set_tenant_id(self, tenant_id: str) -> None:
        """Update the active tenant (set after routing)."""
        self._tenant_id = tenant_id

    @tool
    def search_knowledge_base(
        self,
        *,
        query: Annotated[
            str,
            "The user's question or search query about Azure SDKs.",
        ],
        sources: Annotated[
            list[str],
            "List of knowledge source **names** to search. "
            "Pick from the sources returned by ``route_tenant``. "
            "Example: ['typespec_docs', 'azure_api_guidelines'].",
        ],
        top_k: Annotated[int, "Maximum number of results to return."] = 5,
    ) -> SearchKnowledgeBaseResult:
        """Search the Azure SDK knowledge base using semantic and vector search.

        For each source name provided, the tool resolves an optional OData
        filter (from the tenant config or the global source registry) and
        queries the Azure AI Search index. Results are merged and returned
        as a JSON array of matching documents with title, content, source,
        and relevance score.
        """
        # Build effective source → filter mapping
        resolved: list[dict[str, str]] = []
        tenant_cfg = (
            get_tenant_config(self._tenant_id) if self._tenant_id else None
        )

        for name in sources:
            effective_filter = ""
            # 1. Check tenant-level override
            if tenant_cfg is not None:
                effective_filter = tenant_cfg.get_source_filter(name)
            # 2. Fallback to global registry default
            if not effective_filter:
                src = get_knowledge_source(name)
                effective_filter = src.filter if src else ""

            resolved.append({"source": name, "filter": effective_filter})

        # TODO: implement with Azure AI Search client from utils.azure_ai_search
        # For now, return the resolved source/filter pairs for visibility.
        return json.dumps(
            {
                "status": "not_implemented",
                "query": query,
                "top_k": top_k,
                "resolved_sources": resolved,
            },
            ensure_ascii=False,
        )

    def get_document_context(
        self,
        *,
        document_id: Annotated[str, "The ID of the document or chunk to retrieve full context for"],
    ) -> str:
        """
        Retrieve the full context for a specific document by expanding related
        chunks hierarchically.

        Given a chunk ID from a prior search result, this fetches all sibling
        chunks under the same document heading to provide complete context.

        Returns the assembled document content as a JSON object with title,
        source, link, and full content.
        """
        # TODO: implement chunk expansion logic via Azure AI Search
        return json.dumps({"error": "Not implemented yet"})