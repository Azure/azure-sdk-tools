"""Conversation models for Cosmos DB mapping."""

from __future__ import annotations

from datetime import datetime
from enum import Enum

from pydantic import BaseModel, Field

class UserRole(str, Enum):
    system = "system"
    user = "user"

class ConversationType(str, Enum):
    teams_channel = "teams_channel"


class ConversationDocumentType(str, Enum):
    mapping = "conversation_mapping"
    message = "conversation_message"


class ConversationPartitionPrefix(str, Enum):
    channel = "channel"

class ConversationMessage(BaseModel):
    id: str  # Message ID from Teams
    channel_id: str  # Channel ID
    sender_id: str  # User ID
    sender_name: str  # Display name
    content: str  # Message content
    created_at: datetime  # UTC datetime
    conversation_id: str | None = None  # Customer Conversation ID
    conversation_type: ConversationType | None = None  # Customer Conversation Type


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
