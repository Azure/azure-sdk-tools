"""Graph-based retrieval tool (HTTP client → backend server).

Exposes a single tool,
:meth:`GraphKnowledgeTools.search_knowledge_graph`, that retrieves
graph-grounded source snippets for a natural-language query.
"""

from __future__ import annotations

import logging
from typing import Annotated, Any

import httpx

from config.app_config import get as cfg
from models.knowledge import GraphSearchResult
from tools import tool
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_DEFAULT_TIMEOUT_SECONDS = 60.0


class GraphKnowledgeTools:
    """Knowledge graph retrieval tools backed by the backend server."""

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
        tenant_id: Annotated[
            str,
            "Optional tenant identifier (e.g. 'typespec_channel_qa_bot', "
            "'python_channel_qa_bot'). Read it from the active skill's "
            "[skill_tenant_id] line. When set to a known tenant the "
            "backend restricts graph retrieval to entities sourced from "
            "that tenant's KnowledgeSource set — same scoping as "
            "search_knowledge_base. Pass an empty string for unscoped "
            "retrieval (cross-domain questions).",
        ] = "",
    ) -> GraphSearchResult:
        """Retrieve graph-grounded references for ``query`` via the backend.

        Returns ``references`` (source snippets) plus, in drift mode, an
        ``analysis`` field — a short grounded intermediate conclusion the
        backend synthesised by decomposing the query over the knowledge graph.
        Returns an empty ``references`` list when the query is blank,
        the backend endpoint is not configured, the HTTP call fails, or
        retrieval returned nothing. The chat agent is responsible for
        falling back to other tools in that case.
        """
        normalised_query = (query or "").strip()
        if not normalised_query:
            return GraphSearchResult(references=[], query="")

        endpoint = cfg("GRAPH_QUERY_URL", "").strip()
        audience = cfg("GRAPH_QUERY_AUDIENCE", "").strip()
        if not endpoint or not audience:
            logger.warning(
                "search_knowledge_graph: GRAPH_QUERY_URL / "
                "GRAPH_QUERY_AUDIENCE not configured — returning "
                "empty result"
            )
            return GraphSearchResult(references=[], query=normalised_query)

        try:
            timeout = float(cfg("GRAPH_QUERY_TIMEOUT_SECONDS", str(_DEFAULT_TIMEOUT_SECONDS)))
        except (TypeError, ValueError):
            timeout = _DEFAULT_TIMEOUT_SECONDS

        # Acquire an Entra ID access token for the backend's EasyAuth
        # audience using the chat-agent's Managed Identity. The
        # underlying credential caches the token until expiry so
        # subsequent calls within the lifetime of the sandbox don't
        # hit AAD again.
        scope = f"{audience}/.default"
        try:
            access_token = await get_credential().get_token(scope)
        except Exception:
            logger.exception(
                "search_knowledge_graph: failed to acquire AAD token for scope=%s",
                scope,
            )
            return GraphSearchResult(references=[], query=normalised_query)

        normalised_tenant = (tenant_id or "").strip()
        logger.info(
            "Posting graph query to %s (timeout=%.1fs, tenant_id=%r)",
            endpoint,
            timeout,
            normalised_tenant,
        )

        payload: dict[str, Any] = {"query": normalised_query}
        if normalised_tenant:
            payload["tenant_id"] = normalised_tenant

        try:
            async with httpx.AsyncClient(timeout=timeout) as client:
                response = await client.post(
                    endpoint,
                    headers={"Authorization": f"Bearer {access_token.token}"},
                    json=payload,
                )
                response.raise_for_status()
                payload = response.json()
        except httpx.HTTPError as exc:
            logger.warning(
                "search_knowledge_graph HTTP call failed for query=%r: %s",
                normalised_query,
                exc,
            )
            return GraphSearchResult(references=[], query=normalised_query)
        except Exception:
            logger.exception(
                "search_knowledge_graph: unexpected error for query=%r",
                normalised_query,
            )
            return GraphSearchResult(references=[], query=normalised_query)

        try:
            result = GraphSearchResult.model_validate(payload)
        except Exception:
            logger.exception(
                "search_knowledge_graph: backend payload failed validation: %r",
                payload,
            )
            return GraphSearchResult(references=[], query=normalised_query)

        # Ensure the echoed query reflects what we asked for, even if
        # the backend omitted it.
        if not result.query:
            result.query = normalised_query

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
