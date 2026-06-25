"""Data models for the intention detection endpoint."""

from __future__ import annotations

from pydantic import BaseModel
from models.conversation import ConversationType
from models.chat import Message


class IntentionRequest(BaseModel):
    """Request for intention classification.

    The Logic App provides the message content and conversation metadata.
    The server checks saved conversation messages to determine whether
    an expert has already replied, then falls back to LLM classification.
    """

    message: Message
    conversation_id: str | None = None
    conversation_type: ConversationType | None = None


class IntentionResponse(BaseModel):
    """Result of intention classification."""

    should_respond: bool
    reason: str
