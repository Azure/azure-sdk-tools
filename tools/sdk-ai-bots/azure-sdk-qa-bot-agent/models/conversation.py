"""Conversation models for Cosmos DB mapping."""

from __future__ import annotations

from datetime import datetime
from enum import Enum
from typing import Any

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


class ConversationMessageAttachment(BaseModel):
    """A Teams message attachment, in the shape the Teams connector returns it.

    Field names follow the connector (Graph ``chatMessageAttachment``) instead of
    this module's snake_case, so a stored attachment reads like connector output.
    """

    id: str | None = None
    contentType: str | None = None
    contentUrl: str | None = None
    content: Any = None
    name: str | None = None
    thumbnailUrl: str | None = None
    teamsAppId: str | None = None


class ConversationMessageExtraInfo(BaseModel):
    channel_id: str | None = None
    message_link: str | None = None
    attachments: list[ConversationMessageAttachment] | None = (
        None  # Files, cards, forwarded messages, etc. as the Teams connector returns them
    )


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
