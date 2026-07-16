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


class SaveConversationMessageResponse(BaseModel):
    pass


class BotAnswerVerdict(str, Enum):
    """The LLM's judgement of a bot answer's correctness."""

    Correct = "correct"
    Incorrect = "incorrect"
    Unknown = "unknown"


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
