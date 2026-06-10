"""Graph-based retrieval tool (Microsoft GraphRAG / Local Search context).

Exposes a single tool,
:meth:`GraphKnowledgeTools.search_knowledge_graph`, that retrieves
graph-grounded source snippets for a natural-language query. Mirrors
``search_knowledge_base`` (the Azure AI Search vector retriever) in
output shape so the chat agent can consume both the same way.

Conceptual contract — this is a **retrieval tool**, not an answering
tool:

* Output type ``GraphSearchResult { references[] }`` carries verbatim
  source snippets, deduplicated by document. No synthesised narrative,
  no LLM rewrite — the chat agent's own LLM does final synthesis over
  the union of all retrieved references.
* Recall path differs from ``search_knowledge_base``: graph retrieval
  matches the query against entity descriptions, expands one hop
  through relationships, pulls in community-membership context, and
  resolves back to the text units those entities appear in. KB
  retrieval matches the query embedding directly against text-chunk
  embeddings. The two candidate sets are complementary — graph wins
  on entity-centric / "how does X relate to Y" questions; KB wins on
  exact-phrase / verbatim-fact questions.

No completion LLM call per query — the bot just runs the GraphRAG
context builder (one embedding + one ANN + DataFrame joins), so this
tool's per-call latency is comparable to the KB tool's.
"""

from __future__ import annotations

import logging
from typing import Annotated

from models.knowledge import GraphReference, GraphSearchResult
from tools import tool
from utils.knowledge_graph import (
    GraphSourceRef,
    get_knowledge_graph_service,
)

logger = logging.getLogger(__name__)

# Truncate each reference snippet to keep prompt context bounded.
_MAX_SNIPPET_CHARS = 1200


class GraphKnowledgeTools:
    """Knowledge graph retrieval tools backed by GraphRAG Local Search context."""

    @tool
    async def search_knowledge_graph(
        self,
        *,
        query: Annotated[
            str,
            "A single natural-language QUERY describing what the user "
            "wants grounded. Returns a list of source-document "
            "references (title, link, snippet) retrieved via graph "
            "traversal: entity-description ANN match, 1-hop "
            "relationship expansion, and community-membership "
            "context. Output shape mirrors search_knowledge_base. "
            "Phrase the input as a question or topic (not a keyword "
            "list). Use in parallel with search_knowledge_base; the "
            "two recall paths are complementary.",
        ],
    ) -> GraphSearchResult:
        """Retrieve graph-grounded references for ``query``.

        Returns an empty ``references`` list when the query is blank,
        the graph service is disabled, or retrieval failed. The chat
        agent is responsible for falling back to other tools in that
        case.
        """
        normalised_query = (query or "").strip()
        if not normalised_query:
            return GraphSearchResult(references=[], query="")

        service = get_knowledge_graph_service()

        logger.info(
            "Running GraphRAG context retrieval for query=%r", normalised_query
        )

        try:
            sources = await service.search_graph(normalised_query)
        except Exception as exc:
            logger.warning(
                "GraphRAG search_graph failed for query=%r: %s",
                normalised_query,
                exc,
            )
            return GraphSearchResult(references=[], query=normalised_query)

        if sources is None:
            return GraphSearchResult(references=[], query=normalised_query)

        merged_refs: dict[str, GraphReference] = {}
        for src in sources:
            ref = _graph_source_to_reference(src)
            # Dedupe by (title|link); keep first occurrence (highest-ranked).
            merged_refs.setdefault(ref.title or ref.link, ref)

        result = GraphSearchResult(
            references=list(merged_refs.values()),
            query=normalised_query,
        )

        logger.info(
            "=========GraphRAG Result========= references=%d",
            len(result.references),
        )
        for i, ref in enumerate(result.references):
            logger.info(
                "Graph Reference [%d] title=%s, link=%s",
                i + 1,
                ref.title,
                ref.link,
            )
        logger.info("===================================== query=%r", normalised_query)

        return result


def _graph_source_to_reference(src: GraphSourceRef) -> GraphReference:
    """Convert a :class:`GraphSourceRef` to the tool's :class:`GraphReference`."""
    return GraphReference(
        title=src.title,
        link=src.link,
        snippet=_truncate_snippet(src.content),
        source=src.source or "graphrag",
    )


def _truncate_snippet(content: str | None) -> str:
    """Truncate snippet to bound the prompt context size."""
    if not content:
        return ""
    if len(content) <= _MAX_SNIPPET_CHARS:
        return content
    return content[:_MAX_SNIPPET_CHARS] + "\n... [truncated]"
