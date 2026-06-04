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


class FeedbackClassification(str, Enum):
    """Root-cause label produced by the feedback agent."""

    missing_content = "missing_content"
    outdated_content = "outdated_content"
    retrieval_mismatch = "retrieval_mismatch"
    reasoning_gap = "reasoning_gap"
    out_of_scope = "out_of_scope"


class FeedbackJob(BaseModel):
    """Durable feedback-analysis job persisted in Cosmos `feedback-jobs`.

    Partition key is ``/tenant_id``. The ``id`` follows the
    ``{conversation_id}:{trigger}:{timestamp}`` convention from §6.3
    of the feedback-agent design doc.
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

    # Populated by the agent on completion.
    classification: FeedbackClassification | None = None
    user_intent_summary: str | None = None
    suggested_fix_summary: str | None = None
    corrected_answer: str | None = None
    issue_url: str | None = None

    # Free-form error context (e.g. trace_unavailable) for ops triage.
    error: str | None = None

    def to_cosmos(self) -> dict[str, Any]:
        """Serialize for Cosmos `upsert_item` (enum values as strings)."""
        return self.model_dump(mode="json")

    @classmethod
    def from_cosmos(cls, doc: dict[str, Any]) -> "FeedbackJob":
        # Strip Cosmos system fields before validation.
        cleaned = {k: v for k, v in doc.items() if not k.startswith("_")}
        return cls.model_validate(cleaned)
