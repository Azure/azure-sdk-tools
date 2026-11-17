"""Channel handoff routing tool for the Azure SDK QA Bot Agent."""

from __future__ import annotations

from typing import Annotated

from pydantic import BaseModel

from tools import tool


class RouteToChannelResult(BaseModel):
    """Structured result indicating an unresolved question needs another channel's experts."""

    channel_name: str
    channel_id: str | None = None
    reason: str = ""


class RoutingTools:
    """Tools for explicit channel handoff when a question cannot be resolved here."""

    @tool
    async def route_to_channel(
        self,
        *,
        channel_name: Annotated[
            str,
            "Exact channel_name of the target channel from [routable_channels]. "
            "Do not pass the current channel.",
        ],
        reason: Annotated[
            str,
            "Brief reason why the question remains unresolved and needs that channel's experts.",
        ],
        channel_id: Annotated[
            str | None,
            "Optional channel_id of the target channel from [routable_channels].",
        ] = None,
    ) -> RouteToChannelResult:
        """Recommend re-posting in another Teams channel ONLY when the question remains unresolved.

        Call this ONLY when you cannot resolve the user's question and it requires
        human or domain experts in another channel (such as manual review,
        approval, escalation, or unavailable channel-specific expertise).
        NEVER call this when you have answered the question, even after calling
        `load_skill` for another domain.
        """
        normalized_name = channel_name.strip()
        if not normalized_name:
            raise ValueError("channel_name must not be empty.")
        normalized_id = (
            channel_id.strip() if channel_id and channel_id.strip() else None
        )
        return RouteToChannelResult(
            channel_name=normalized_name,
            channel_id=normalized_id,
            reason=reason.strip(),
        )
