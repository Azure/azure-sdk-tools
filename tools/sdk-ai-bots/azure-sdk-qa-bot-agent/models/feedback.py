"""Data models for the feedback workflow."""

from __future__ import annotations

from enum import Enum
from typing import Any

from pydantic import BaseModel

from models.conversation import ConversationType


class Reaction(str, Enum):
    """User feedback reaction types."""

    good = "good"
    bad = "bad"
    unknown = "unknown"


class FeedbackRequest(BaseModel):
    """Incoming feedback payload from the Teams App."""

    channel_id: str | None = None
    tenant_id: str = "unknown"
    reaction: Reaction = Reaction.unknown
    comment: str | None = None
    reasons: list[str] = []
    link: str | None = None
    user_name: str | None = None
    # NEW (required when reaction=bad for the Self-Evolving Knowledge Agent
    # enqueue path).
    # The API is conversation-scoped; the server resolves the matching
    # bot response_id from the most recent assistant message.
    conversation_id: str | None = None
    conversation_type: ConversationType | None = None


class FeedbackResponse(BaseModel):
    """Result of processing a feedback request."""

    saved: bool = False
    issue_url: str | None = None


# ---------------------------------------------------------------------------
# Hosted Self-Evolving Knowledge Agent I/O contract
# ---------------------------------------------------------------------------


class SelfEvolvingKnowledgeAgentInput(BaseModel):
    """Structured input sent to the hosted Self-Evolving Knowledge Agent.

    Serialized as JSON in a single `user` message — the agent's
    instruction.md spec calls out this exact schema. Feedback is scoped to a
    whole **conversation (QA thread)**, not a single bot reply, so the payload
    carries only the thread coordinates. The agent reconstructs the transcript
    with `fetch_conversation` and derives each bot turn's `trace_id` from it.
    """

    tenant_id: str
    conversation_id: str
    conversation_type: ConversationType

    def to_json(self) -> str:
        return self.model_dump_json(exclude_none=False)


class FoundryAgentReference(BaseModel):
    """`agent_reference` extra-body block for the Responses API."""

    name: str
    version: str
    type: str = "agent_reference"

    def to_extra_body(self) -> dict[str, Any]:
        return self.model_dump()
