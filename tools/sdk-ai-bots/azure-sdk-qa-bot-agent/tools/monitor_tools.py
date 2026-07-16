"""Azure Monitor / App Insights tools for the Azure SDK QA Bot Agent.

Provides ``fetch_chat_trace`` — a normalized, token-bounded view of the
chat agent's App Insights spans for a given ``trace_id``. Used by the
feedback agent to reconstruct what the chat agent did on a turn that
received negative feedback.
"""

from __future__ import annotations

import logging
from typing import Annotated, Any

from pydantic import BaseModel, Field

from config.app_config import get as cfg
from tools import tool
from utils.azure_monitor import query_spans_by_trace_id

logger = logging.getLogger(__name__)

# App Configuration key holding the ARM resource id of the chat agent's
# Application Insights resource (the traces we reconstruct feedback from).
_AGENT_APPINSIGHTS_RESOURCE_ID_KEY = "AGENT_APPLICATIONINSIGHTS_RESOURCE_ID"

# Caps to keep tool output token-bounded.
_MAX_TRACE_SPANS = 80
_MAX_TRACE_FIELD_CHARS = 400


def _get_agent_appinsights_resource_id() -> str:
    rid = (cfg(_AGENT_APPINSIGHTS_RESOURCE_ID_KEY, "") or "").strip()
    if not rid:
        raise RuntimeError(
            f"App Configuration key '{_AGENT_APPINSIGHTS_RESOURCE_ID_KEY}' is not set; "
            "cannot query App Insights for chat-agent traces."
        )
    return rid


def _truncate(s: str | None, limit: int) -> str | None:
    if s is None:
        return None
    if len(s) <= limit:
        return s
    return s[:limit] + " …[truncated]"


class TraceCall(BaseModel):
    timestamp: str
    table: str
    name: str
    type: str
    duration_ms: float | None = None
    success: bool | None = None
    result_code: str | None = None
    data: str | None = None
    attributes: dict[str, Any] = Field(default_factory=dict)


class ChatTraceView(BaseModel):
    trace_id: str
    found: bool
    span_count: int
    truncated: bool = False
    error: str | None = None
    calls: list[TraceCall] = Field(default_factory=list)


class MonitorTools:
    """App Insights trace tools surfaced to the hosted feedback agent."""

    @tool
    async def fetch_chat_trace(
        self,
        *,
        trace_id: Annotated[
            str,
            "The OTel trace id of the chat turn (visible in the Foundry "
            "Tracing tab). Used to look up matching App Insights spans "
            "emitted by the chat agent.",
        ],
    ) -> ChatTraceView:
        """Fetch normalized chat-agent spans for a given ``trace_id``.

        Returns an ordered list of tool calls / requests / log records.
        On App Insights ingestion lag or empty results, ``found=False`` and
        the agent should return a structured ``trace_unavailable`` outcome.
        """
        if not trace_id:
            return ChatTraceView(
                trace_id="",
                found=False,
                span_count=0,
                error="trace_id is empty",
            )
        try:
            resource_id = _get_agent_appinsights_resource_id()
            spans = await query_spans_by_trace_id(trace_id, resource_id=resource_id)
        except Exception as exc:  # defensive — query_spans already handles
            logger.exception("fetch_chat_trace failed for %s", trace_id)
            return ChatTraceView(
                trace_id=trace_id,
                found=False,
                span_count=0,
                error=f"query_failed: {exc}",
            )

        if not spans:
            return ChatTraceView(
                trace_id=trace_id,
                found=False,
                span_count=0,
                error="no_spans_found",
            )

        truncated = len(spans) > _MAX_TRACE_SPANS
        clipped = spans[:_MAX_TRACE_SPANS]
        calls = [
            TraceCall(
                timestamp=s.timestamp,
                table=s.table,
                name=s.name,
                type=s.type,
                duration_ms=s.duration_ms,
                success=s.success,
                result_code=s.result_code,
                data=_truncate(s.data, _MAX_TRACE_FIELD_CHARS),
                attributes=s.custom_dimensions,
            )
            for s in clipped
        ]
        return ChatTraceView(
            trace_id=trace_id,
            found=True,
            span_count=len(spans),
            truncated=truncated,
            calls=calls,
        )


__all__ = ["MonitorTools", "TraceCall", "ChatTraceView"]
