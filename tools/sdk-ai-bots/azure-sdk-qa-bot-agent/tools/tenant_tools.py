"""Tenant routing tool for the Azure SDK QA Bot Agent.

Provides a tool the hosted chat-agent can invoke to determine the best
tenant for the current conversation.  The tool:
  1. Reads the tenant routing prompt template.
  2. Sends the original tenant_id + conversation summary to an LLM.
  3. Parses the LLM JSON response to get the recommended tenant_id.
  4. Loads the recommended tenant's QA guideline.
  5. Returns the routed tenant_id, guideline, and knowledge sources so the
     agent can adjust its behaviour for the rest of the conversation.
"""

from __future__ import annotations

import json
import logging
from typing import Annotated

from config.app_config import get as cfg
from config.tenant_config import (
    TenantID,
    get_all_tenant_ids,
    get_tenant_config,
    get_tenant_sources_display,
    load_tenant_qa_guideline,
)
from models.chat import RouteTenantResult
from tools import tool
from utils.llm import LLMError, PromptTemplate, execute_prompt

logger = logging.getLogger(__name__)

_ROUTING_TEMPLATE = PromptTemplate(
    prompt_file="tenant_routing.md",
    schema_file="tenant_routing_result_schema.json",
)

class TenantTools:
    """Tools for routing conversations to the most appropriate tenant."""

    @tool
    async def route_tenant(
        self,
        *,
        original_tenant_id: Annotated[
            str,
            "The tenant ID that the user originally connected from "
            "(provided in the [tenant_context] system message at the start of the conversation).",
        ],
        conversation_summary: Annotated[
            str,
            "A brief summary of the conversation so far, including the user's "
            "core question or topic. Used to determine which specialised tenant "
            "should handle the conversation.",
        ],
    ) -> RouteTenantResult:
        """Route the conversation to the best-matching tenant.

        Analyses the user's question domain and the original tenant to decide
        whether to stay with the current tenant or switch to a more specialised
        one.  Returns a JSON object with:
          - route_tenant: the recommended tenant ID
          - tenant_guideline: the QA guideline for the routed tenant
          - knowledge_sources: list of {name, description} for the tenant's
            knowledge sources — use these to decide which sources to pass to
            ``search_knowledge_base``
          - routed: whether the tenant was changed from the original

        The agent should use the returned ``tenant_guideline`` as additional
        context for answering the user's question, and pick relevant sources
        from ``knowledge_sources`` when calling the search tool.
        """
        # ------------------------------------------------------------------
        # 1. Validate original tenant
        # ------------------------------------------------------------------
        valid_ids = get_all_tenant_ids()
        if original_tenant_id not in valid_ids:
            logger.warning(
                "Unknown original_tenant_id '%s', falling back to general_qa_bot",
                original_tenant_id,
            )
            original_tenant_id = TenantID.GENERAL_QA_BOT.value

        # ------------------------------------------------------------------
        # 2. Check if routing is enabled for this tenant
        # ------------------------------------------------------------------
        config = get_tenant_config(original_tenant_id)
        if config is None or not config.enable_routing:
            # Routing disabled — stay with the original tenant
            guideline = load_tenant_qa_guideline(original_tenant_id)
            sources = get_tenant_sources_display(original_tenant_id)
            return RouteTenantResult(
                route_tenant=original_tenant_id,
                tenant_guideline=guideline,
                knowledge_sources=sources,
                routed=False,
            )

        # ------------------------------------------------------------------
        # 3. Call LLM for tenant routing
        # ------------------------------------------------------------------
        recommended = await self._llm_route(
            original_tenant_id, conversation_summary
        )

        guideline = load_tenant_qa_guideline(recommended)
        sources = get_tenant_sources_display(recommended)

        return RouteTenantResult(
            route_tenant=recommended,
            tenant_guideline=guideline,
            knowledge_sources=sources,
            routed=recommended != original_tenant_id,
        )

    # ------------------------------------------------------------------
    # Internal helpers
    # ------------------------------------------------------------------

    @staticmethod
    async def _llm_route(original_tenant_id: str, summary: str) -> str:
        """Call the LLM to determine the best tenant for the conversation.

        Uses the shared :func:`execute_prompt` utility with the routing
        prompt template and JSON schema.  Falls back to the original tenant
        if the LLM call fails or returns an invalid tenant.
        """
        try:
            result = await execute_prompt(
                _ROUTING_TEMPLATE,
                variables={"original_tenant": original_tenant_id},
                user_message=summary,
                model=cfg("AOAI_CHAT_REASONING_MODEL", "gpt-5.1"),
                reasoning_effort=cfg(
                    "AOAI_CHAT_REASONING_MODEL_REASONING_EFFORT", "high"
                ),
            )
        except (FileNotFoundError, LLMError) as exc:
            logger.error("LLM tenant routing failed: %s", exc)
            return original_tenant_id

        routed_id = result.get("route_tenant", "")
        logger.info(
            "LLM tenant routing recommendation: %s → %s",
            original_tenant_id,
            routed_id,
        )

        # Validate the routed tenant exists
        if not routed_id or routed_id == original_tenant_id:
            return original_tenant_id

        if get_tenant_config(routed_id) is None:
            logger.warning(
                "LLM recommended unknown tenant '%s', staying with original",
                routed_id,
            )
            return original_tenant_id

        return routed_id
