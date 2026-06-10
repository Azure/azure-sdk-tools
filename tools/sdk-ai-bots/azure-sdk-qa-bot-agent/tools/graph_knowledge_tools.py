"""Graph-based answering tool (Microsoft GraphRAG / DRIFT search).

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
    """Knowledge graph answering tools backed by GraphRAG DRIFT search."""

    @tool
    async def ask_knowledge_graph(
        self,
        *,
        query: Annotated[
            str,
            "A single natural-language QUESTION to ASK the knowledge graph. "
            "This is an answering tool: GraphRAG runs DRIFT search to "
            "reason over communities of related entities and traverse "
            "their relationships, then synthesises an expert answer. "
            "Phrase the input as a QUESTION or a topic (not a keyword "
            "list). Use this tool when the user's question benefits from "
            "cross-document reasoning, summarisation, or 'explain how X "
            "relates to Y' style answers. The call is expensive (multiple "
            "LLM calls); send one focused question per turn.",
        ],
    ) -> GraphAnswerResult:
        """Ask the knowledge graph and return a synthesised expert answer.

        Runs Microsoft GraphRAG's DRIFT (Dynamic Reasoning and Inference
        with Flexible Traversal) search for the supplied ``query``. The
        resulting ``answer`` is a graph-aware synthesis; ``citations``
        lists the source documents that grounded it.

        Returns an empty result (empty ``answer``, no citations) when
        the query is blank, the graph service is disabled, or the
        search failed. The chat agent is responsible for falling back
        to other tools in that case.
        """
        normalised_query = (query or "").strip()
        if not normalised_query:
            return GraphAnswerResult(answer="", citations=[], query="")

        service = get_knowledge_graph_service()

        logger.info("Running GraphRAG DRIFT search for query=%r", normalised_query)

        try:
            outcome = await service.drift_search(normalised_query)
        except Exception as exc:
            logger.warning(
                "GraphRAG drift_search failed for query=%r: %s",
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
