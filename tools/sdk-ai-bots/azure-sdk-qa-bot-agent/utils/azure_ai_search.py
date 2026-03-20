"""Azure AI Search SDK client helpers for knowledge retrieval.

This module uses the Azure AI Search Python SDK (not raw REST).  Each search
result is automatically expanded by its header hierarchy so the agent gets
full section context in a single call.  Sibling queries run concurrently
via ``asyncio.gather`` for fast response times.
"""

from __future__ import annotations

import asyncio
import logging

from azure.search.documents.aio import SearchClient as AzureSearchClient
from azure.search.documents.knowledgebases.aio import KnowledgeBaseRetrievalClient
from azure.search.documents.knowledgebases.models import (
    KnowledgeBaseRetrievalRequest,
    KnowledgeRetrievalMediumReasoningEffort,
    KnowledgeRetrievalSemanticIntent,
    SearchIndexKnowledgeSourceParams,
)
from utils.azure_credential import get_credential

from config.app_config import get as cfg
from config.tenant_config import get_knowledge_source
from models.knowledge import KnowledgeChunk

logger = logging.getLogger(__name__)


class SearchClient:
    """Search wrapper using Azure AI Search SDK clients."""

    def __init__(self) -> None:
        self._endpoint = (cfg("AI_SEARCH_BASE_URL", "") or "").rstrip("/")
        self._index = cfg("AI_SEARCH_INDEX", "")
        self._knowledge_base_name = cfg("AI_SEARCH_KNOWLEDGE_BASE")
        self._knowledge_source_name = cfg("AI_SEARCH_KNOWLEDGE_SOURCE")
        self._top_k = int(cfg("AI_SEARCH_TOPK"))
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

    async def agentic_search(
        self,
        query: str,
        sources: list[str],
        source_filters: dict[str, str],
    ) -> list[KnowledgeChunk]:
        """Retrieve references and expand each by header hierarchy.

        For every chunk returned by the knowledge-base retrieval, a sibling
        query fetches all chunks under the same header section so the agent
        receives full contextual content in a single call.
        """
        kb_params: list[SearchIndexKnowledgeSourceParams] = []
        for source_name in sources:
            filter_add_on = source_filters.get(source_name, "")
            kb_params.append(
                SearchIndexKnowledgeSourceParams(
                    knowledge_source_name=self._knowledge_source_name,
                    include_references=True,
                    include_reference_source_data=True,
                    filter_add_on=filter_add_on,
                )
            )

        request = KnowledgeBaseRetrievalRequest(
            intents=[KnowledgeRetrievalSemanticIntent(search=query)],
            include_activity=True,
            output_mode="extractiveData",
            retrieval_reasoning_effort=KnowledgeRetrievalMediumReasoningEffort(),
            knowledge_source_params=kb_params if kb_params else None,
            max_output_size=20000,
        )

        result = await self._kb_client.retrieve(
            retrieval_request=request,
        )

        # Parse source_data directly into KnowledgeChunk via aliases
        raw_refs: list[KnowledgeChunk] = []
        for ref in result.references or []:
            source_data = getattr(ref, "source_data", None) or {}
            raw_refs.append(KnowledgeChunk.model_validate(source_data))

        # Deduplicate: skip chunks whose header section is already covered
        # by a broader expansion earlier in the list.
        unique_refs: list[KnowledgeChunk] = []
        seen_chunk_ids: set[str] = set()
        expanded_h1: set[str] = set()  # "context_id|title|header1"
        expanded_h2: set[str] = set()  # "context_id|title|header1|header2"
        expanded_h3: set[str] = set()  # "context_id|title|header1|header2|header3"

        for chunk in raw_refs[: max(self._top_k, 1)]:
            if chunk.chunk_id in seen_chunk_ids:
                continue
            seen_chunk_ids.add(chunk.chunk_id)

            hierarchy = _detect_hierarchy(chunk.header1, chunk.header2, chunk.header3)

            # Skip if a parent hierarchy was already expanded
            if chunk.header1:
                h1_key = f"{chunk.source}|{chunk.title}|{chunk.header1}"
                if h1_key in expanded_h1 and hierarchy in ("header2", "header3"):
                    continue
            if chunk.header1 and chunk.header2:
                h2_key = f"{chunk.source}|{chunk.title}|{chunk.header1}|{chunk.header2}"
                if h2_key in expanded_h2 and hierarchy == "header3":
                    continue
            if chunk.header1 and chunk.header2 and chunk.header3:
                h3_key = f"{chunk.source}|{chunk.title}|{chunk.header1}|{chunk.header2}|{chunk.header3}"
                if h3_key in expanded_h3:
                    continue

            unique_refs.append(chunk)

            # Track this expansion for future iterations
            if hierarchy == "header1" and chunk.header1:
                expanded_h1.add(f"{chunk.source}|{chunk.title}|{chunk.header1}")
            elif hierarchy == "header2" and chunk.header1 and chunk.header2:
                expanded_h2.add(f"{chunk.source}|{chunk.title}|{chunk.header1}|{chunk.header2}")
            elif hierarchy == "header3" and chunk.header1 and chunk.header2 and chunk.header3:
                expanded_h3.add(f"{chunk.source}|{chunk.title}|{chunk.header1}|{chunk.header2}|{chunk.header3}")

        # Expand all chunks by header hierarchy concurrently
        tasks = [self._expand_by_hierarchy(raw) for raw in unique_refs]
        return await asyncio.gather(*tasks)

    async def _expand_by_hierarchy(self, chunk: KnowledgeChunk) -> KnowledgeChunk:
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
            top=50,
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

    async def close(self) -> None:
        await self._kb_client.close()
        await self._search_client.close()
        close_method = getattr(self._credential, "close", None)
        if callable(close_method):
            await close_method()


def _escape_odata(value: str) -> str:
    return value.replace("'", "''")


def _detect_hierarchy(header1: str, header2: str, header3: str) -> str:
    """Determine the hierarchy level of a chunk (mirrors Go DetectChunkHierarchy)."""
    if header3:
        return "header3"
    if header2 and header1:
        return "header2"
    if header1:
        return "header1"
    return "unknown"


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