"""Data models for the feedback workflow."""

from __future__ import annotations

from datetime import datetime
from enum import Enum
from typing import Any

from pydantic import BaseModel, Field

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
    # NEW (required when reaction=bad for feedback-agent enqueue path).
    # The API is conversation-scoped; the server resolves the matching
    # bot response_id from the most recent assistant message.
    conversation_id: str | None = None
    conversation_type: ConversationType | None = None


class FeedbackResponse(BaseModel):
    """Result of processing a feedback request."""

    saved: bool = False
    issue_url: str | None = None


# ---------------------------------------------------------------------------
# Feedback agent job model (Cosmos `feedback-jobs` container)
# ---------------------------------------------------------------------------


class FeedbackJobTrigger(str, Enum):
    bad_reaction = "bad_reaction"
    expert_reply = "expert_reply"


class FeedbackJobStatus(str, Enum):
    queued = "queued"
    running = "running"
    done = "done"
    skipped = "skipped"


class FeedbackJob(BaseModel):
    """Durable feedback-analysis job persisted in Cosmos `feedback-jobs`.

    Partition key is ``/tenant_id``. The ``id`` follows the
    ``{conversation_id}:{trigger}:{timestamp}`` convention from §6.3
    of the feedback-agent design doc.

    The job row is a **lifecycle marker only** — the hosted agent owns
    the entire analysis (classification, fix summary, filing the GitHub
    issue, ...) via its own tools. The service does not parse the agent
    reply; the raw text is logged for ops triage.
    """

    id: str
    tenant_id: str
    trigger: FeedbackJobTrigger
    response_id: str
    conversation_id: str
    conversation_type: ConversationType

    comment: str | None = None
    reasons: list[str] = Field(default_factory=list)

    status: FeedbackJobStatus = FeedbackJobStatus.queued
    created_at: datetime
    updated_at: datetime

    # Free-form error context (e.g. agent_invocation_failed, timeout)
    # for ops triage. ``None`` when the agent ran to completion.
    error: str | None = None

    def to_cosmos(self) -> dict[str, Any]:
        """Serialize for Cosmos `upsert_item` (enum values as strings)."""
        return self.model_dump(mode="json")

    @classmethod
    def from_cosmos(cls, doc: dict[str, Any]) -> "FeedbackJob":
        # Strip Cosmos system fields before validation.
        cleaned = {k: v for k, v in doc.items() if not k.startswith("_")}
        return cls.model_validate(cleaned)


# ---------------------------------------------------------------------------
# Hosted feedback-agent I/O contract
# ---------------------------------------------------------------------------


class UserFeedbackInput(BaseModel):
    """Explicit user feedback payload (only present for `bad_reaction`)."""

    comment: str | None = None
    reasons: list[str] = Field(default_factory=list)


class FeedbackAgentInput(BaseModel):
    """Structured input sent to the hosted feedback agent.

    Serialized as JSON in a single `user` message — the agent's
    instruction.md spec calls out this exact schema.
    """

    trigger: FeedbackJobTrigger
    tenant_id: str
    conversation_id: str
    conversation_type: ConversationType
    response_id: str
    user_feedback: UserFeedbackInput | None = None

    def to_json(self) -> str:
        return self.model_dump_json(exclude_none=False)


class FoundryAgentReference(BaseModel):
    """`agent_reference` extra-body block for the Responses API."""

    name: str
    version: str
    type: str = "agent_reference"

    def to_extra_body(self) -> dict[str, Any]:
        return self.model_dump()
