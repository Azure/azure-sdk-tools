"""Graph-based knowledge retrieval tool (Microsoft GraphRAG / DRIFT search).

Exposes a single tool, :meth:`GraphKnowledgeTools.search_knowledge_graph`,
that runs GraphRAG's DRIFT search against the knowledge graph built by the
``azure-sdk-qa-bot-knowledge-graph-sync`` project. The vector similarity
component of the search hits the configured Azure AI Search indexes; the
graph structure (entities, relationships, community hierarchy, source text)
is loaded from the parquet artefacts produced by the sync pipeline.

Returned references point to the **original source documents** cited by
the DRIFT search (resolved via ``documents.parquet`` and ``text_units``),
so the LLM can surface real document titles / paths in its answer rather
than a synthetic "graph insight" stub.
"""

from __future__ import annotations

import asyncio
import logging
from typing import Annotated

from models.knowledge import Reference, SearchKnowledgeBaseResult
from tools import tool
from utils.knowledge_graph import (
    GraphSourceRef,
    get_knowledge_graph_service,
)

logger = logging.getLogger(__name__)

# Truncate each chunk excerpt to keep prompt context bounded.
_MAX_CONTENT_CHARS_PER_RESULT = 3000

# DRIFT search is expensive — cap the number of parallel queries per call.
_MAX_QUERIES = 2


class GraphKnowledgeTools:
    """Knowledge graph retrieval tools backed by GraphRAG DRIFT search."""

    @tool
    async def search_knowledge_graph(
        self,
        *,
        queries: Annotated[
            list[str],
            "One or two natural-language questions to ask the knowledge graph. "
            "GraphRAG DRIFT search reasons over communities of related entities "
            "and traverses their relationships, so phrase the query as a "
            "QUESTION or a topic — not a keyword list. "
            "Use this tool when the user's question requires connecting "
            "concepts across multiple documents or summarising a topic area "
            "(e.g., 'How does the TypeSpec ARM template relate to operationId "
            "naming?' or 'Explain the relationship between LRO, polling, and "
            "x-ms-long-running-operation'). "
            "Each query is expensive (multiple LLM calls); prefer one focused "
            "question, two only if they cover genuinely different facets.",
        ],
        tenant_id: Annotated[
            str,
            "The active tenant ID for the current conversation. Currently "
            "informational only — the underlying knowledge graph is global, "
            "but the field is kept for parity with search_knowledge_base and "
            "future per-tenant graphs.",
        ],
    ) -> SearchKnowledgeBaseResult:
        """Search the GraphRAG knowledge graph and return the source
        documents it cited.

        For each query, runs Microsoft GraphRAG's DRIFT (Dynamic Reasoning
        and Inference with Flexible Traversal) search, then extracts the
        text-unit citations from the resulting context payload and resolves
        them back to their original source-document files via
        ``documents.parquet``.

        Each resulting :class:`Reference` represents one cited source
        document — title and link reflect the original file path, and
        ``content`` is a representative chunk excerpt from that document.
        """
        service = get_knowledge_graph_service()

        capped = [q for q in queries[:_MAX_QUERIES] if q and q.strip()]
        if not capped:
            return SearchKnowledgeBaseResult(results=[])

        logger.info(
            "Running GraphRAG DRIFT search for tenant=%s, queries=%s",
            tenant_id,
            capped,
        )

        tasks = [service.drift_search(q) for q in capped]
        outcomes = await asyncio.gather(*tasks, return_exceptions=True)

        # Dedup across queries by document title so the LLM doesn't see the
        # same document twice if both queries cited it.
        merged: dict[str, Reference] = {}
        for query, outcome in zip(capped, outcomes):
            if isinstance(outcome, BaseException):
                logger.warning(
                    "GraphRAG drift_search failed for query=%r: %s",
                    query,
                    outcome,
                )
                continue
            if outcome is None:
                continue
            answer, sources = outcome
            if answer:
                logger.info(
                    "GraphRAG synthesised answer (query=%r) length=%d",
                    query,
                    len(answer),
                )
            for src in sources:
                ref = _graph_source_to_reference(src)
                # Keep the first occurrence (highest-ranked per query order).
                merged.setdefault(ref.title or ref.link, ref)

        refs = list(merged.values())

        logger.info(
            "=========Final Graph Search Result========= total=%d", len(refs)
        )
        for i, ref in enumerate(refs):
            logger.info(
                "Graph Result [%d] source=%s, title=%s, link=%s, content_len=%d",
                i + 1,
                ref.source,
                ref.title,
                ref.link,
                len(ref.content or ""),
            )
        logger.info("===================================== total=%d results", len(refs))

        return SearchKnowledgeBaseResult(results=refs)


def _graph_source_to_reference(src: GraphSourceRef) -> Reference:
    """Convert a :class:`GraphSourceRef` to the bot's :class:`Reference`."""
    return Reference(
        title=src.title,
        source=src.source,
        link=src.link,
        content=_truncate_content(src.content),
        score=0.0,
    )


def _truncate_content(content: str | None) -> str:
    """Truncate content to bound the prompt context size."""
    if not content:
        return ""
    if len(content) <= _MAX_CONTENT_CHARS_PER_RESULT:
        return content
    return content[:_MAX_CONTENT_CHARS_PER_RESULT] + "\n... [truncated]"
