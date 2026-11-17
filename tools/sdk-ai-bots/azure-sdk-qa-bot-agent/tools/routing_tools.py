"""Channel handoff routing tool for the Azure SDK QA Bot Agent."""

from __future__ import annotations

from typing import Annotated

from pydantic import BaseModel

from config.tenant_config import TenantID, get_tenant_config
from skills.tenant_skills import get_skill_to_tenant_map
from tools import tool


class RouteToChannelResult(BaseModel):
    """Structured result indicating an unresolved question needs another channel's experts."""

    tenant_id: TenantID
    reason: str = ""


def _resolve_target_tenant(raw_tenant: str) -> TenantID:
    normalized = raw_tenant.strip()
    try:
        tenant = TenantID(normalized)
    except ValueError:
        skill_tenant = get_skill_to_tenant_map().get(normalized)
        if skill_tenant is None:
            raise ValueError(f"Unknown target tenant_id: {raw_tenant!r}")
        tenant = skill_tenant

    if tenant == TenantID.AZURE_TYPESPEC_AUTHORING or get_tenant_config(tenant) is None:
        raise ValueError(f"Target tenant_id {tenant.value!r} is not a routable channel.")
    return tenant


class RoutingTools:
    """Tools for explicit channel handoff when a question cannot be resolved here."""

    @tool
    async def route_to_channel(
        self,
        *,
        tenant_id: Annotated[
            str,
            "Target channel tenant_id for follow-up: "
            "'typespec_channel_qa_bot', 'api_spec_review_bot', "
            "'api_stewardship_board_qa_bot', 'azure_sdk_onboarding', "
            "'azsdk_tools_agent_qa_bot', 'azure_mcp_server', "
            "'python_channel_qa_bot', 'dotnet_channel_qa_bot', "
            "'golang_channel_qa_bot', 'java_channel_qa_bot', or "
            "'javascript_channel_qa_bot'. Do not pass the current channel's own tenant.",
        ],
        reason: Annotated[
            str,
            "Brief reason why the question remains unresolved and needs that channel's experts.",
        ],
    ) -> RouteToChannelResult:
        """Recommend re-posting in another Teams channel ONLY when the question remains unresolved.

        Call this ONLY when you cannot resolve the user's question and it requires
        human or domain experts in another channel (such as manual review,
        approval, escalation, or unavailable channel-specific expertise).
        NEVER call this when you have answered the question, even after calling
        `load_skill` for another domain.
        """
        target = _resolve_target_tenant(tenant_id)
        return RouteToChannelResult(tenant_id=target, reason=reason.strip())
