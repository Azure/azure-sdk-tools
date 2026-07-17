"""Conversation models for Cosmos DB mapping."""

from __future__ import annotations

from datetime import datetime
from enum import Enum

from pydantic import BaseModel, Field


class Role(str, Enum):
    """Message roles in the conversation."""

    User = "user"
    Assistant = "assistant"
    System = "system"
    Developer = "developer"


class ConversationType(str, Enum):
    teams_channel = "teams_channel"


class ConversationDocumentType(str, Enum):
    mapping = "conversation_mapping"
    message = "conversation_message"
    feedback = "conversation_feedback"


class ConversationPartitionPrefix(str, Enum):
    channel = "channel"


class ConversationMessage(BaseModel):
    id: str  # Message ID from Teams
    tenant_id: str | None = None  # Tenant ID
    sender_role: Role  # Message sender role
    sender_id: str  # User ID
    sender_name: str  # Display name
    content: str  # Message content
    created_at: datetime  # UTC datetime
    conversation_id: str | None = None  # Customer Conversation ID
    conversation_type: ConversationType | None = None  # Customer Conversation Type
    trace_id: str | None = None  # OTel trace id of the turn (bot messages only)
    should_reply: bool | None = (
        None  # Whether the message passed intention recognition (in bot scope)
    )
    extra_info: ConversationMessageExtraInfo | None = (
        None  # Any additional info(channel_id, etc.)
    )


class ConversationMessageExtraInfo(BaseModel):
    channel_id: str | None = None
    message_link: str | None = None


class UserFeedback(BaseModel):
    """A user's explicit reaction to a bot answer (thumbs up/down + notes).

    Captured from the Teams feedback card and attached to the bot message it
    concerns so thread queries can surface it.
    """

    reaction: str  # good | bad | unknown
    comment: str | None = None
    reasons: list[str] = Field(default_factory=list)
    user_name: str | None = None
    link: str | None = None
    created_at: datetime | None = None


class ConversationMappingItem(BaseModel):
    id: str
    customer_conversation_id: str
    mapping_key: str
    agent_conversation_id: str
    conversation_type: ConversationType | None = None
    document_type: ConversationDocumentType = Field(
        default=ConversationDocumentType.mapping
    )


class ConversationMessageItem(ConversationMessage):
    conversation_partition: str
    document_type: ConversationDocumentType = Field(
        default=ConversationDocumentType.message
    )


class ConversationFeedbackItem(BaseModel):
    """A stored user-feedback document in the conversation partition.

    Lives in the ``conversation-messages`` container alongside message
    documents (``document_type = conversation_feedback``) so it can be
    queried by ``conversation_partition`` and attached to the bot message it
    targets.
    """

    id: str
    conversation_id: str
    conversation_type: ConversationType
    conversation_partition: str
    document_type: ConversationDocumentType = Field(
        default=ConversationDocumentType.feedback
    )
    # The bot message this feedback concerns. When absent, callers attach it
    # to the most recent bot message at or before ``feedback.created_at``.
    target_message_id: str | None = None
    feedback: UserFeedback


class SaveConversationMessageResponse(BaseModel):
    pass


class BotAnswerVerdict(str, Enum):
    """The LLM's judgement of a bot answer's correctness."""

    Correct = "correct"
    Incorrect = "incorrect"
    Unknown = "unknown"


class ConversationState(str, Enum):
    """Whether a conversation thread has concluded.

    The evaluator judges this **before** the correctness verdict: an
    ``ongoing`` thread is still in flight (e.g. the poster just asked a
    follow-up the bot has not answered, or a human is mid-discussion), so
    its verdict is not yet final and it should stay ``ongoing`` in the QA
    status table.
    """

    Ongoing = "ongoing"
    Finished = "finished"


class ConversationEvaluationItem(BaseModel):
    """A single conversation's evaluation and the context it was judged on.

    This is the conversation-faced result produced by the evaluation logic in
    :class:`services.conversation_service.ConversationService`. 
    """

    conversation_id: str
    conversation_partition: str
    transcript: str
    message_count: int
    has_expert_reply: bool
    state: ConversationState = Field(
        default=ConversationState.Ongoing,
        description="Whether the thread has concluded or is still ongoing.",
    )
    verdict: BotAnswerVerdict
    reasoning: str = Field(
        description="Short explanation of why this verdict was chosen."
    )
    confidence: float = Field(
        default=0.0,
        ge=0.0,
        le=1.0,
        description="Model confidence in the verdict, 0-1.",
    )
    evaluated_at: datetime
    message_link: str | None = Field(
        default=None,
        description="Optional channel deep link back to the conversation.",
    )
