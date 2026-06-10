"""Graph-based answering tool (Microsoft GraphRAG / Local Search).

Exposes a single tool, :meth:`GraphKnowledgeTools.ask_knowledge_graph`,
that asks the knowledge graph a natural-language question and returns
a *synthesized expert answer* together with the source documents the
answer is grounded in.

Conceptual contract — this is an **answering tool**, not a search tool:

* Output type ``GraphAnswerResult { answer, citations[] }`` carries the
  graph-aware synthesis. ``answer`` is the expert opinion,
  ``citations`` list the source documents that grounded it.
* The chat agent treats the answer as a narrative spine and may pair
  it with verbatim chunks from ``search_knowledge_base`` (the vector
  KB tool) or other tools (web search, GitHub, pipeline analysis,
  ...). The ``instruction.md`` "Answer synthesis" section documents
  the merge rules.

Why Local Search (not DRIFT / global)
-------------------------------------
Local Search is GraphRAG's single-LLM, query-focused mode: entity
description vector match → 1-hop relationship expansion → community
summaries for matched entities → entity → text-unit back-references.
On the bot's snapshot it returns in ~20s (vs ~73s for DRIFT, ~110s for
global). The chat agent's own LLM does the cross-tool reasoning, so
Local Search supplies the right level of graph-aware grounding without
inflating per-turn latency.

POC scope — tenant filtering (``scope`` / ``service_type`` / source
folder allow-lists) has been removed; the shared graph is served to
every caller. Per-tenant masking will be layered back on top once the
blob-direct end-to-end pipeline is validated.
"""

from __future__ import annotations

import logging
from typing import Annotated

from models.knowledge import GraphAnswerResult, GraphCitation
from tools import tool
from utils.knowledge_graph import (
    GraphSourceRef,
    get_knowledge_graph_service,
)

logger = logging.getLogger(__name__)

# Truncate each citation snippet to keep prompt context bounded.
_MAX_SNIPPET_CHARS = 1200


class GraphKnowledgeTools:
    """Knowledge graph answering tools backed by GraphRAG Local Search."""

    @tool
    async def ask_knowledge_graph(
        self,
        *,
        query: Annotated[
            str,
            "A single natural-language QUESTION to ASK the knowledge graph. "
            "This is an answering tool: GraphRAG runs Local Search to "
            "match entities related to the query, traverse their 1-hop "
            "relationships, and synthesise an expert answer grounded in "
            "the community summaries and source text units the entities "
            "belong to. Phrase the input as a QUESTION or a topic (not a "
            "keyword list). Use this tool when the user's question "
            "benefits from entity-centric or 'how does X relate to Y' "
            "style reasoning across documents. One LLM call per query "
            "(~20s); send one focused question per turn.",
        ],
    ) -> GraphAnswerResult:
        """Ask the knowledge graph and return a synthesised expert answer.

        Runs Microsoft GraphRAG's Local Search for the supplied
        ``query``. The resulting ``answer`` is a graph-aware synthesis
        (one LLM call over entity-, relationship-, community- and
        text-unit context); ``citations`` lists the source documents
        that grounded it.

        Returns an empty result (empty ``answer``, no citations) when
        the query is blank, the graph service is disabled, or the
        search failed. The chat agent is responsible for falling back
        to other tools in that case.
        """
        normalised_query = (query or "").strip()
        if not normalised_query:
            return GraphAnswerResult(answer="", citations=[], query="")

        service = get_knowledge_graph_service()

        logger.info("Running GraphRAG Local Search for query=%r", normalised_query)

        try:
            outcome = await service.local_search(normalised_query)
        except Exception as exc:
            logger.warning(
                "GraphRAG local_search failed for query=%r: %s",
                normalised_query,
                exc,
            )
            return GraphAnswerResult(answer="", citations=[], query=normalised_query)

        if outcome is None:
            return GraphAnswerResult(answer="", citations=[], query=normalised_query)

        answer_text, sources = outcome
        answer = answer_text.strip() if answer_text else ""
        if answer:
            logger.info(
                "GraphRAG synthesised answer (query=%r) length=%d",
                normalised_query,
                len(answer),
            )

        merged_citations: dict[str, GraphCitation] = {}
        for src in sources:
            citation = _graph_source_to_citation(src)
            # Dedupe by (title|link); keep first occurrence (highest-ranked).
            merged_citations.setdefault(citation.title or citation.link, citation)

        result = GraphAnswerResult(
            answer=answer,
            citations=list(merged_citations.values()),
            query=normalised_query,
        )

        logger.info(
            "=========GraphRAG Result========= answer_len=%d, citations=%d",
            len(result.answer),
            len(result.citations),
        )
        for i, citation in enumerate(result.citations):
            logger.info(
                "Graph Citation [%d] title=%s, link=%s",
                i + 1,
                citation.title,
                citation.link,
            )
        logger.info("===================================== query=%r", normalised_query)

        return result


def _graph_source_to_citation(src: GraphSourceRef) -> GraphCitation:
    """Convert a :class:`GraphSourceRef` to the tool's :class:`GraphCitation`."""
    return GraphCitation(
        title=src.title,
        link=src.link,
        snippet=_truncate_snippet(src.content),
    )


def _truncate_snippet(content: str | None) -> str:
    """Truncate snippet to bound the prompt context size."""
    if not content:
        return ""
    if len(content) <= _MAX_SNIPPET_CHARS:
        return content
    return content[:_MAX_SNIPPET_CHARS] + "\n... [truncated]"
