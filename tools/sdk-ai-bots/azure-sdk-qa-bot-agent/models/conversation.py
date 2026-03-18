"""Conversation models for Cosmos DB mapping."""

from __future__ import annotations

from pydantic import BaseModel
from datetime import datetime
from enum import Enum

class UserRole(str, Enum):
    system = "system"
    user = "user"

class ConversationType(str, Enum):
    teams_channel = "teams_channel"

class ConversationMessage(BaseModel):
    id: str  # Message ID from Teams
    channel_id: str  # Channel ID
    sender_id: str  # User ID
    sender_name: str  # Display name
    content: str  # Message content
    created_at: datetime  # UTC datetime
    conversation_id: str | None = None  # Customer Conversation ID
    conversation_type: ConversationType | None = None  # Customer Conversation Type
