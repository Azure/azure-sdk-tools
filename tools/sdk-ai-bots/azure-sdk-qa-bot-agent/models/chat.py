"""Data models for the chat (QA) scenario."""

from __future__ import annotations

from pydantic import BaseModel
from models.conversation import ConversationType


class ChatRequest(BaseModel):
    """Incoming chat request from the Teams App."""

    tenant_id: str = "unknown"
    conversation_id: str | None = None
    conversation_type: ConversationType | None = None
    message: str
    history: list[dict] = []
    sources: list[str] = []


class Reference(BaseModel):
    """A knowledge reference cited in a chat response."""

    title: str
    source: str
    link: str
    content: str = ""


class ChatResponse(BaseModel):
    """Chat response returned to the caller."""

    answer: str
    conversation_id: str | None = None
    references: list[Reference] = []
    intention: str | None = None
