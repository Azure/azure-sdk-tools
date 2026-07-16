"""Conversation retrieval tools for the Azure SDK QA Bot Agent.

Provides ``fetch_conversation`` — an ordered, token-bounded view of a
customer conversation, used by the feedback agent to reconstruct the
dialogue surrounding a turn that received negative feedback (including
any expert correction that followed).
"""

from __future__ import annotations

import logging
from typing import Annotated

from pydantic import BaseModel, Field

from models.conversation import ConversationType
from services.conversation_service import ConversationService
from tools import tool

logger = logging.getLogger(__name__)

# Caps to keep tool output token-bounded.
_MAX_MESSAGES = 100
_MAX_MESSAGE_CHARS = 1500


def _truncate(s: str | None, limit: int) -> str | None:
    if s is None:
        return None
    if len(s) <= limit:
        return s
    return s[:limit] + " …[truncated]"


def _resolve_conversation_type(value: str) -> ConversationType:
    try:
        return ConversationType(value)
    except ValueError as exc:
        valid = ", ".join(t.value for t in ConversationType)
        raise ValueError(
            f"Unknown conversation_type '{value}'. Valid values: {valid}"
        ) from exc


class FeedbackMessage(BaseModel):
    id: str
    role: str
    sender_name: str
    content: str
    created_at: str
    message_link: str | None = None


class ConversationView(BaseModel):
    conversation_id: str
    conversation_type: str
    found: bool
    message_count: int
    truncated: bool = False
    conversation_link: str | None = None
    messages: list[FeedbackMessage] = Field(default_factory=list)


class TraceConversationRef(BaseModel):
    trace_id: str
    found: bool
    conversation_id: str | None = None
    conversation_type: str | None = None


class ConversationTools:
    """Conversation history tools surfaced to the hosted feedback agent."""

    def __init__(
        self,
        *,
        conversation_service: ConversationService | None = None,
    ) -> None:
        self._conversations = conversation_service or ConversationService()

    @tool
    async def fetch_conversation(
        self,
        *,
        conversation_id: Annotated[str, "Customer conversation id."],
        conversation_type: Annotated[
            str,
            "Customer conversation type (e.g. 'teams_channel').",
        ],
    ) -> ConversationView:
        """Return all messages in a conversation, ordered by created_at."""
        try:
            ctype = _resolve_conversation_type(conversation_type)
        except ValueError:
            return ConversationView(
                conversation_id=conversation_id,
                conversation_type=conversation_type,
                found=False,
                message_count=0,
                messages=[],
            )

        items = await self._conversations.get_messages_by_conversation_id(
            conversation_id=conversation_id,
            conversation_type=ctype,
        )

        truncated = len(items) > _MAX_MESSAGES
        clipped = items[:_MAX_MESSAGES]
        messages = [
            FeedbackMessage(
                id=m.id,
                role=m.sender_role.value,
                sender_name=m.sender_name,
                content=_truncate(m.content, _MAX_MESSAGE_CHARS) or "",
                created_at=m.created_at.isoformat() if m.created_at else "",
                message_link=(
                    m.extra_info.message_link if m.extra_info else None
                ),
            )
            for m in clipped
        ]
        conversation_link = next(
            (msg.message_link for msg in messages if msg.message_link),
            None,
        )
        return ConversationView(
            conversation_id=conversation_id,
            conversation_type=conversation_type,
            found=bool(items),
            message_count=len(items),
            truncated=truncated,
            conversation_link=conversation_link,
            messages=messages,
        )

    @tool
    async def resolve_conversation_by_trace_id(
        self,
        *,
        trace_id: Annotated[
            str,
            "OTel trace id of the turn that received feedback (from Foundry Tracing).",
        ],
    ) -> TraceConversationRef:
        """Resolve conversation_id/type from a trace id.

        Use this when the user only supplies a trace id: it returns the
        conversation coordinates needed to call ``fetch_conversation``.
        """
        message = await self._conversations.get_message_by_trace_id(trace_id)
        if message is None:
            return TraceConversationRef(trace_id=trace_id, found=False)

        return TraceConversationRef(
            trace_id=trace_id,
            found=True,
            conversation_id=message.conversation_id,
            conversation_type=(
                message.conversation_type.value
                if message.conversation_type
                else None
            ),
        )


__all__ = ["ConversationTools", "FeedbackMessage", "ConversationView"]
