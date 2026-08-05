"""QA record model for the Cosmos ``qa-records`` container.

A ``QARecord`` is the durable status row for **one conversation thread**
(aggregated from the per-message rows in ``conversation-messages`` by
``conversation_id``). Each record carries **two status layers**:

* **Layer 1 — QA lifecycle** (:class:`QAStatus`): ``ongoing`` while the
  thread is still open, ``finished`` once it concluded with a correct bot
  answer, or ``failed`` once it concluded with an incorrect/unknown bot
  answer (i.e. a case worth a feedback analysis).
* **Layer 2 — Feedback lifecycle** (:class:`FeedbackStatus`, embedded in
  :class:`FeedbackState`): only populated once the QA reaches ``failed``.
  ``created`` when the chatbot evolution agent session is requested, ``running``
  once the hosted agent accepted and is processing, ``done`` when it
  finished, ``failed`` when it errored/timed out.

Partition key is ``/tenant_id`` (matches the episode and conversation
conventions). The ``id`` is the thread key
``{conversation_type}:{conversation_id}`` so the daily aggregation job is
naturally idempotent.
"""

from __future__ import annotations

from datetime import datetime
from enum import Enum
from typing import Any

from pydantic import BaseModel

from models.conversation import BotAnswerVerdict, ConversationType


class QAStatus(str, Enum):
    """Layer-1 lifecycle state of a QA thread."""

    #: The thread is still open — either unanswered follow-ups remain or no
    #: human has confirmed/corrected the bot yet.
    ongoing = "ongoing"
    #: The thread concluded and the bot answered correctly (archived).
    finished = "finished"
    #: The thread concluded but the bot answer was wrong or unconfirmed;
    #: a feedback analysis is warranted.
    failed = "failed"


class FeedbackStatus(str, Enum):
    """Layer-2 lifecycle state of the feedback-agent analysis."""

    #: A feedback-agent session has been requested/persisted.
    created = "created"
    #: The hosted agent accepted the request and is processing.
    running = "running"
    #: The agent finished and the result was persisted.
    done = "done"
    #: The agent errored, timed out, or the run was cancelled.
    failed = "failed"


class FeedbackState(BaseModel):
    """Embedded Layer-2 feedback lifecycle for a :class:`QARecord`."""

    status: FeedbackStatus = FeedbackStatus.created
    #: Failure context (e.g. ``agent_invocation_failed``, ``timeout``);
    #: ``None`` while healthy.
    error: str | None = None
    created_at: datetime | None = None
    updated_at: datetime | None = None


class QARecord(BaseModel):
    """Durable two-layer status row for a single QA thread.

    Persisted in the Cosmos ``qa-records`` container (partition key
    ``/tenant_id``).
    """

    id: str  # {conversation_type}:{conversation_id}
    tenant_id: str
    conversation_id: str
    conversation_type: ConversationType
    #: The Teams channel the thread belongs to (used to exclude testing
    #: channels from the feedback loop). Falls back to the channel segment of
    #: ``conversation_id`` when not explicitly captured.
    channel_id: str | None = None

    #: Deep link back to the conversation thread (for triage / issue body).
    message_link: str | None = None

    # -- Layer 1: QA lifecycle --------------------------------------------
    qa_status: QAStatus = QAStatus.ongoing
    verdict: BotAnswerVerdict | None = None
    reasoning: str | None = None
    confidence: float | None = None
    has_expert_reply: bool = False
    message_count: int = 0

    # -- Layer 2: feedback lifecycle (present once qa_status == failed) ----
    feedback: FeedbackState | None = None

    # -- Bookkeeping ------------------------------------------------------
    #: Timestamp of the latest message in the thread (drives the "did the
    #: thread go quiet?" heuristic the evaluator's `finished` gate refines).
    last_activity_at: datetime | None = None
    #: When this thread was first added to the status table.
    first_seen_at: datetime
    #: When the thread was last evaluated by the LLM judge.
    evaluated_at: datetime | None = None
    created_at: datetime
    updated_at: datetime

    @staticmethod
    def build_id(
        conversation_type: ConversationType,
        conversation_id: str,
    ) -> str:
        """Return the deterministic thread key used as the Cosmos ``id``."""
        return f"{conversation_type.value}:{conversation_id}"

    def to_cosmos(self) -> dict[str, Any]:
        """Serialize for Cosmos ``upsert_item`` (enum values as strings)."""
        return self.model_dump(mode="json")

    @classmethod
    def from_cosmos(cls, doc: dict[str, Any]) -> "QARecord":
        """Deserialize a Cosmos document, stripping system (``_``) fields."""
        cleaned = {k: v for k, v in doc.items() if not k.startswith("_")}
        return cls.model_validate(cleaned)


__all__ = [
    "QAStatus",
    "FeedbackStatus",
    "FeedbackState",
    "QARecord",
]
