"""Knowledge retrieval tools for the Azure SDK QA Bot Agent.

Provides tools to search the Azure SDK knowledge base via Azure AI Search,
retrieve full document context, and list available knowledge sources.
Mirrors the Go backend's search service capabilities.
"""

from __future__ import annotations

import json
from typing import Annotated


class KnowledgeTools:
    """Tools for Azure SDK knowledge retrieval and search operations."""

    # Supported knowledge source categories (mirrors Go backend Source enum)
    KNOWLEDGE_SOURCES = [
        "typespec-docs",
        "azure-sdk-docs",
        "azure-rest-api-specs",
        "azure-sdk-for-python",
        "azure-sdk-for-net",
        "azure-sdk-for-java",
        "azure-sdk-for-js",
        "azure-sdk-for-go",
        "azure-sdk-design-guidelines",
        "azure-sdk-migration-guides",
        "azure-openapi-specs",
    ]

    def search_knowledge_base(
        self,
        *,
        query: Annotated[str, "The user's question or search query about Azure SDKs"],
        source: Annotated[list[str], "Optional list of knowledge sources to filter by, e.g. ['azure-sdk-for-python', 'azure-sdk-docs']"] = None,
        top_k: Annotated[int, "Maximum number of results to return"] = 5,
    ) -> str:
        """
        Search the Azure SDK knowledge base using semantic and vector search.

        Runs against the Azure AI Search index to find relevant documentation,
        guidelines, and code samples. Supports filtering by SDK language and
        knowledge source.

        Returns a JSON array of matching documents with title, content, source,
        and relevance score.
        """
        # TODO: implement with Azure AI Search client from utils.azure_ai_search
        return json.dumps({"error": "Not implemented yet"})

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

    def list_knowledge_sources(self) -> str:
        """
        List all available knowledge source categories that can be used to
        filter search results.

        Returns a JSON array of source identifiers.
        """
        return json.dumps(self.KNOWLEDGE_SOURCES, indent=2)